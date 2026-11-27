// Optional sign-in (Firebase Authentication) and per-account game saves (Cloud Firestore).
//
// Nothing here is needed to play: when firebase-config.js has no config, or Firebase can't be loaded,
// players simply stay signed out and progress is kept in their browser.

import { firebaseConfig, signInMethods } from "./firebase-config.js";

const SDK = "https://www.gstatic.com/firebasejs/10.14.1";
const EMAIL_KEY = "orion:email-for-sign-in";
const READY_TIMEOUT_MS = 6000;

export const enabled = Boolean(firebaseConfig?.apiKey && firebaseConfig?.projectId);

let fb = null; // { auth, db, A: auth module, F: firestore module }
let user = null;
const listeners = new Set();

let resolveReady;
/** Resolves with the signed-in user (or null) once the initial sign-in state is known. */
export const ready = new Promise((resolve) => (resolveReady = resolve));

export const currentUser = () => user;

/** Call fn(user) whenever the player signs in or out. Returns an unsubscribe function. */
export function onChange(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

const PROVIDERS = {
  google: { label: "Google", make: (A) => withParams(new A.GoogleAuthProvider(), { prompt: "select_account" }) },
  facebook: { label: "Facebook", make: (A) => new A.FacebookAuthProvider() },
  microsoft: { label: "Microsoft", make: (A) => withParams(new A.OAuthProvider("microsoft.com"), { prompt: "select_account" }) },
  github: { label: "GitHub", make: (A) => new A.GithubAuthProvider() },
  apple: { label: "Apple", make: (A) => withScopes(new A.OAuthProvider("apple.com"), ["email", "name"]) },
};
const PROVIDER_IDS = {
  "google.com": "google",
  "facebook.com": "facebook",
  "microsoft.com": "microsoft",
  "github.com": "github",
  "apple.com": "apple",
};

function withParams(provider, params) {
  provider.setCustomParameters(params);
  return provider;
}
function withScopes(provider, scopes) {
  for (const s of scopes) provider.addScope(s);
  return provider;
}

function el(tag, props = {}, children = []) {
  const node = Object.assign(document.createElement(tag), props);
  for (const child of [].concat(children)) if (child) node.append(child);
  return node;
}

// ---------------------------------------------------------------------------------------------
// Firebase setup

async function init() {
  const [appMod, A, F] = await Promise.all([
    import(`${SDK}/firebase-app.js`),
    import(`${SDK}/firebase-auth.js`),
    import(`${SDK}/firebase-firestore-lite.js`),
  ]);
  const app = appMod.initializeApp(firebaseConfig);
  fb = { auth: A.getAuth(app), db: F.getFirestore(app), A, F };

  await completeEmailLink().catch((err) => {
    console.error(err);
    openSignIn(errorMessage(err));
  });

  A.onAuthStateChanged(fb.auth, (u) => {
    const changed = (u?.uid || null) !== (user?.uid || null);
    user = u;
    render();
    resolveReady(user);
    if (changed) for (const fn of listeners) fn(user);
  });
}

if (enabled) {
  init().catch((err) => {
    console.error("Sign-in is unavailable:", err);
    fb = null;
    render();
    resolveReady(null);
  });
  setTimeout(() => resolveReady(user), READY_TIMEOUT_MS);
} else {
  resolveReady(null);
}

async function completeEmailLink() {
  const { auth, A } = fb;
  if (!A.isSignInWithEmailLink(auth, location.href)) return;
  let email = localStorage.getItem(EMAIL_KEY);
  if (!email) email = window.prompt("To finish signing in, please confirm your email address.");
  if (email) {
    await A.signInWithEmailLink(auth, email.trim(), location.href);
    localStorage.removeItem(EMAIL_KEY);
  }
  // Remove the one-time code from the address bar.
  const url = new URL(location.href);
  for (const key of ["apiKey", "oobCode", "mode", "lang", "continueUrl", "tenantId"]) url.searchParams.delete(key);
  history.replaceState(null, "", url);
}

// ---------------------------------------------------------------------------------------------
// Cloud saves: users/{uid}/games/{slug}

function saveDoc(slug) {
  if (!fb || !user) throw new Error("Not signed in");
  return fb.F.doc(fb.db, "users", user.uid, "games", slug);
}

/** The player's saved progress for a game, or null if there isn't any. */
export async function getCloudSave(slug) {
  const snap = await fb.F.getDoc(saveDoc(slug));
  if (!snap.exists()) return null;
  const d = snap.data();
  return { bytes: d.save.toUint8Array(), compressed: Boolean(d.compressed), updatedAt: Number(d.updatedAt) || 0 };
}

export async function putCloudSave(slug, { bytes, compressed, updatedAt }) {
  const { F } = fb;
  await F.setDoc(saveDoc(slug), {
    save: F.Bytes.fromUint8Array(bytes),
    compressed,
    updatedAt,
    format: 1,
    savedAt: F.serverTimestamp(),
  });
}

async function deleteAllCloudSaves() {
  const { F, db } = fb;
  const snap = await F.getDocs(F.collection(db, "users", user.uid, "games"));
  await Promise.all(snap.docs.map((d) => F.deleteDoc(d.ref)));
}

// ---------------------------------------------------------------------------------------------
// Sign-in actions

function errorMessage(err) {
  switch (err?.code) {
    case "auth/popup-blocked":
      return "Your browser blocked the sign-in window. Allow pop-ups for this site and try again.";
    case "auth/account-exists-with-different-credential":
    case "auth/email-already-in-use":
      return "You've already signed in with this email using a different option. Please use that one.";
    case "auth/unauthorized-domain":
      return "Sign-in isn't set up for this web address yet.";
    case "auth/operation-not-allowed":
      return "That sign-in option isn't available yet.";
    case "auth/network-request-failed":
      return "We couldn't connect. Check your internet connection and try again.";
    case "auth/invalid-email":
      return "That email address doesn't look right.";
    case "auth/invalid-action-code":
    case "auth/expired-action-code":
      return "That sign-in link has expired or was already used. Please request a new one.";
    case "auth/too-many-requests":
      return "Too many attempts. Please wait a moment and try again.";
    default:
      return "Sign-in didn't work. Please try again.";
  }
}

const QUIET_ERRORS = new Set(["auth/popup-closed-by-user", "auth/cancelled-popup-request", "auth/user-cancelled"]);

async function signInWith(key) {
  const { auth, A } = fb;
  setDialogMessage("");
  try {
    await A.signInWithPopup(auth, PROVIDERS[key].make(A));
    dialog?.close();
  } catch (err) {
    if (!QUIET_ERRORS.has(err?.code)) {
      console.error(err);
      setDialogMessage(errorMessage(err), true);
    }
  }
}

async function sendEmailLink(email) {
  const { auth, A } = fb;
  setDialogMessage("Sending…");
  try {
    await A.sendSignInLinkToEmail(auth, email, { url: location.href, handleCodeInApp: true });
    localStorage.setItem(EMAIL_KEY, email);
    setDialogMessage(`We've sent a sign-in link to ${email}. Open it on this device to finish signing in.`);
  } catch (err) {
    console.error(err);
    setDialogMessage(errorMessage(err), true);
  }
}

async function signOutNow() {
  await fb.A.signOut(fb.auth);
}

async function deleteAccount() {
  const ok = window.confirm(
    "Delete your Orion Arcade account and all progress saved to it?\n\n" +
      "Progress stored in this browser isn't affected. This can't be undone.",
  );
  if (!ok) return;
  const { A } = fb;
  const current = user;
  try {
    await deleteAllCloudSaves();
    try {
      await A.deleteUser(current);
    } catch (err) {
      if (err?.code !== "auth/requires-recent-login") throw err;
      const key = PROVIDER_IDS[current.providerData[0]?.providerId];
      if (!key) {
        window.alert("For your security, please sign out, sign in again, and then delete your account.");
        return;
      }
      await A.reauthenticateWithPopup(current, PROVIDERS[key].make(A));
      await A.deleteUser(current);
    }
    window.alert("Your account and saved progress have been deleted.");
  } catch (err) {
    console.error(err);
    if (!QUIET_ERRORS.has(err?.code)) window.alert("We couldn't delete your account. Please try again.");
  }
}

// ---------------------------------------------------------------------------------------------
// UI: a "Sign in" button or account menu in the header (#account), and a sign-in dialog.

let dialog = null;
let dialogMessage = null;

function setDialogMessage(text, isError = false) {
  if (!dialogMessage) return;
  dialogMessage.textContent = text;
  dialogMessage.classList.toggle("is-error", isError);
  dialogMessage.hidden = !text;
}

function buildDialog() {
  const buttons = signInMethods
    .filter((key) => PROVIDERS[key])
    .map((key) =>
      el("button", {
        type: "button",
        className: `provider provider-${key}`,
        textContent: `Continue with ${PROVIDERS[key].label}`,
        onclick: () => signInWith(key),
      }),
    );

  let emailForm = null;
  if (signInMethods.includes("email")) {
    const input = el("input", { type: "email", required: true, placeholder: "you@example.com", autocomplete: "email" });
    input.setAttribute("aria-label", "Email address");
    emailForm = el("form", { className: "email-form" }, [
      input,
      el("button", { type: "submit", className: "provider", textContent: "Email me a sign-in link" }),
    ]);
    emailForm.addEventListener("submit", (e) => {
      e.preventDefault();
      sendEmailLink(input.value.trim());
    });
  }

  dialogMessage = el("p", { className: "signin-message", hidden: true });
  dialogMessage.setAttribute("role", "status");

  const close = el("button", { type: "button", className: "dialog-close", textContent: "×", onclick: () => dialog.close() });
  close.setAttribute("aria-label", "Close");

  const privacy = el("p", { className: "signin-fine" }, [
    "Signing in is optional. ",
    el("a", { href: "privacy.html", textContent: "Privacy" }),
  ]);

  dialog = el("dialog", { className: "signin-dialog" }, [
    close,
    el("h2", { textContent: "Sign in" }),
    el("p", {
      className: "signin-intro",
      textContent: "Save your game progress to your account and pick up where you left off on any device.",
    }),
    el("div", { className: "providers" }, buttons),
    emailForm && buttons.length ? el("div", { className: "divider", textContent: "or" }) : null,
    emailForm,
    dialogMessage,
    privacy,
  ]);
  dialog.setAttribute("aria-label", "Sign in");
  // Clicking the backdrop closes the dialog.
  dialog.addEventListener("click", (e) => {
    if (e.target === dialog) dialog.close();
  });
  document.body.append(dialog);
}

/** Open the sign-in dialog, optionally with a message (e.g. an error). */
export function openSignIn(message = "") {
  if (!enabled || !fb) return;
  if (!dialog) buildDialog();
  setDialogMessage(message, Boolean(message));
  if (!dialog.open) dialog.showModal();
}

function avatar(u) {
  const initial = (u.displayName || u.email || "?").trim().charAt(0).toUpperCase();
  const fallback = el("span", { className: "avatar", textContent: initial });
  if (!u.photoURL) return fallback;
  const img = el("img", { className: "avatar", src: u.photoURL, alt: "", referrerPolicy: "no-referrer" });
  img.addEventListener("error", () => img.replaceWith(fallback), { once: true });
  return img;
}

function render() {
  const slot = document.getElementById("account");
  if (!slot) return;
  if (!enabled || !fb) {
    slot.hidden = true;
    return;
  }
  slot.hidden = false;

  if (!user) {
    slot.replaceChildren(el("button", { type: "button", className: "btn", textContent: "Sign in", onclick: () => openSignIn() }));
    return;
  }

  const name = user.displayName || user.email || "Your account";
  const menu = el("details", { className: "account-menu" }, [
    el("summary", {}, [avatar(user), el("span", { className: "account-name", textContent: name.split(" ")[0] })]),
    el("div", { className: "menu" }, [
      el("p", { className: "menu-who" }, [el("strong", { textContent: user.displayName || "Signed in" }), user.email ? el("span", { textContent: user.email }) : null]),
      el("p", { className: "menu-note", textContent: "Your game progress is saved to this account." }),
      el("button", { type: "button", textContent: "Sign out", onclick: signOutNow }),
      el("button", { type: "button", className: "danger", textContent: "Delete account", onclick: deleteAccount }),
    ]),
  ]);
  slot.replaceChildren(menu);
}

// Close the account menu when clicking elsewhere.
document.addEventListener("click", (e) => {
  for (const m of document.querySelectorAll(".account-menu[open]")) if (!m.contains(e.target)) m.open = false;
});
