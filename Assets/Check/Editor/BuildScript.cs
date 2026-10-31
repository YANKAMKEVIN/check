using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Check.EditorTools
{
    /// <summary>
    /// Configuration du projet et builds en ligne de commande :
    ///   Unity -batchmode -quit -projectPath . -executeMethod Check.EditorTools.BuildScript.BuildAndroid
    ///   Unity -batchmode -quit -projectPath . -executeMethod Check.EditorTools.BuildScript.BuildMac
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BundleId = "com.kevinyankam.check";

        [MenuItem("Check/Configurer le projet")]
        public static void Setup()
        {
            EnsureScene();
            ConfigurePlayer();
            ConfigureInputHandling();
            ConfigureRendering();
            ConfigureSuitFont();
            AssetDatabase.SaveAssets();
            Debug.Log("[Check] Projet configuré");
        }

        const string SuitFontPath = "Assets/Check/Game/UI/Resources/CheckSuits.ttf";

        /// <summary>
        /// Police de l'interface sur mobile : Noto Sans Symbols 2 (OFL, symboles ♠ ♥ ♦ ♣ seulement),
        /// dynamique, avec repli glyphe par glyphe sur la police système pour les lettres.
        /// </summary>
        static void ConfigureSuitFont()
        {
            if (!(AssetImporter.GetAtPath(SuitFontPath) is TrueTypeFontImporter importer)) return;
            var names = new[] { "Roboto", "Helvetica Neue", "Arial" };
            if (importer.fontTextureCase == FontTextureCase.Dynamic && importer.includeFontData &&
                importer.fontNames != null && importer.fontNames.SequenceEqual(names)) return;
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.includeFontData = true;
            importer.fontNames = names;
            importer.SaveAndReimport();
        }

        [MenuItem("Check/Build Android (APK)")]
        public static void BuildAndroid()
        {
            Setup();
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Build(BuildTarget.Android, "Builds/Android/Check.apk");
        }

        [MenuItem("Check/Build macOS")]
        public static void BuildMac()
        {
            Setup();
            // Seul le module Mac Mono est installé (pas IL2CPP) : suffisant pour le prototype.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/Check.app");
        }

        static void Build(BuildTarget target, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Check] Build {target} : {s.result}, {s.totalErrors} erreur(s), {s.totalSize / (1024 * 1024)} Mo -> {output}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>La scène est vide : CheckApp construit tout au lancement.</summary>
        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var app = new GameObject("Check");
                app.AddComponent<Check.Game.App.CheckApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Kevin Yankam";
            PlayerSettings.productName = "Check";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            // Écran de démarrage Unity : obligatoire en licence Personal, on garde le logo seul.
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
        }

        /// <summary>
        /// Active Input Handling = Input System Package (1). Sans cela, aucune entrée n'arrive au jeu.
        /// Le changement ne prend effet qu'au prochain lancement de l'éditeur.
        /// </summary>
        public static bool ConfigureInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == 1) return false;
            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[Check] Active Input Handling passé à Input System : relancer l'éditeur avant de builder.");
            return true;
        }

        /// <summary>Ajuste les assets URP existants (projet créé depuis le modèle URP) pour la DA néon.</summary>
        static void ConfigureRendering()
        {
            var assets = new System.Collections.Generic.List<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main) assets.Add(main);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q && !assets.Contains(q)) assets.Add(q);

            if (assets.Count == 0)
            {
                Debug.LogError("[Check] Aucun asset URP : créer le projet depuis le modèle URP.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            foreach (var a in assets)
            {
                a.supportsHDR = true;           // indispensable au bloom néon
                a.msaaSampleCount = 4;
                a.shadowDistance = 30f;
                a.renderScale = 1f;
                // supportsSoftShadows a un setter interne en URP 17 : on passe par la sérialisation.
                var so = new SerializedObject(a);
                var soft = so.FindProperty("m_SoftShadowsSupported");
                if (soft != null) soft.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(a);
            }
            VisualsBuildSupport.Apply();
        }
    }
}
