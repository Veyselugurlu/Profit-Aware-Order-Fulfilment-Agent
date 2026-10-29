using System;
using System.IO;
using Fulfilment.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Fulfilment.Editor
{
    /// <summary>
    /// Builds the main scene and its supporting assets from code so the setup is reproducible, and builds the
    /// Windows player. Menu: Fulfilment → Build Main Scene / Build Windows Player.
    /// Batch: -executeMethod Fulfilment.Editor.SceneBuilder.BuildSceneAndPlayerBatch
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        private const string MaterialPath = "Assets/_Game/Art/SpriteUnlit.mat";
        private const string PanelSettingsPath = "Assets/_Game/UI/HudPanelSettings.asset";
        private const string ThemePath = "Assets/_Game/UI/RuntimeTheme.tss";
        private const string StyleSheetPath = "Assets/_Game/UI/Hud.uss";
        private const string PlayerPath = "Builds/ProfitFulfilmentAgent/ProfitFulfilmentAgent.exe";
        private const string Marker = "[SceneBuilder]";

        [MenuItem("Fulfilment/Build Main Scene")]
        public static void BuildMainScene()
        {
            // New scene first: opening a scene unloads assets nobody references, which would turn
            // references loaded earlier into dead objects that serialize as null.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material material = EnsureMaterial();
            PanelSettings panelSettings = EnsurePanelSettings();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet == null) throw new InvalidOperationException($"Missing {StyleSheetPath}");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 24f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            camGo.transform.position = new Vector3(32f, 20f, -10f);
            var cameraController = camGo.AddComponent<CameraController>();

            var simGo = new GameObject("Simulation");
            var runner = simGo.AddComponent<SimulationRunner>();
            var demo = simGo.AddComponent<DemoAutomation>();

            var map = new GameObject("Map").AddComponent<MapView>();
            var parcels = new GameObject("Parcels").AddComponent<ParcelLayer>();
            var effects = new GameObject("Effects").AddComponent<EffectsLayer>();
            var selection = new GameObject("Selection").AddComponent<SelectionController>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var hudGo = new GameObject("HUD");
            var document = hudGo.AddComponent<UIDocument>();
            // The panelSettings property setter is not persisted in edit mode; write the serialized field.
            Set(document, ("m_PanelSettings", panelSettings));
            var hud = hudGo.AddComponent<HudController>();

            Set(demo, ("runner", runner), ("selection", selection), ("map", map));
            Set(map, ("runner", runner), ("spriteMaterial", material));
            Set(parcels, ("runner", runner), ("map", map));
            Set(effects, ("runner", runner), ("map", map));
            Set(selection, ("sceneCamera", cam), ("map", map), ("runner", runner));
            Set(cameraController, ("map", map));
            Set(hud, ("runner", runner), ("selection", selection), ("cameraController", cameraController),
                ("sceneCamera", cam), ("map", map), ("styleSheet", styleSheet));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (document.panelSettings == null || hud == null)
                throw new InvalidOperationException("HUD panel settings were not saved with the scene");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"{Marker} scene saved to {ScenePath}");
        }

        [MenuItem("Fulfilment/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build {report.summary.result}, {report.summary.totalErrors} errors");
            Debug.Log($"{Marker} player built: {PlayerPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
        }

        public static void BuildSceneAndPlayerBatch()
        {
            int exitCode = 0;
            try
            {
                BuildMainScene();
                BuildWindowsPlayer();
            }
            catch (Exception e)
            {
                Debug.LogError($"{Marker} FAILED {e}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP 2D unlit sprite shader not found");
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath) ?? "Assets");
            material = new Material(shader) { name = "SpriteUnlit" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }

        private static void Set(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, UnityEngine.Object value) in values)
            {
                SerializedProperty prop = so.FindProperty(field);
                if (prop == null) throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
