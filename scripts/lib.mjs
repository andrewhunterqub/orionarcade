import { readFile, writeFile, readdir, stat } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
export const SITE = path.join(ROOT, "site");
export const GAMES = path.join(SITE, "games");
export const MANIFEST = path.join(GAMES, "games.json");

export const SLUG_RE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export async function readManifest() {
  return JSON.parse(await readFile(MANIFEST, "utf8"));
}

export async function writeManifest(data) {
  await writeFile(MANIFEST, JSON.stringify(data, null, 2) + "\n");
}

export async function exists(p) {
  try {
    await stat(p);
    return true;
  } catch {
    return false;
  }
}

const COMPRESSED = "(?:\\.gz|\\.br|\\.unityweb)?";

/**
 * Find the Unity WebGL build files in site/games/<slug>/Build.
 * Returns paths relative to the game folder, e.g. "Build/MyGame.data.br".
 */
export async function detectBuild(slug) {
  const buildDir = path.join(GAMES, slug, "Build");
  if (!(await exists(buildDir))) {
    throw new Error(`No Build folder at ${path.relative(ROOT, buildDir)}. Build your Unity WebGL project into site/games/${slug}/`);
  }
  const files = await readdir(buildDir);

  const loaders = files.filter((f) => f.endsWith(".loader.js"));
  if (loaders.length !== 1) {
    throw new Error(`Expected exactly one *.loader.js in ${path.relative(ROOT, buildDir)}, found ${loaders.length}.`);
  }
  const prefix = loaders[0].slice(0, -".loader.js".length);
  const esc = prefix.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const find = (ext, required = true) => {
    const re = new RegExp(`^${esc}\\.${ext}${COMPRESSED}$`);
    const match = files.find((f) => re.test(f));
    if (!match && required) throw new Error(`Missing ${prefix}.${ext}[.gz|.br|.unityweb] in ${path.relative(ROOT, buildDir)}`);
    return match ? `Build/${match}` : undefined;
  };

  const build = {
    loader: `Build/${loaders[0]}`,
    framework: find("framework\\.js"),
    data: find("data"),
    code: find("wasm"),
  };
  const symbols = find("symbols\\.json", false);
  if (symbols) build.symbols = symbols;
  return build;
}

/** Files compressed with gzip/brotli need Content-Encoding headers that many static hosts (incl. GitHub Pages) don't send. */
export function needsServerHeaders(build) {
  return Object.values(build).filter((f) => /\.(gz|br)$/.test(f));
}
