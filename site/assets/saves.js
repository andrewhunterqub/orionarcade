// Per-game save slots, with cloud sync for signed-in players.
//
// Unity WebGL keeps PlayerPrefs and Application.persistentDataPath in an IndexedDB database called
// "/idbfs". The folder inside it comes from the page's URL, and every game runs from play.html, so all
// games on this site would otherwise share one save folder. Before a game starts we swap its own
// files into that folder, and while it runs we copy any changes back out. Each game's copy lives in
// our own "orion-arcade" database and, for signed-in players, in their account.
//
// Games can also store key/value data directly through window.OrionArcade (see the Unity plugin in
// unity/OrionArcade). That data is kept and synced alongside the game's files.

import * as account from "./account.js";

const IDBFS = { name: "/idbfs", store: "FILE_DATA", version: 21 };
const OWN_DB = "orion-arcade";
const MAX_CLOUD_BYTES = 900_000; // Firestore documents are limited to 1 MiB.
const POLL_MS = 10_000;
const UPLOAD_DELAY_MS = 15_000;
const RETRY_MS = 60_000;
const CLOUD_TIMEOUT_MS = 8000;
const PREFER_CLOUD_KEY = (slug) => `orion:prefer-cloud:${slug}`;

// ---------------------------------------------------------------------------------------------
// IndexedDB helpers

const promised = (request) =>
  new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });

const finished = (tx) =>
  new Promise((resolve, reject) => {
    tx.oncomplete = () => resolve();
    tx.onerror = tx.onabort = () => reject(tx.error || new Error("IndexedDB transaction failed"));
  });

function openDb(name, version, upgrade) {
  return new Promise((resolve, reject) => {
    const request = version ? indexedDB.open(name, version) : indexedDB.open(name);
    request.onupgradeneeded = () => upgrade?.(request.result, request.transaction);
    request.onsuccess = () => {
      const db = request.result;
      // Never hold up Unity if it needs to upgrade the database.
      db.onversionchange = () => db.close();
      resolve(db);
    };
    request.onerror = () => reject(request.error);
  });
}

// Matches the store Emscripten (Unity's runtime) creates, in case we create the database first.
function upgradeIdbfs(db, tx) {
  const store = db.objectStoreNames.contains(IDBFS.store) ? tx.objectStore(IDBFS.store) : db.createObjectStore(IDBFS.store);
  if (!store.indexNames.contains("timestamp")) store.createIndex("timestamp", "timestamp", { unique: false });
}

/** Everything in Unity's save folder, as [path, entry] pairs. */
async function readWorking() {
  const db = await openDb(IDBFS.name);
  try {
    if (!db.objectStoreNames.contains(IDBFS.store)) return [];
    const store = db.transaction(IDBFS.store, "readonly").objectStore(IDBFS.store);
    const [keys, values] = await Promise.all([promised(store.getAllKeys()), promised(store.getAll())]);
    return keys.map((key, i) => [key, values[i]]);
  } finally {
    db.close();
  }
}

/** Replace everything in Unity's save folder. Only call this before the game starts. */
async function writeWorking(entries) {
  let db = await openDb(IDBFS.name);
  if (!db.objectStoreNames.contains(IDBFS.store)) {
    if (!entries.length) {
      db.close();
      return;
    }
    const version = Math.max(IDBFS.version, db.version + 1);
    db.close();
    db = await openDb(IDBFS.name, version, upgradeIdbfs);
  }
  try {
    const tx = db.transaction(IDBFS.store, "readwrite");
    const store = tx.objectStore(IDBFS.store);
    store.clear();
    for (const [key, value] of entries) store.put(value, key);
    await finished(tx);
  } finally {
    db.close();
  }
}

const openOwn = () =>
  openDb(OWN_DB, 1, (db) => {
    if (!db.objectStoreNames.contains("saves")) db.createObjectStore("saves");
    if (!db.objectStoreNames.contains("meta")) db.createObjectStore("meta");
  });

async function ownGet(store, key) {
  const db = await openOwn();
  try {
    return await promised(db.transaction(store, "readonly").objectStore(store).get(key));
  } finally {
    db.close();
  }
}

