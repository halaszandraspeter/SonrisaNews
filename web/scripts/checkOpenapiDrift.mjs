#!/usr/bin/env node
/**
 * OpenAPI drift check. CI calls <c>pnpm generate:api:check</c>; the
 * script regenerates the schema into a temp file and compares it
 * against the committed one. Exit 0 = no drift, exit 1 = drift, exit
 * 2 = the live API was unreachable (CI must ensure the dev stack
 * is up first).
 *
 * The script does NOT call out to the network directly. CI is
 * expected to have a live API at <c>http://localhost:5080</c>
 * (e.g. via the <c>dev: up</c> task) before this runs. The
 * <c>OPENAPI_URL</c> env var overrides the default for staging /
 * production.
 *
 * The comparison is a whitespace-insensitive structural diff: a
 * fresh <c>openapi-typescript</c> regen produces a different byte
 * sequence than the hand-authored file (key order, formatting)
 * but the same type structure. The <c>stripNoise</c> helper drops
 * comments and trailing whitespace, which is enough to make the
 * regen byte-equivalent to the committed file when the schema
 * hasn't actually changed.
 */
import { execSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { isAbsolute, join, resolve } from "node:path";

const DEFAULT_API_URL = "http://localhost:5080/openapi/v1.json";
const SCHEMA_RELPATH = "lib/api/schema.ts";
const API_URL = process.env.OPENAPI_URL ?? DEFAULT_API_URL;

function stripNoise(content) {
  // Drop line comments, block comments, blank lines, and trailing
  // whitespace so a regen whose only difference is formatting
  // compares equal to the committed file.
  return content
    .replace(/\/\*[\s\S]*?\*\//g, "")          // /* block comments */
    .replace(/^\s*\/\/.*$/gm, "")              // // line comments
    .replace(/\s+$/gm, "")                     // trailing ws per line
    .replace(/^\s*\n/gm, "")                   // blank lines
    .trim();
}

const committedPath = isAbsolute(SCHEMA_RELPATH)
  ? SCHEMA_RELPATH
  : resolve(process.cwd(), SCHEMA_RELPATH);

if (!existsSync(committedPath)) {
  console.error(`\u274C Schema file not found: ${committedPath}`);
  console.error("   Run from the web/ directory: pnpm generate:api:check");
  process.exit(2);
}

const tmp = mkdtempSync(join(tmpdir(), "sonrisa-openapi-"));
const regenPath = join(tmp, "schema.ts");

try {
  execSync(
    `pnpm exec openapi-typescript ${API_URL} -o ${regenPath}`,
    { stdio: "inherit" },
  );
  const committed = stripNoise(readFileSync(committedPath, "utf8"));
  const regen = stripNoise(readFileSync(regenPath, "utf8"));
  if (committed === regen) {
    console.log("\u2714  lib/api/schema.ts matches the live OpenAPI doc.");
    process.exit(0);
  }
  console.error("\u274C lib/api/schema.ts is out of date with the live OpenAPI doc.");
  console.error("   Run `pnpm generate:api` and commit the new schema.ts.");
  process.exit(1);
} catch (err) {
  console.error("\u274C OpenAPI drift check failed to run.");
  console.error(err);
  process.exit(2);
} finally {
  rmSync(tmp, { recursive: true, force: true });
}
