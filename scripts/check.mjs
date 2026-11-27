#!/usr/bin/env node
// Validate site/games/games.json: required fields, unique slugs, and that every referenced file exists.
// Exits non-zero on errors so CI blocks a broken deploy.

import path from "node:path";
import { stat } from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { GAMES, SLUG_RE, exists, needsServerHeaders, readManifest } from "./lib.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const GITHUB_FILE_LIMIT = 100 * 1024 * 1024;
const errors = [];
const warnings = [];

let manifest;
try {
  manifest = await readManifest();
} catch (err) {
  console.error(`games.json is not valid JSON: ${err.message}`);
  process.exit(1);
}

if (!Array.isArray(manifest.games)) errors.push(`games.json must contain a "games" array`);

const seen = new Set();
for (const [i, g] of (manifest.games || []).entries()) {
  const where = g?.slug ? `"${g.slug}"` : `games[${i}]`;
  if (!g?.slug || !SLUG_RE.test(g.slug)) {
    errors.push(`${where}: slug must be lowercase letters, numbers and dashes`);
    continue;
  }
  if (seen.has(g.slug)) errors.push(`${where}: duplicate slug`);
  seen.add(g.slug);
  if (!g.title) errors.push(`${where}: missing title`);
  if (g.tags && !Array.isArray(g.tags)) errors.push(`${where}: tags must be an array`);

  const isHtml = g.type === "html";
  const b = isHtml ? {} : g.build || {};
  if (!isHtml) {
    for (const key of ["loader", "framework", "data", "code"]) {
      if (!b[key]) errors.push(`${where}: build.${key} is missing (run: npm run add-game -- ${g.slug})`);
    }
  }

  const files = [...Object.values(b), isHtml ? g.entry || "index.html" : null, g.thumbnail].filter(Boolean);
  for (const rel of files) {
    const abs = path.resolve(GAMES, g.slug, rel);
    if (!abs.startsWith(path.join(GAMES, g.slug) + path.sep)) {
      errors.push(`${where}: path escapes the game folder: ${rel}`);
    } else if (!(await exists(abs))) {
      errors.push(`${where}: file not found: site/games/${g.slug}/${rel}`);
    } else if ((await stat(abs)).size > GITHUB_FILE_LIMIT) {
      errors.push(`${where}: ${rel} is over GitHub's 100 MB file limit; enable compression or reduce build size`);
    }
  }

  const compressed = needsServerHeaders(b);
  if (compressed.length) {
    warnings.push(
      `${where}: ${compressed.join(", ")} need Content-Encoding headers, which GitHub Pages doesn't send. ` +
        "Rebuild with Compression Format: Disabled or with Decompression Fallback enabled.",
    );
  }
}

// Repo-wide size scan: every file git would commit (tracked + untracked-not-ignored).
// GitHub hard-rejects pushes containing a file over 100 MB, so catch it before committing.
const SIZE_WARN = 50 * 1024 * 1024;
const SITE_BUDGET = 1024 * 1024 * 1024;
try {
  const out = execFileSync("git", ["ls-files", "-z", "--cached", "--others", "--exclude-standard"], {
    cwd: ROOT,
    maxBuffer: 256 * 1024 * 1024,
  }).toString();
  let siteTotal = 0;
  for (const rel of out.split("\0").filter(Boolean)) {
    let size;
    try {
      size = (await stat(path.join(ROOT, rel))).size;
    } catch {
      continue; // deleted in the working tree
    }
    if (rel.startsWith("site/")) siteTotal += size;
    const mb = (size / 1048576).toFixed(1);
    if (size > GITHUB_FILE_LIMIT) errors.push(`${rel} is ${mb} MB, over GitHub's 100 MB file limit (ignore it, split it, or shrink it)`);
    else if (size > SIZE_WARN) warnings.push(`${rel} is ${mb} MB; keep files well under 100 MB`);
  }
  if (siteTotal > SITE_BUDGET) {
    errors.push(`site/ totals ${(siteTotal / 1048576).toFixed(0)} MB, over the 1 GB GitHub Pages budget`);
  }
} catch (err) {
  warnings.push(`size scan skipped (git not available?): ${err.message}`);
}

for (const w of warnings) console.warn(`warning: ${w}`);
for (const e of errors) console.error(`error: ${e}`);
const count = manifest.games?.length ?? 0;
if (errors.length) {
  console.error(`\n${errors.length} error(s)`);
  process.exit(1);
}
console.log(`games.json OK (${count} game${count === 1 ? "" : "s"})`);
