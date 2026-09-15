#!/usr/bin/env bash
#
# Builds the Sprint 2 delivery artifacts into dist/.
#
#   dist/backend/              published API + configuration, no secrets
#   dist/mobile/               mobile project, ready to build
#   dist/presentation-assets/  openapi.json, diagrams, endpoint list, test results
#
# Usage:  ./scripts/build-artifacts.sh
#
#   FLOW_OPENAPI_PORT   port the API is started on to export the specification
#                       (default 41999; change it if that one is taken)
#   FLOW_SKIP_TESTS=1   skip the suite, for a pipeline that already ran it
#
# Requires: .NET 8 SDK, Node 20+. A running MongoDB is only needed for the
# integration tests, which are skipped when FLOW_TEST_MONGO_URI is unset and no
# Docker daemon is available.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$ROOT/dist"
STAGE="$DIST/staging"

cd "$ROOT"

echo "==> Cleaning dist/"
rm -rf "$DIST"
mkdir -p "$DIST/backend" "$DIST/mobile" "$DIST/presentation-assets" "$STAGE"

# ---------------------------------------------------------------------------
# Backend
# ---------------------------------------------------------------------------
echo "==> Building the backend"
dotnet publish src/Flow.API/Flow.API.csproj -c Release -o "$STAGE/api" --nologo

echo "==> Assembling dist/backend"
cp -r src "$DIST/backend/src"
cp -r tests "$DIST/backend/tests"
cp Flow.sln Dockerfile docker-compose.yml .env.example "$DIST/backend/"
cp README.md ENGINEERING.md PROJECT_DECISIONS.md "$DIST/backend/"
cp -r docs "$DIST/backend/docs"

# Build output and local settings never travel with source.
find "$DIST/backend" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
find "$DIST/backend" -name "appsettings.*.local.json" -delete 2>/dev/null || true

# ---------------------------------------------------------------------------
# OpenAPI
# ---------------------------------------------------------------------------
echo "==> Exporting openapi.json"

# The specification is generated from the running application, so it reflects the
# real route table rather than a hand-maintained copy.
#
# That means a database is required, and not only for the health endpoint: startup seeds
# the Identity roles before serving anything, and aborts if it cannot reach MongoDB. Point
# Mongo__ConnectionString at whatever is available; a standalone server is enough here,
# since nothing on this path opens a transaction.
#
# Fetching over HTTP makes "something answered" the weakest possible evidence. This block
# used to hard-code port 5199 and trust any non-empty response. On a machine where another
# development server already held that port, the API failed to bind, curl reached the
# stranger, and its HTML was written straight to openapi.json. The run only fell over
# several steps later, when a JSON parse tripped on it — and only because that particular
# stranger served a page. One serving valid JSON would have shipped.
#
# So the export proves three things before publishing anything: our own process is still
# alive, the bytes came back from it, and the document is Flow's.
OPENAPI_PORT="${FLOW_OPENAPI_PORT:-41999}"
SPEC_URL="http://localhost:$OPENAPI_PORT/swagger/v1/swagger.json"
CANDIDATE="$STAGE/openapi.candidate.json"
API_LOG="$STAGE/openapi-export.log"
API_PID=""

# Only ever the process this script started. A stranger on the port is the problem being
# solved here, not something to go killing.
stop_api() {
  if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
    kill "$API_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true
  fi
  API_PID=""
}
trap stop_api EXIT INT TERM

export_failed() {
  echo "    ERROR: $1" >&2
  echo "    Last lines of the API's own output ($API_LOG):" >&2
  tail -n 25 "$API_LOG" >&2 || true
  rm -f "$CANDIDATE"
  exit 1
}

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://localhost:$OPENAPI_PORT" \
Swagger__Enabled=true \
Mongo__EnsureIndexes=false \
Mongo__ConnectionString="${Mongo__ConnectionString:-mongodb://localhost:27017/?replicaSet=rs0}" \
Mongo__Database="${Mongo__Database:-flow_openapi_export}" \
JwtSettings__SecretKey="openapi-export-only-not-a-real-secret-32b" \
dotnet "$STAGE/api/Flow.API.dll" > "$API_LOG" 2>&1 &
API_PID=$!

echo "    starting the API on port $OPENAPI_PORT (override with FLOW_OPENAPI_PORT)"

exported=0

for _ in $(seq 1 40); do
  # Liveness first, every time round. If our API is gone then whatever is listening on
  # that port belongs to somebody else, and asking it for a specification is precisely
  # the bug. A port already in use is the ordinary reason to end up here.
  if ! kill -0 "$API_PID" 2>/dev/null; then
    API_PID=""
    export_failed "the API exited before serving the specification. A port already taken and an unreachable database are the usual reasons; its own output below says which. If port $OPENAPI_PORT is in use, set FLOW_OPENAPI_PORT to a free one."
  fi

  # Downloaded beside the deliverable, never onto it: a rejected candidate must not be
  # able to leave a half-written openapi.json behind.
  if curl -fsS "$SPEC_URL" -o "$CANDIDATE" 2>/dev/null; then
    exported=1
    break
  fi

  sleep 1
done

[ "$exported" = "1" ] || export_failed "the API never served $SPEC_URL."

