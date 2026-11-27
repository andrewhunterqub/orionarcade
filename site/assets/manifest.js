// Shared helpers for reading the game manifest (games/games.json).

export const GAMES_ROOT = "games";

export async function loadGames() {
  const res = await fetch(`${GAMES_ROOT}/games.json`, { cache: "no-cache" });
  if (!res.ok) throw new Error(`Could not load game list (HTTP ${res.status})`);
  const data = await res.json();
  // Hidden games stay playable by direct link; the library filters them out.
  return (data.games || []).filter((g) => g && g.slug && g.title);
}

/** Resolve a path inside a game's folder, e.g. gameUrl(game, "Build/x.loader.js"). */
export function gameUrl(game, path) {
  return `${GAMES_ROOT}/${encodeURIComponent(game.slug)}/${path}`;
}

/** Parse "16:9", "16/9" or "1.777" into a CSS aspect-ratio value. */
export function aspectRatio(game) {
  const raw = String(game.aspect || "16:9");
  const m = raw.match(/^\s*(\d+(?:\.\d+)?)\s*[:/x]\s*(\d+(?:\.\d+)?)\s*$/);
  if (m) return `${m[1]} / ${m[2]}`;
  const n = Number(raw);
  return Number.isFinite(n) && n > 0 ? String(n) : "16 / 9";
}

/** Stable colour pair derived from a string, for thumbnail placeholders. */
export function placeholderGradient(seed) {
  let h = 0;
  for (const ch of seed) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  const hue = h % 360;
  return `linear-gradient(135deg, hsl(${hue} 70% 45%), hsl(${(hue + 60) % 360} 70% 30%))`;
}
