// Bridge between Unity and the Orion Arcade player page (window.OrionArcade, see site/assets/saves.js).
// Every function is safe to call when the game isn't running inside the arcade.
mergeInto(LibraryManager.library, {
  OrionArcade_IsAvailable: function () {
    return typeof window !== "undefined" && window.OrionArcade ? 1 : 0;
  },

  OrionArcade_IsSignedIn: function () {
    return window.OrionArcade && window.OrionArcade.isSignedIn() ? 1 : 0;
  },

  OrionArcade_PlayerName: function () {
    var name = (window.OrionArcade && window.OrionArcade.playerName()) || "";
    var size = lengthBytesUTF8(name) + 1;
    var buffer = _malloc(size);
    stringToUTF8(name, buffer, size);
    return buffer;
  },

  OrionArcade_Load: function (key) {
    if (!window.OrionArcade) return 0;
    var value = window.OrionArcade.load(UTF8ToString(key));
    if (value === null || value === undefined) return 0;
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  OrionArcade_Save: function (key, value) {
    if (window.OrionArcade) window.OrionArcade.save(UTF8ToString(key), UTF8ToString(value));
  },

  OrionArcade_Delete: function (key) {
    if (window.OrionArcade) window.OrionArcade.remove(UTF8ToString(key));
  },

  OrionArcade_Flush: function () {
    if (window.OrionArcade) window.OrionArcade.flush();
  },

  // Write files in Application.persistentDataPath to browser storage so the arcade can pick them up.
  OrionArcade_SyncFiles: function () {
    FS.syncfs(false, function (err) {
      if (err) console.warn("OrionArcade: couldn't save files", err);
    });
  },
});