# Answering is not the same as being the right server, and being JSON is not the same as
# being ours. scripts/check-openapi.mjs is the single definition of that difference; the
# CI job applies it again to the published file.
if ! node scripts/check-openapi.mjs "$CANDIDATE"; then
  export_failed "what answered on port $OPENAPI_PORT did not serve Flow's specification."
fi

stop_api
mv "$CANDIDATE" "$DIST/presentation-assets/openapi.json"

# ---------------------------------------------------------------------------
# Mobile
# ---------------------------------------------------------------------------
echo "==> Assembling dist/mobile"
mkdir -p "$DIST/mobile/flow-mobile"

# node_modules is reproducible from the lockfile and would dominate the archive.
tar --exclude=node_modules --exclude=.expo --exclude=dist \
    -cf - -C "$ROOT" mobile | tar -xf - -C "$DIST/mobile/flow-mobile" --strip-components=1

# ---------------------------------------------------------------------------
# Presentation assets
# ---------------------------------------------------------------------------
echo "==> Assembling dist/presentation-assets"
cp docs/sprint-2/*.md "$DIST/presentation-assets/"

echo "==> Recording the endpoint list"
if [ -s "$DIST/presentation-assets/openapi.json" ]; then
  node -e '
    const spec = require(process.argv[1]);
    const rows = [];
    for (const [path, methods] of Object.entries(spec.paths ?? {})) {
      for (const [method, op] of Object.entries(methods)) {
        rows.push([
          method.toUpperCase().padEnd(6),
          path.padEnd(52),
          (op.tags ?? []).join(",").padEnd(12),
          (op.summary ?? "").split("\n")[0],
        ].join(" "));
      }
    }
    rows.sort();
    console.log(`Flow API — ${rows.length} endpoints\n`);
    console.log(rows.join("\n"));
  ' "$DIST/presentation-assets/openapi.json" > "$DIST/presentation-assets/endpoints.txt"
fi

# The suite runs here, and a failure stops the packaging.
#
# It used to print a warning and carry on, which meant a red suite still produced a dist/
# and two archives that looked exactly like a finished delivery. That is the one outcome a
# packaging script must never have: everything downstream — someone attaching the archive
# to a submission, a pipeline uploading it — treats "the script succeeded" as the signal,
# and there was nothing else to read.
#
# FLOW_SKIP_TESTS exists for one situation: a pipeline where the suite already ran in an
# earlier job and the MongoDB image would have to be pulled again to prove the same thing.
# Setting it moves the responsibility to the caller, who is then asserting the suite was
# green elsewhere. The CI artefacts job earns that by declaring `needs: backend`, so it
# cannot start unless the backend job — suite included — already passed.
#
# FLOW_TEST_COMMAND replaces the command for scripts/build-artifacts.contract.test.sh,
# which has to be able to produce a red suite on demand. It runs through `bash -c` rather
# than `eval` so a command carrying its own `exit` cannot take this script's exit status
# with it.
echo "==> Running the test suite"

TEST_COMMAND="${FLOW_TEST_COMMAND:-dotnet test Flow.sln --nologo -v quiet}"
tests_failed=0

if [ "${FLOW_SKIP_TESTS:-0}" = "1" ]; then
  echo "    skipped by FLOW_SKIP_TESTS; the caller is responsible for having run them"
  echo "Test suite skipped in this run (FLOW_SKIP_TESTS=1)." > "$STAGE/test-output.txt"
else
  if [ -n "${FLOW_TEST_COMMAND:-}" ]; then
    echo "    running an overridden command: $TEST_COMMAND"
  fi

  if bash -c "$TEST_COMMAND" > "$STAGE/test-output.txt" 2>&1; then
    echo "    tests passed"
  else
    tests_failed=1
  fi
fi

# Recorded either way. On a green run it is a delivery asset; on a red one it is the
# evidence of what broke, and it has to exist before the exit below.
grep -E "Aprovado!|Passed!|Com falha|Failed!" "$STAGE/test-output.txt" \
  > "$DIST/presentation-assets/test-results.txt" 2>/dev/null || \
  cp "$STAGE/test-output.txt" "$DIST/presentation-assets/test-results.txt"

if [ "$tests_failed" = "1" ]; then
  echo "    ERROR: the test suite failed, so no delivery will be packaged." >&2

  # $STAGE survives because the run stops here; the cleanup at the end is never reached.
  echo "    Full output kept at $STAGE/test-output.txt. What it reported:" >&2

  # Both languages: the SDK reports in whatever locale the machine is set to.
  summary=$(grep -iE "error|erro|falha|failed" "$STAGE/test-output.txt" | head -n 20 || true)

  if [ -n "$summary" ]; then
    printf '%s\n' "$summary" >&2
  else
    tail -n 20 "$STAGE/test-output.txt" >&2 || true
  fi

  exit 1
fi

# ---------------------------------------------------------------------------
# Archives
# ---------------------------------------------------------------------------
echo "==> Creating archives"
( cd "$DIST/backend" && tar -czf "$DIST/flow-backend.tar.gz" . )
( cd "$DIST/mobile" && tar -czf "$DIST/flow-mobile.tar.gz" . )

rm -rf "$STAGE"

echo
echo "Artifacts written to dist/:"
find "$DIST" -maxdepth 2 -type f -exec ls -lh {} \; | awk '{print "   ", $5, $9}'
echo
echo "The APK is built separately and needs EAS credentials:"
echo "    cd mobile && eas build --platform android --profile preview"
