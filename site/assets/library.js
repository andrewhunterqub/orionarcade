import { loadGames, gameUrl, placeholderGradient } from "./manifest.js";

const grid = document.getElementById("grid");
const empty = document.getElementById("empty");
const search = document.getElementById("search");
const tagBar = document.getElementById("tags");

document.getElementById("year").textContent = new Date().getFullYear();

let games = [];
let activeTag = null;

function el(tag, props = {}, children = []) {
  const node = Object.assign(document.createElement(tag), props);
  for (const child of [].concat(children)) if (child) node.append(child);
  return node;
}

function thumbnail(game) {
  const fallback = el("div", {
    className: "thumb thumb-fallback",
    textContent: game.title.trim().charAt(0).toUpperCase(),
  });
  fallback.style.background = placeholderGradient(game.slug);
  if (!game.thumbnail) return fallback;

  const img = el("img", {
    className: "thumb",
    src: gameUrl(game, game.thumbnail),
    alt: "",
    loading: "lazy",
    decoding: "async",
  });
  img.addEventListener("error", () => img.replaceWith(fallback), { once: true });
  return img;
}

function card(game) {
  const tags = (game.tags || []).map((t) => el("span", { textContent: t }));
  return el("a", { className: "card", href: `play.html?game=${encodeURIComponent(game.slug)}` }, [
    thumbnail(game),
    el("div", { className: "card-body" }, [
      el("h2", { textContent: game.title }),
      game.description ? el("p", { textContent: game.description }) : null,
      tags.length ? el("div", { className: "card-tags" }, tags) : null,
    ]),
  ]);
}

function render() {
  const q = search.value.trim().toLowerCase();
  const shown = games.filter((g) => {
    if (activeTag && !(g.tags || []).includes(activeTag)) return false;
    if (!q) return true;
    return [g.title, g.description, ...(g.tags || [])].join(" ").toLowerCase().includes(q);
  });

  grid.replaceChildren(...shown.map(card));

  if (games.length === 0) {
    empty.textContent = "No games yet. New games are on the way!";
  } else if (shown.length === 0) {
    empty.textContent = "No games match your search.";
  }
  empty.hidden = shown.length > 0;
}

function renderTags() {
  const all = [...new Set(games.flatMap((g) => g.tags || []))].sort();
  if (all.length < 2) return;

  const buttons = [null, ...all].map((tag) => {
    const b = el("button", { type: "button", className: "tag", textContent: tag ?? "All" });
    b.setAttribute("aria-pressed", String(tag === activeTag));
    b.addEventListener("click", () => {
      activeTag = tag;
      for (const other of tagBar.children) other.setAttribute("aria-pressed", String(other === b));
      render();
    });
    return b;
  });
  tagBar.replaceChildren(...buttons);
}

search.addEventListener("input", render);

try {
  games = (await loadGames()).filter((g) => !g.hidden);
  // Newest first, then alphabetical.
  games.sort((a, b) => String(b.added || "").localeCompare(String(a.added || "")) || a.title.localeCompare(b.title));
  renderTags();
  render();
} catch (err) {
  console.error(err);
  empty.textContent = "Sorry, the game list couldn't be loaded. Please try again later.";
  empty.hidden = false;
}