async function ownPut(store, key, value) {
  const db = await openOwn();
  try {
    const tx = db.transaction(store, "readwrite");
    tx.objectStore(store).put(value, key);
    await finished(tx);
  } finally {
    db.close();
  }
}

// ---------------------------------------------------------------------------------------------
// Save records: { entries, kv, updatedAt, syncedAt, sig, uid }
//   entries    Unity's files, as stored in /idbfs
//   kv         data saved through window.OrionArcade
//   updatedAt  when this device last saw a change (ms)
//   syncedAt   the updatedAt that was last uploaded to (or downloaded from) the account
//   sig        fingerprint of entries, to spot changes cheaply
//   uid        account this progress was last synced with

function signature(entries) {
  let h = 2166136261;
  const mix = (s) => {
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
  };
  for (const [key, v] of entries) {
    mix(String(key));
    mix(String(+v?.timestamp || 0));
    mix(String(v?.mode));
    mix(String(v?.contents?.byteLength ?? -1));
  }
  return `${entries.length}:${(h >>> 0).toString(36)}`;
}

const emptyRecord = (uid = null) => ({ entries: [], kv: {}, updatedAt: 0, syncedAt: 0, sig: signature([]), uid });

/** Put this game's files into Unity's save folder and return its record. */
async function swapIn(slug) {
  const [owner, working] = await Promise.all([ownGet("meta", "owner"), readWorking()]);
  const sig = signature(working);

  if (owner === undefined) {
    // First game run on this device since the arcade started managing saves: adopt what's there.
    const record = { ...emptyRecord(), entries: working, sig, updatedAt: working.length ? Date.now() : 0 };
    await ownPut("saves", slug, record);
    await ownPut("meta", "owner", slug);
    return record;
  }

  if (owner !== slug) {
    // The folder holds another game's files. Make sure its copy is current, then swap.
    const previous = await ownGet("saves", owner);
    if (!previous || previous.sig !== sig) {
      await ownPut("saves", owner, { ...(previous || emptyRecord()), entries: working, sig, updatedAt: Date.now() });
    }
    const record = (await ownGet("saves", slug)) || emptyRecord();
    await writeWorking(record.entries);
    await ownPut("saves", slug, record);
    await ownPut("meta", "owner", slug);
    return record;
  }

  // Same game as last time: pick up anything it saved after our last copy.
  const record = (await ownGet("saves", slug)) || emptyRecord();
  if (record.sig !== sig) {
    Object.assign(record, { entries: working, sig, updatedAt: Date.now() });
    await ownPut("saves", slug, record);
  }
  return record;
}

// ---------------------------------------------------------------------------------------------
// Cloud encoding: JSON (Dates and byte arrays tagged), gzipped where the browser supports it.

function toBase64(bytes) {
  let s = "";
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

function fromBase64(text) {
  const s = atob(text);
  const bytes = new Uint8Array(s.length);
  for (let i = 0; i < s.length; i++) bytes[i] = s.charCodeAt(i);
  return bytes;
}

function toPlain(value) {
  if (value instanceof Date) return { $date: value.getTime() };
  if (ArrayBuffer.isView(value)) return { $bytes: toBase64(new Uint8Array(value.buffer, value.byteOffset, value.byteLength)) };
  if (Array.isArray(value)) return value.map(toPlain);
  if (value && typeof value === "object") return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, toPlain(v)]));
  return value;
}

function fromPlain(value) {
  if (Array.isArray(value)) return value.map(fromPlain);
  if (value && typeof value === "object") {
    if ("$date" in value) return new Date(value.$date);
    if ("$bytes" in value) return fromBase64(value.$bytes);
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, fromPlain(v)]));
  }
  return value;
}

async function transform(bytes, stream) {
  return new Uint8Array(await new Response(new Blob([bytes]).stream().pipeThrough(stream)).arrayBuffer());
}

async function encode(record) {
  const json = new TextEncoder().encode(JSON.stringify({ v: 1, kv: record.kv || {}, files: toPlain(record.entries) }));
  if (typeof CompressionStream === "function") return { bytes: await transform(json, new CompressionStream("gzip")), compressed: true };
  return { bytes: json, compressed: false };
}

async function decode({ bytes, compressed }) {
  const raw = compressed ? await transform(bytes, new DecompressionStream("gzip")) : bytes;
  const data = JSON.parse(new TextDecoder().decode(raw));
  return { entries: fromPlain(data.files || []), kv: data.kv || {} };
}

