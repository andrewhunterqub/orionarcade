using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

/// <summary>
/// Command-line build for LOAB.
///   Unity.exe -batchmode -quit -projectPath projects/LOAB -executeMethod LoabBuild.BuildWebGL -logFile build.log
/// Output goes to site/games/loab (relative to the repo root).
/// </summary>
public static class LoabBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string LitMat = "Assets/Resources/Materials/Lit.mat";
    const string GlowMat = "Assets/Resources/Materials/Glow.mat";
    const string OutDir = "../../site/games/loab";

    [MenuItem("LOAB/Setup Assets")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Resources/Materials");
        Directory.CreateDirectory("Assets/Scenes");

        if (AssetDatabase.LoadAssetAtPath<Material>(LitMat) == null)
        {
            var m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Glossiness", 0.25f);
            AssetDatabase.CreateAsset(m, LitMat);
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(GlowMat) == null)
        {
            var m = new Material(Shader.Find("Standard"));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(m, GlowMat);
        }

        const string PanelPath = "Assets/Resources/UI/PanelSettings.asset";
        if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) == null)
        {
            AssetDatabase.ImportAsset("Assets/Resources/UI/LoabTheme.tss");
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/UI/LoabTheme.tss");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1366, 800);
            AssetDatabase.CreateAsset(ps, PanelPath);
        }

        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        const string TiltMat = "Assets/Resources/Materials/TiltShift.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(TiltMat) == null)
        {
            AssetDatabase.ImportAsset("Assets/Shaders/TiltShift.shader");
            var sh = Shader.Find("Hidden/LoabTiltShift");
            if (sh != null) AssetDatabase.CreateAsset(new Material(sh), TiltMat);
            else Debug.LogError("Tilt-shift shader not found");
        }
        var litAsset = AssetDatabase.LoadAssetAtPath<Material>(LitMat);
        if (litAsset != null && litAsset.GetFloat("_Glossiness") > 0.13f) { litAsset.SetFloat("_Glossiness", 0.12f); EditorUtility.SetDirty(litAsset); }

        foreach (var path in new[] { LitMat, GlowMat })
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !mat.enableInstancing) { mat.enableInstancing = true; EditorUtility.SetDirty(mat); }
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "Orion Design";
        PlayerSettings.productName = "Let's Open A Bar!";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
        PlayerSettings.SplashScreen.show = false;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("LOAB/Build WebGL")]
    public static void BuildWebGL()
    {
        Setup();
        if (Directory.Exists(OutDir + "/Build")) Directory.Delete(OutDir + "/Build", true);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"LOAB build result: {report.summary.result}, size {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
        // Unity's template page isn't used by the arcade (it has its own player page).
        foreach (var junk in new[] { OutDir + "/index.html" })
            if (File.Exists(junk)) File.Delete(junk);
        if (Directory.Exists(OutDir + "/TemplateData")) Directory.Delete(OutDir + "/TemplateData", true);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
