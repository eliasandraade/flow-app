#!/usr/bin/env bash
#
# What build-artifacts.sh must, and must not, produce.
#
# The interesting property is a negative one: a red suite must leave no delivery behind.
# That is exactly the case nobody exercises by hand, because running the script normally
# means the tests passed — so it was wrong for a long time without anyone noticing.
#
# The suite itself is replaced by FLOW_TEST_COMMAND here. Running Flow's real tests three
# times over would take longer than the rest of CI and would prove nothing extra: what is
# under test is the script's reaction to a test command's exit status, not the tests. The
# script is otherwise the real one, doing its real publish, export and packaging.
#
# Run with:  bash scripts/build-artifacts.contract.test.sh
#
# Needs whatever a normal run needs, MongoDB included: the OpenAPI export happens before
# the step being tested and is not stubbed out.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

WORK="$(mktemp -d)"
LOG="$WORK/run.log"
RAN_MARKER="$WORK/the-suite-ran"
FAILURES=0

trap 'rm -rf "$WORK"' EXIT

pass() { echo "  ok    $1"; }
fail() { echo "  FAIL  $1"; FAILURES=$((FAILURES + 1)); }

check() {
  if [ "$2" = "$3" ]; then pass "$1"; else fail "$1 (esperado '$3', obtido '$2')"; fi
}

exists() { [ -e "$1" ] && echo yes || echo no; }

# Each case starts from nothing, so an archive found afterwards was made by this run.
run_script() {
  rm -rf dist
  rm -f "$RAN_MARKER"
  env "$@" bash ./scripts/build-artifacts.sh > "$LOG" 2>&1
  echo $?
}

echo "== suíte verde: a entrega é produzida =="
code=$(run_script FLOW_TEST_COMMAND="touch '$RAN_MARKER'")
check "termina com sucesso" "$code" "0"
check "a suíte foi executada" "$(exists "$RAN_MARKER")" "yes"
check "o archive do backend existe" "$(exists dist/flow-backend.tar.gz)" "yes"
check "o archive do mobile existe" "$(exists dist/flow-mobile.tar.gz)" "yes"
check "o resumo dos testes foi gravado" "$(exists dist/presentation-assets/test-results.txt)" "yes"

echo "== suíte vermelha: nenhuma entrega é produzida =="
code=$(run_script FLOW_TEST_COMMAND="touch '$RAN_MARKER'; echo 'Com falha!  - Com falha: 3'; exit 1")
check "termina com erro" "$([ "$code" != "0" ] && echo erro || echo sucesso)" "erro"
check "a suíte foi executada" "$(exists "$RAN_MARKER")" "yes"
check "o archive do backend NÃO existe" "$(exists dist/flow-backend.tar.gz)" "no"
check "o archive do mobile NÃO existe" "$(exists dist/flow-mobile.tar.gz)" "no"
check "a saída dos testes foi preservada" "$(exists dist/staging/test-output.txt)" "yes"

if grep -q "no delivery will be packaged" "$LOG"; then
  pass "o motivo aparece na saída"
else
  fail "o motivo não aparece na saída"
fi

echo "== FLOW_SKIP_TESTS=1: a suíte não roda e o resto segue =="
# The command would fail loudly if it ran at all, so the marker's absence is the proof.
code=$(run_script FLOW_SKIP_TESTS=1 FLOW_TEST_COMMAND="touch '$RAN_MARKER'; exit 1")
check "termina com sucesso" "$code" "0"
check "a suíte NÃO foi executada" "$(exists "$RAN_MARKER")" "no"
check "o archive do backend existe" "$(exists dist/flow-backend.tar.gz)" "yes"
check "o archive do mobile existe" "$(exists dist/flow-mobile.tar.gz)" "yes"

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "contrato de empacotamento: tudo certo"
else
  echo "contrato de empacotamento: $FAILURES verificação(ões) reprovada(s)"
  echo "última saída do script:"
  tail -n 30 "$LOG"
fi

exit "$FAILURES"