function withTimeout(promise, ms) {
  return Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error("Timed out")), ms))]);
}

// ---------------------------------------------------------------------------------------------
// Session for the game being played

/**
 * Get a game's saves ready. Call before the Unity loader runs, then call start() once the game is up.
 * Never throws: if saves can't be prepared, the game still runs with whatever Unity finds.
 * onStatus(status) is called with { text, tone, actions: [{ label, run }] } or null.
 */
export async function prepareSaves(slug, { onStatus = () => {} } = {}) {
  const s = {
    slug,
    onStatus,
    record: emptyRecord(),
    localFiles: true,
    uid: null,
    conflict: false,
    uploading: false,
    tooBig: false,
    error: "",
    stopped: false,
    timer: null,
  };

  try {
    s.record = await swapIn(slug);
  } catch (err) {
    console.warn("Saves: couldn't use browser storage, so files won't be synced.", err);
    s.localFiles = false;
  }

  const user = await account.ready;
  if (user) await reconcile(s, user, true);

  installApi(s);
  account.onChange((u) => onAccountChange(s, u));
  report(s);
  return { start: () => startWatching(s) };
}

async function persist(s) {
  if (!s.localFiles) return;
  try {
    await ownPut("saves", s.slug, s.record);
  } catch (err) {
    console.warn("Saves: couldn't store a local copy.", err);
  }
}

/** Line up this device's progress with the account's. atStart: the game hasn't loaded yet. */
async function reconcile(s, user, atStart) {
  s.uid = user.uid;
  s.error = "";
  s.conflict = false;

  const preferCloud = atStart && sessionStorage.getItem(PREFER_CLOUD_KEY(s.slug)) === "1";
  if (preferCloud) sessionStorage.removeItem(PREFER_CLOUD_KEY(s.slug));

  let cloud;
  try {
    cloud = await withTimeout(account.getCloudSave(s.slug), CLOUD_TIMEOUT_MS);
  } catch (err) {
    console.warn("Saves: couldn't read account progress.", err);
    s.error = "We couldn't reach your account, so progress is being saved in this browser for now.";
    return;
  }

  const local = s.record;
  const otherAccount = Boolean(local.uid && local.uid !== user.uid);
  const useCloud = cloud && (preferCloud || otherAccount || cloud.updatedAt > local.updatedAt);

  if (useCloud || (otherAccount && !cloud)) {
    if (!atStart) {
      // The game is already running with this device's progress; let the player choose.
      s.conflict = true;
      return;
    }
    let record = emptyRecord(user.uid);
    if (cloud) {
      try {
        const decoded = await decode(cloud);
        record = { ...decoded, updatedAt: cloud.updatedAt, syncedAt: cloud.updatedAt, sig: signature(decoded.entries), uid: user.uid };
      } catch (err) {
        console.error("Saves: account progress couldn't be read.", err);
        s.error = "Your account's progress for this game couldn't be read, so this browser's progress is being used.";
        return;
      }
    }
    if (s.localFiles) await writeWorking(record.entries);
    s.record = record;
    await persist(s);
    return;
  }

  local.uid = user.uid;
  if (cloud ? local.updatedAt > cloud.updatedAt : local.updatedAt > 0) {
    // This browser has progress the account doesn't.
    local.syncedAt = 0;
    upload(s);
  } else {
    local.syncedAt = local.updatedAt;
    await persist(s);
  }
}

async function upload(s) {
  clearTimeout(s.timer);
  const rec = s.record;
  if (!s.uid || s.conflict || s.stopped || rec.syncedAt >= rec.updatedAt) return;
  if (s.uploading) {
    s.timer = setTimeout(() => upload(s), UPLOAD_DELAY_MS);
    return;
  }
  s.uploading = true;
  report(s);
  const at = rec.updatedAt;
  const uid = s.uid;
  try {
    const encoded = await encode(rec);
    s.tooBig = encoded.bytes.length > MAX_CLOUD_BYTES;
    if (!s.tooBig) {
      await account.putCloudSave(s.slug, { ...encoded, updatedAt: at });
      if (s.uid === uid) {
        rec.syncedAt = at;
        rec.uid = uid;
      }
      s.error = "";
      await persist(s);
    }
  } catch (err) {
    console.warn("Saves: upload failed.", err);
    s.error = "Your latest progress couldn't be saved to your account yet. We'll keep trying.";
    s.timer = setTimeout(() => upload(s), RETRY_MS);
  } finally {
    s.uploading = false;
    if (!s.error && !s.tooBig && rec.updatedAt > rec.syncedAt) scheduleUpload(s);
    report(s);
  }
}

