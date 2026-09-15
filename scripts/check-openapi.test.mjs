// The specification check, checked.
//
// This validation only earns its place if it rejects things, and the interesting rejections
// are not malformed files — they are documents that look entirely reasonable and belong to
// someone else. Those are the cases a human reviewer waves through.
//
// Run with:  node --test scripts/
//
// The "real Flow specification" case reads scripts/fixtures/flow-openapi-surface.json,
// which is the exported document reduced to the parts this validation reads: the version,
// the title, and every route with its real methods. Keeping the whole export in the
// repository would commit a build artefact that goes stale; keeping the surface keeps the
// part that matters. The complete, freshly exported file is validated for real on every CI
// run, by the same function.

import test from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

import { inspectSpecification } from "./check-openapi.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const fixture = () =>
  JSON.parse(readFileSync(join(here, "fixtures", "flow-openapi-surface.json"), "utf8"));

const inspect = (value) =>
  inspectSpecification(typeof value === "string" ? value : JSON.stringify(value));

const complaints = (result) => result.problems.join(" | ");

// ─── The document Flow actually publishes ──────────────────────────────────

test("the real Flow specification is accepted", () => {
  const result = inspect(fixture());

  assert.equal(result.ok, true, complaints(result));
  assert.equal(result.title, "Flow API");
  assert.ok(result.operations >= 50, `only ${result.operations} operations counted`);
});

// ─── The failure that was actually observed ────────────────────────────────

test("a web page served by a neighbouring process is rejected", () => {
  // This is verbatim the shape of what a local development server answered with when it
  // held the export port: the fetch succeeded, the file was not empty, and it was HTML.
  const result = inspect('<!doctype html>\n<html lang="pt-BR">\n  <head>\n    <title>TRIAD</title>');

  assert.equal(result.ok, false);
  assert.match(complaints(result), /not JSON/);
  assert.match(complaints(result), /doctype/, "the diagnosis should quote what arrived");
});

test("an empty body is rejected", () => {
  assert.equal(inspect("").ok, false);
});

// ─── The failure that would not have been observed ─────────────────────────

test("another product's OpenAPI document is rejected", () => {
  // The dangerous neighbour: HTTP 200, valid JSON, genuinely an OpenAPI document. Parsing
  // accepts it, a non-empty check accepts it, and it is not Flow. Only the title and the
  // route surface can tell.
  const result = inspect({
    openapi: "3.0.1",
    info: { title: "Outro Sistema", version: "1.0" },
    paths: {},
  });

  assert.equal(result.ok, false);
  assert.match(complaints(result), /belongs to something else/);
});

test("arbitrary JSON is rejected", () => {
  const result = inspect({ status: "ok", uptime: 1234 });

  assert.equal(result.ok, false);
  assert.match(complaints(result), /not an OpenAPI 3 document/);
});

test("a JSON array is rejected", () => {
  assert.equal(inspect([1, 2, 3]).ok, false);
});

// ─── Ours, but not whole ───────────────────────────────────────────────────

test("a Flow specification missing a sentinel route is rejected", () => {
  const specification = fixture();
  delete specification.paths["/api/v1/ideas/{id}/flow-score"];

  const result = inspect(specification);

  assert.equal(result.ok, false);
  assert.ok(
    result.operations >= 50,
    "the surface must still be large enough, so the sentinel is what fails",
  );
  assert.match(complaints(result), /flow-score/);
});

test("a Flow specification with an implausibly small surface is rejected", () => {
  const specification = fixture();
  const kept = ["/api/v1/auth/login", "/api/v1/projects", "/api/v1/ideas/{id}/flow-score",
    "/api/v1/dashboard/insights"];

  specification.paths = Object.fromEntries(
    Object.entries(specification.paths).filter(([route]) => kept.includes(route)),
  );

  const result = inspect(specification);

  assert.equal(result.ok, false);
  assert.match(complaints(result), /operations are described/);
});

test("a document with no paths at all is rejected", () => {
  const specification = fixture();
  delete specification.paths;

  assert.equal(inspect(specification).ok, false);
});

// ─── The exit code the shell script depends on ─────────────────────────────

test("the command line reports through its exit status", () => {
  const directory = mkdtempSync(join(tmpdir(), "flow-openapi-"));
  const checker = join(here, "check-openapi.mjs");

  const good = join(directory, "good.json");
  writeFileSync(good, JSON.stringify(fixture()));

  const bad = join(directory, "bad.json");
  writeFileSync(bad, "<!doctype html><html></html>");

  // build-artifacts.sh publishes the file only if this command succeeds, so the exit
  // status is the contract, not the message.
  const accepted = execFileSync(process.execPath, [checker, good], { encoding: "utf8" });
  assert.match(accepted, /Flow API/);

  assert.throws(
    () => execFileSync(process.execPath, [checker, bad], { stdio: "pipe" }),
    (error) => error.status === 1,
  );
});
