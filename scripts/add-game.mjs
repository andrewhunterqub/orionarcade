#!/usr/bin/env node
// Register (or refresh) a Unity WebGL build in site/games/games.json.
//
//   npm run add-game -- <slug> [--title "My Game"] [--description "..."] [--tags arcade,puzzle]
//                              [--controls "Arrow keys to move"] [--aspect 16:9] [--no-mobile]
//
// Build your Unity project for WebGL into site/games/<slug>/ first. Running this again for an
// existing slug re-detects the build files and updates any fields you pass, keeping the rest.

import path from "node:path";
import { parseArgs } from "node:util";
import { GAMES, ROOT, SLUG_RE, detectBuild, exists, needsServerHeaders, readManifest, writeManifest } from "./lib.mjs";

const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    title: { type: "string" },
    description: { type: "string" },
    tags: { type: "string" },
    controls: { type: "string" },
    aspect: { type: "string" },
    version: { type: "string" },
    "no-mobile": { type: "boolean" },
  },
});

const slug = positionals[0];
if (!slug || !SLUG_RE.test(slug)) {
  console.error("Usage: npm run add-game -- <slug> [--title ...]\n  <slug> must be lowercase letters, numbers and dashes, e.g. space-dodge");
  process.exit(1);
}

try {
  // A folder with index.html and no Unity Build/ folder is a plain web game.
  const isHtml = !(await exists(path.join(GAMES, slug, "Build"))) && (await exists(path.join(GAMES, slug, "index.html")));
  const build = isHtml ? {} : await detectBuild(slug);
  const manifest = await readManifest();
  manifest.games ??= [];

  let game = manifest.games.find((g) => g.slug === slug);
  const isNew = !game;
  if (isNew) {
    game = {
      slug,
      title: slug.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()),
      description: "",
      tags: [],
      added: new Date().toISOString().slice(0, 10),
    };
    manifest.games.push(game);
  }

  if (values.title) game.title = values.title;
  if (values.description) game.description = values.description;
  if (values.tags) game.tags = values.tags.split(",").map((t) => t.trim().toLowerCase()).filter(Boolean);
  if (values.controls) game.controls = values.controls;
  if (values.aspect) game.aspect = values.aspect;
  if (values.version) game.version = values.version;
  if (values["no-mobile"]) game.mobile = false;

  for (const name of ["thumbnail.png", "thumbnail.jpg", "thumbnail.webp"]) {
    if (await exists(path.join(GAMES, slug, name))) {
      game.thumbnail = name;
      break;
    }
  }
  if (isHtml) {
    game.type = "html";
    game.entry = "index.html";
    delete game.build;
  } else {
    delete game.type;
    delete game.entry;
    game.build = build;
    if (await exists(path.join(GAMES, slug, "StreamingAssets"))) game.streamingAssets = "StreamingAssets";
  }

  await writeManifest(manifest);

  console.log(`${isNew ? "Added" : "Updated"} "${game.title}" (${slug})${isHtml ? " as a web (HTML) game" : ""}`);
  for (const [k, v] of Object.entries(build)) console.log(`  ${k.padEnd(9)} ${v}`);
  if (!game.thumbnail) console.log(`  Tip: add site/games/${slug}/thumbnail.png (16:10, e.g. 640x400) for a nicer card.`);

  const compressed = needsServerHeaders(build);
  if (compressed.length) {
    console.warn(
      `\nWarning: ${compressed.join(", ")} use gzip/brotli without Decompression Fallback.\n` +
        "GitHub Pages can't serve these correctly. In Unity: Player Settings > Publishing Settings, either\n" +
        "set Compression Format to Disabled, or keep it and tick Decompression Fallback. Then rebuild.",
    );
  }
  console.log(`\nManifest: ${path.relative(ROOT, path.join(GAMES, "games.json"))}`);
} catch (err) {
  console.error(`Error: ${err.message}`);
  process.exit(1);
}