function scheduleUpload(s, delay = UPLOAD_DELAY_MS) {
  if (!s.uid || s.conflict || s.tooBig) return;
  clearTimeout(s.timer);
  s.timer = setTimeout(() => upload(s), delay);
}

function changed(s) {
  s.record.updatedAt = Date.now();
  persist(s);
  scheduleUpload(s);
  report(s);
}

async function capture(s) {
  if (!s.localFiles || s.stopped) return;
  const working = await readWorking();
  const sig = signature(working);
  if (sig === s.record.sig || s.stopped) return;
  Object.assign(s.record, { entries: working, sig });
  changed(s);
}

function startWatching(s) {
  const check = () => capture(s).catch((err) => console.warn("Saves: couldn't check for changes.", err));
  setInterval(check, POLL_MS);
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") capture(s).then(() => upload(s), () => upload(s));
  });
  window.addEventListener("pagehide", check);
}

function onAccountChange(s, user) {
  clearTimeout(s.timer);
  if (user) {
    reconcile(s, user, false).finally(() => report(s));
  } else {
    Object.assign(s, { uid: null, conflict: false, error: "", tooBig: false });
    report(s);
  }
}

// Player choices when the account's progress differs from what's running.
function loadAccountProgress(s) {
  s.stopped = true;
  clearTimeout(s.timer);
  sessionStorage.setItem(PREFER_CLOUD_KEY(s.slug), "1");
  location.reload();
}

function keepDeviceProgress(s) {
  s.conflict = false;
  s.record.uid = s.uid;
  s.record.updatedAt = Date.now();
  s.record.syncedAt = 0;
  persist(s);
  upload(s);
}

// ---------------------------------------------------------------------------------------------
// Status line under the game

function report(s) {
  s.onStatus(status(s));
}

function status(s) {
  if (!account.enabled) return null;
  const user = account.currentUser();
  if (!user) {
    return {
      text: "Your progress is saved in this browser.",
      tone: "muted",
      actions: [{ label: "Sign in to save it to your account", run: () => account.openSignIn() }],
    };
  }
  if (s.conflict) {
    return {
      text: "Your account has different progress for this game than this browser.",
      tone: "warn",
      actions: [
        { label: "Load my account's progress", run: () => loadAccountProgress(s) },
        { label: "Keep this browser's progress", run: () => keepDeviceProgress(s) },
      ],
    };
  }
  if (s.tooBig) return { text: "This game's save data is too large to store in your account, so it's kept in this browser.", tone: "warn" };
  if (s.error) return { text: s.error, tone: "warn" };
  if (s.uploading) return { text: "Saving progress to your account…", tone: "muted" };
  if (s.record.updatedAt > s.record.syncedAt) return { text: "New progress will be saved to your account shortly.", tone: "muted" };
  return { text: "Your progress is saved to your account.", tone: "ok" };
}

// ---------------------------------------------------------------------------------------------
// window.OrionArcade: lets games store key/value data that follows the player's account.

function installApi(s) {
  window.OrionArcade = Object.freeze({
    version: 1,
    game: s.slug,
    isSignedIn: () => Boolean(account.currentUser()),
    playerName: () => account.currentUser()?.displayName || "",
    load: (key) => {
      const value = s.record.kv[String(key)];
      return value === undefined ? null : value;
    },
    save: (key, value) => {
      s.record.kv = { ...s.record.kv, [String(key)]: String(value) };
      changed(s);
    },
    remove: (key) => {
      if (!(String(key) in s.record.kv)) return;
      const { [String(key)]: _removed, ...rest } = s.record.kv;
      s.record.kv = rest;
      changed(s);
    },
    keys: () => Object.keys(s.record.kv),
    /** Upload now instead of waiting (e.g. at a checkpoint). */
    flush: () => upload(s),
  });
}
