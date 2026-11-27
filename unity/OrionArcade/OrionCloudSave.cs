using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OrionArcade
{
    /// <summary>
    /// Save data that follows the player's Orion Arcade account when they're signed in, and stays in
    /// the browser when they aren't. Outside the arcade (the Editor, other platforms, other websites)
    /// it uses PlayerPrefs instead, so the same code works everywhere.
    ///
    /// Plain PlayerPrefs and files in Application.persistentDataPath are synced by the arcade too, so
    /// this class is optional. Use it when you want to know whether the player is signed in, or to
    /// upload progress straight away at a checkpoint with <see cref="Save"/>.
    /// </summary>
    public static class OrionCloudSave
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int OrionArcade_IsAvailable();
        [DllImport("__Internal")] static extern int OrionArcade_IsSignedIn();
        [DllImport("__Internal")] static extern string OrionArcade_PlayerName();
        [DllImport("__Internal")] static extern string OrionArcade_Load(string key);
        [DllImport("__Internal")] static extern void OrionArcade_Save(string key, string value);
        [DllImport("__Internal")] static extern void OrionArcade_Delete(string key);
        [DllImport("__Internal")] static extern void OrionArcade_Flush();
        [DllImport("__Internal")] static extern void OrionArcade_SyncFiles();

        static bool InArcade => OrionArcade_IsAvailable() == 1;
#else
        static bool InArcade => false;
        static int OrionArcade_IsSignedIn() => 0;
        static string OrionArcade_PlayerName() => "";
        static string OrionArcade_Load(string key) => null;
        static void OrionArcade_Save(string key, string value) { }
        static void OrionArcade_Delete(string key) { }
        static void OrionArcade_Flush() { }
        static void OrionArcade_SyncFiles() { }
#endif

        /// <summary>True when running on the Orion Arcade player page.</summary>
        public static bool IsInArcade => InArcade;

        /// <summary>True when the player is signed in, so progress is saved to their account.</summary>
        public static bool IsSignedIn => InArcade && OrionArcade_IsSignedIn() == 1;

        /// <summary>The signed-in player's display name, or an empty string.</summary>
        public static string PlayerName => InArcade ? OrionArcade_PlayerName() ?? "" : "";

        public static bool HasKey(string key) => InArcade ? OrionArcade_Load(key) != null : PlayerPrefs.HasKey(key);

        public static string GetString(string key, string defaultValue = "")
        {
            if (!InArcade) return PlayerPrefs.GetString(key, defaultValue);
            return OrionArcade_Load(key) ?? defaultValue;
        }

        public static void SetString(string key, string value)
        {
            if (InArcade) OrionArcade_Save(key, value ?? "");
            else PlayerPrefs.SetString(key, value ?? "");
        }

        public static int GetInt(string key, int defaultValue = 0) =>
            int.TryParse(GetString(key, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

        public static void SetInt(string key, int value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));

        public static float GetFloat(string key, float defaultValue = 0f) =>
            float.TryParse(GetString(key, null), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

        public static void SetFloat(string key, float value) => SetString(key, value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>Load an object saved with <see cref="SetJson"/> (uses JsonUtility).</summary>
        public static T GetJson<T>(string key, T defaultValue = default)
        {
            var json = GetString(key, null);
            return string.IsNullOrEmpty(json) ? defaultValue : JsonUtility.FromJson<T>(json);
        }

        public static void SetJson<T>(string key, T value) => SetString(key, JsonUtility.ToJson(value));

        public static void DeleteKey(string key)
        {
            if (InArcade) OrionArcade_Delete(key);
            else PlayerPrefs.DeleteKey(key);
        }

        /// <summary>
        /// Save now. In the arcade this uploads to the player's account straight away instead of
        /// within the next few seconds; elsewhere it calls PlayerPrefs.Save().
        /// </summary>
        public static void Save()
        {
            if (InArcade) OrionArcade_Flush();
            else PlayerPrefs.Save();
        }

        /// <summary>
        /// Call after writing your own files to Application.persistentDataPath. In a WebGL build this
        /// writes them to browser storage, where the arcade picks them up and syncs them.
        /// </summary>
        public static void SyncFiles()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OrionArcade_SyncFiles();
#endif
        }
    }
}
