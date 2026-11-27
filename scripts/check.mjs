#!/usr/bin/env node
// Validate site/games/games.json: required fields, unique slugs, and that every referenced file exists.
// Exits non-zero on errors so CI blocks a broken deploy.

import path from "node:path";
import { stat } from "node:fs/promises";
import { GAMES, SLUG_RE, exists, needsServerHeaders, readManifest } from "./lib.mjs";

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

for (const w of warnings) console.warn(`warning: ${w}`);
for (const e of errors) console.error(`error: ${e}`);
const count = manifest.games?.length ?? 0;
if (errors.length) {
  console.error(`\n${errors.length} error(s) in games.json`);
  process.exit(1);
}
console.log(`games.json OK (${count} game${count === 1 ? "" : "s"})`);
