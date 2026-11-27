import { loadGames, gameUrl, aspectRatio } from "./manifest.js";
import { prepareSaves } from "./saves.js";
import { setupFullscreen, isIPhone, isStandalone } from "./fullscreen.js";

const $ = (id) => document.getElementById(id);
const canvas = $("unity-canvas");
const loading = $("loading");
const progress = $("progress");
const progressLabel = $("progress-label");
const errorBox = $("error");
const warningBox = $("warning");
const fullscreenBtn = $("fullscreen");
const syncStatus = $("sync-status");

const isMobile = /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);

function showError(message) {
  loading.hidden = true;
  if ($("title").textContent === "Loading…") $("title").textContent = "Oops!";
  errorBox.replaceChildren(
    Object.assign(document.createElement("p"), { textContent: message }),
    Object.assign(document.createElement("a"), { href: "./", textContent: "Back to all games" }),
  );
  errorBox.hidden = false;
}

// Unity calls this for warnings and errors raised by the build at runtime.
function unityBanner(message, type) {
  if (type === "error") {
    showError(message);
    return;
  }
  warningBox.textContent = message;
  warningBox.hidden = false;
  if (type !== "warning") setTimeout(() => (warningBox.hidden = true), 5000);
}

function showSyncStatus(status) {
  if (!status) {
    syncStatus.hidden = true;
    return;
  }
  const buttons = (status.actions || []).map((a) =>
    Object.assign(document.createElement("button"), { type: "button", className: "link-btn", textContent: a.label, onclick: a.run }),
  );
  syncStatus.replaceChildren(Object.assign(document.createElement("span"), { textContent: status.text }), ...buttons);
  syncStatus.dataset.tone = status.tone || "muted";
  syncStatus.hidden = false;
}

// Fullscreen on every browser, including iPhone Safari (which only gets "fill the screen"; see fullscreen.js).
function enableFullscreen() {
  const fs = setupFullscreen($("stage"), {
    onChange: (on) => {
      fullscreenBtn.textContent = on ? "Exit fullscreen" : "Fullscreen";
      if (!on) canvas.focus?.();
    },
  });
  fullscreenBtn.hidden = false;
  fullscreenBtn.addEventListener("click", () => fs.toggle());
  showIPhoneTip();
}

const TIP_KEY = "orion:ios-fullscreen-tip";

function showIPhoneTip() {
  if (!isIPhone() || isStandalone()) return;
  try {
    if (localStorage.getItem(TIP_KEY)) return;
  } catch {}
  const tip = $("ios-tip");
  const close = Object.assign(document.createElement("button"), { type: "button", className: "link-btn", textContent: "Got it" });
  close.addEventListener("click", () => {
    tip.hidden = true;
    try { localStorage.setItem(TIP_KEY, "1"); } catch {}
  });
  tip.replaceChildren(
    Object.assign(document.createElement("span"), {
      textContent: "On iPhone, Fullscreen fills the screen but Safari keeps its bars. For true fullscreen, tap Share, then Add to Home Screen, and open the arcade from there.",
    }),
    close,
  );
  tip.hidden = false;
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = src;
    s.onload = resolve;
    s.onerror = () => reject(new Error(`Couldn't load the game loader (${src}).`));
    document.body.append(s);
  });
}

async function start() {
  const slug = new URLSearchParams(location.search).get("game");
  if (!slug) return showError("No game selected.");

  let game;
  try {
    game = (await loadGames()).find((g) => g.slug === slug);
  } catch (err) {
    return showError(err.message);
  }
  if (!game) return showError("We couldn't find that game.");

  document.title = `${game.title} | Orion Arcade`;
  $("title").textContent = game.title;
  $("description").textContent = game.description || "";
  if (game.controls) {
    $("controls").textContent = game.controls;
    $("controls").hidden = false;
  }
  $("stage").style.setProperty("--aspect", aspectRatio(game));

  if (game.type === "html") return startHtml(game);

  const b = game.build || {};
  if (!b.loader || !b.data || !b.framework || !b.code) {
    return showError("This game's build is incomplete. Please check back soon.");
  }

  const config = {
    dataUrl: gameUrl(game, b.data),
    frameworkUrl: gameUrl(game, b.framework),
    codeUrl: gameUrl(game, b.code),
    streamingAssetsUrl: gameUrl(game, game.streamingAssets || "StreamingAssets"),
    companyName: game.company || "Orion Design",
    productName: game.title,
    productVersion: game.version || "1.0",
    showBanner: unityBanner,
  };
  if (b.symbols) config.symbolsUrl = gameUrl(game, b.symbols);
  if (b.memory) config.memoryUrl = gameUrl(game, b.memory);

  if (isMobile) {
    // Avoid huge framebuffers on high-DPI phones.
    config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 2);
    if (game.mobile === false) {
      unityBanner("This game is designed for a keyboard and may not work well on a phone or tablet.", "warning");
    }
  }

  progressLabel.textContent = "Loading your progress…";
  const saves = await prepareSaves(game.slug, { onStatus: showSyncStatus });

  try {
    await loadScript(gameUrl(game, b.loader));
    // createUnityInstance is defined globally by the Unity loader script.
    const instance = await window.createUnityInstance(canvas, config, (p) => {
      const pct = Math.round(p * 100);
      progress.style.width = `${pct}%`;
      progressLabel.textContent = pct < 100 ? `Loading… ${pct}%` : "Starting…";
    });
    loading.hidden = true;
    saves.start();
    enableFullscreen();
    canvas.focus();
  } catch (err) {
    console.error(err);
    showError(typeof err === "string" ? err : err?.message || "The game failed to start.");
  }
}

// Plain web games (no Unity) run in a sandboxed iframe pointing at the game's entry page.
function startHtml(game) {
  const frame = document.createElement("iframe");
  frame.className = "game-frame";
  frame.title = game.title;
  frame.src = gameUrl(game, game.entry || "index.html");
  frame.allow = "fullscreen; autoplay; gamepad";
  frame.setAttribute("sandbox", "allow-scripts allow-same-origin allow-pointer-lock allow-popups");
  frame.addEventListener("load", () => {
    loading.hidden = true;
    frame.focus();
  }, { once: true });
  canvas.replaceWith(frame);
  if (!game.aspect) $("stage").classList.add("stage-fill");
  enableFullscreen();
}

start();
