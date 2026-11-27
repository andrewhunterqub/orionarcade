// Fullscreen that works everywhere, including iPhone Safari.
//
// - Desktop, Android and iPad: the Fullscreen API (prefixed as webkitRequestFullscreen on older iPad Safari).
// - iPhone Safari has no Fullscreen API for anything but video, so the game fills the screen instead
//   ("pseudo-fullscreen"): the stage is pinned over the whole page and the header is hidden. Safari's own
//   bars stay, so for true fullscreen on iPhone the player can Add to Home Screen; the arcade's web app
//   manifest then opens it with no browser chrome at all.

const doc = document;

const nativeElement = () => doc.fullscreenElement || doc.webkitFullscreenElement || null;

function nativeRequest(el) {
  if (el.requestFullscreen) return el.requestFullscreen({ navigationUI: "hide" });
  if (el.webkitRequestFullscreen) return el.webkitRequestFullscreen();
  return null;
}

function nativeExit() {
  if (doc.exitFullscreen) return doc.exitFullscreen();
  if (doc.webkitExitFullscreen) return doc.webkitExitFullscreen();
  return null;
}

export const supportsNative = () => Boolean(doc.fullscreenEnabled || doc.webkitFullscreenEnabled);

/** Running from the home screen (iOS) or as an installed app: already free of browser chrome. */
export const isStandalone = () => window.matchMedia("(display-mode: standalone), (display-mode: fullscreen)").matches || navigator.standalone === true;

/** iPhone or iPod (iPads report as Macs with touch, and have a Fullscreen API). */
export const isIPhone = () => /iPhone|iPod/.test(navigator.userAgent);

/**
 * Wire up a fullscreen button for a stage element. Returns { enter, exit, toggle, active }.
 * onChange(active) is called whenever fullscreen starts or stops.
 */
export function setupFullscreen(stage, { onChange = () => {} } = {}) {
  let pseudo = false;

  const exitBtn = Object.assign(doc.createElement("button"), {
    type: "button",
    className: "fs-exit",
    textContent: "×",
    title: "Exit fullscreen",
  });
  exitBtn.setAttribute("aria-label", "Exit fullscreen");
  exitBtn.addEventListener("click", () => exit());
  stage.append(exitBtn);

  const active = () => pseudo || nativeElement() === stage;

  function setPseudo(on) {
    pseudo = on;
    stage.classList.toggle("pseudo-fullscreen", on);
    doc.documentElement.classList.toggle("pseudo-fullscreen-page", on);
    if (on) window.scrollTo(0, 0);
    // Let the game re-measure its canvas.
    window.dispatchEvent(new Event("resize"));
    onChange(on);
  }

  async function enter() {
    if (active()) return;
    if (supportsNative()) {
      try {
        const request = nativeRequest(stage);
        if (request !== null) {
          await request; // a promise on modern browsers, undefined on older Safari
          if (await becameFullscreen()) return;
        }
      } catch (err) {
        console.warn("Fullscreen API refused, filling the screen instead.", err);
      }
    }
    setPseudo(true);
  }

  // Some browsers report support but never actually go fullscreen; give it half a second.
  function becameFullscreen() {
    if (nativeElement() === stage) return Promise.resolve(true);
    return new Promise((resolve) => {
      const done = () => { clearTimeout(timer); doc.removeEventListener("fullscreenchange", done); doc.removeEventListener("webkitfullscreenchange", done); resolve(nativeElement() === stage); };
      const timer = setTimeout(done, 500);
      doc.addEventListener("fullscreenchange", done);
      doc.addEventListener("webkitfullscreenchange", done);
    });
  }

  async function exit() {
    if (pseudo) setPseudo(false);
    else if (nativeElement()) await nativeExit();
  }

  const toggle = () => (active() ? exit() : enter());

  const onNativeChange = () => onChange(nativeElement() === stage);
  doc.addEventListener("fullscreenchange", onNativeChange);
  doc.addEventListener("webkitfullscreenchange", onNativeChange);
  doc.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && pseudo) setPseudo(false);
  });

  return { enter, exit, toggle, active };
}
