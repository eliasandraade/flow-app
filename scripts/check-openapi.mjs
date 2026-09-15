#!/usr/bin/env node
//
// What an exported openapi.json has to prove before it is allowed to be a deliverable.
//
// The specification is fetched over HTTP from an application this repository started a
// moment earlier, and "something answered on that port" is a much weaker fact than it
// looks. A port can already be held by an unrelated process; the application then fails to
// bind and the fetch succeeds anyway, against the wrong server. That happened: a local
// development server answered, its HTML landed in dist/presentation-assets/openapi.json,
// and nothing noticed until a later step tried to parse it. Had the neighbour served valid
// JSON instead of a page, the wrong specification would have shipped.
//
// So the checks here are about identity, not well-formedness. Parsing proves the bytes are
// JSON. Only the title, the route surface and the sentinel routes prove they are *ours*.
//
// One definition, used twice: scripts/build-artifacts.sh runs it against the candidate
// before publishing the file, and the CI job runs it against what landed in dist/. Two
// separate checks would eventually disagree about what a valid specification is.
//
// Usage:  node scripts/check-openapi.mjs <file>

import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

/** The exported document identifies itself as Flow's, or it is not Flow's. */
export const EXPECTED_TITLE = "Flow API";

/**
 * A floor, never an equality. The surface was 54 operations when this was written and
 * adding a legitimate endpoint must not break the build; losing a quarter of the API
 * without noticing should.
 */
export const MINIMUM_OPERATIONS = 50;

/**
 * Routes that any Flow specification publishes. They exist to catch the case a count
 * cannot: a different product's OpenAPI document, which can be perfectly valid, perfectly
 * large, and still not this API. `flow-score` in particular belongs to nothing else.
 */
export const SENTINEL_PATHS = [
  "/api/v1/auth/login",
  "/api/v1/projects",
  "/api/v1/ideas/{id}/flow-score",
  "/api/v1/dashboard/insights",
];

/** Keys under a path item that are operations; `parameters` and `$ref` are not. */
const HTTP_METHODS = new Set([
  "get", "put", "post", "delete", "options", "head", "patch", "trace",
]);

/**
 * Inspects a document and reports every problem it has, rather than the first.
 * A caller staring at a rejected export wants the whole picture in one go.
 */
export function inspectSpecification(text, options = {}) {
  const {
    expectedTitle = EXPECTED_TITLE,
    minimumOperations = MINIMUM_OPERATIONS,
    sentinelPaths = SENTINEL_PATHS,
  } = options;

  const problems = [];
  let document;

  try {
    document = JSON.parse(text);
  } catch (error) {
    // Quoting the opening bytes turns "Unexpected token '<'" into a diagnosis: an HTML
    // page here means something other than the API answered the request.
    const opening = String(text).trimStart().slice(0, 60).replace(/\s+/g, " ");
    problems.push(`the document is not JSON — it starts with ${JSON.stringify(opening)}`);
    return { ok: false, problems, operations: 0, title: null };
  }

  if (document === null || typeof document !== "object" || Array.isArray(document)) {
    return {
      ok: false,
      problems: ["the document is valid JSON but not a JSON object"],
      operations: 0,
      title: null,
    };
  }

  const version = typeof document.openapi === "string" ? document.openapi : null;

  if (version === null) {
    problems.push('there is no "openapi" field, so this is not an OpenAPI 3 document');
  } else if (!version.startsWith("3.")) {
    problems.push(`"openapi" is ${JSON.stringify(version)}; a 3.x document is expected`);
  }

  const title = typeof document.info?.title === "string" ? document.info.title.trim() : null;

  if (title === null) {
    problems.push('there is no "info.title", so the document identifies nothing');
  } else if (title.toLowerCase() !== expectedTitle.toLowerCase()) {
    problems.push(
      `"info.title" is ${JSON.stringify(title)} rather than ${JSON.stringify(expectedTitle)} — `
      + "this specification belongs to something else",
    );
  }

  const paths = document.paths;

  if (paths === null || typeof paths !== "object" || Array.isArray(paths)) {
    problems.push('there is no "paths" object, so the document describes no API');
    return { ok: false, problems, operations: 0, title };
  }

  let operations = 0;

  for (const item of Object.values(paths)) {
    if (item === null || typeof item !== "object") continue;

    for (const key of Object.keys(item)) {
      if (HTTP_METHODS.has(key.toLowerCase())) operations += 1;
    }
  }

  if (operations < minimumOperations) {
    problems.push(
      `only ${operations} operations are described; at least ${minimumOperations} are expected, `
      + "so this is a partial or foreign surface",
    );
  }

  const missing = sentinelPaths.filter((route) => !Object.hasOwn(paths, route));

  if (missing.length > 0) {
    problems.push(`routes Flow always publishes are missing: ${missing.join(", ")}`);
  }

  return { ok: problems.length === 0, problems, operations, title };
}

function main(argv) {
  const [file] = argv;

  if (!file) {
    console.error("usage: node scripts/check-openapi.mjs <file>");
    return 2;
  }

  let text;

  try {
    text = readFileSync(file, "utf8");
  } catch (error) {
    console.error(`Could not read ${file}: ${error.message}`);
    return 1;
  }

  const result = inspectSpecification(text);

  if (!result.ok) {
    console.error(`${file} is not a usable Flow specification:`);
    for (const problem of result.problems) console.error(`  - ${problem}`);
    return 1;
  }

  console.log(
    `${file}: "${result.title}", ${result.operations} operations, `
    + `all ${SENTINEL_PATHS.length} sentinel routes present.`,
  );

  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  process.exit(main(process.argv.slice(2)));
}
