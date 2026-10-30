using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Week3Lab.Editor
{
    /// <summary>
    /// Builds the Week 3 "Stay on Platform" scene for the unchanged lecture scripts, following the lecture's
    /// hierarchy (solution 02/03): SafePlatform (Plane + collider), DeathFloor (Cube + collider + DeathZone),
    /// Bot prefab (Capsule + Rigidbody with X/Z rotation frozen + Brain, Eyes child without collider at
    /// (0, 0.52, 0.40)), PopulationManager, camera and light.
    /// Menu: Fulfilment → Week 3 Lab → Build Stay On Platform Scene.
    /// </summary>
    public static class Week3LabSceneBuilder
    {
        public const string ScenePath = Fulfilment.Editor.SceneBuilder.Week3LabScenePath;
        private const string PrefabPath = "Assets/_Game/Week3Lab/Bot.prefab";
        private const string MaterialDir = "Assets/_Game/Week3Lab/Materials";

        [MenuItem("Fulfilment/Week 3 Lab/Build Stay On Platform Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material platformMat = EnsureMaterial("Platform", new Color(0.55f, 0.62f, 0.58f));
            Material deathMat = EnsureMaterial("DeathFloor", new Color(0.62f, 0.18f, 0.18f));
            Material botMat = EnsureMaterial("Bot", new Color(0.16f, 0.47f, 0.84f));
            Material eyesMat = EnsureMaterial("Eyes", new Color(0.93f, 0.63f, 0.0f));

            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 7.5f, -7.5f), Quaternion.Euler(45f, 0f, 0f));

            var light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Plane);
            platform.name = "SafePlatform";
            platform.transform.localScale = new Vector3(0.5f, 1f, 0.5f); // 5 × 5 units
            platform.GetComponent<Renderer>().sharedMaterial = platformMat;

            GameObject death = GameObject.CreatePrimitive(PrimitiveType.Cube);
            death.name = "DeathFloor";
            death.transform.position = new Vector3(0f, -3f, 0f);
            death.transform.localScale = new Vector3(30f, 0.5f, 30f);
            death.GetComponent<Renderer>().sharedMaterial = deathMat;
            death.AddComponent<DeathZone>();

            GameObject botPrefab = BuildBotPrefab(botMat, eyesMat);

            var populationGo = new GameObject("PopulationManager");
            populationGo.transform.position = new Vector3(0f, 1f, 0f);
            var population = populationGo.AddComponent<PopulationManager>();
            population.botPrefab = botPrefab;
            population.safePlatform = platform.GetComponent<Collider>();

            var runner = new GameObject("Week3LabRunner").AddComponent<Week3LabRunner>();
            runner.population = population;

            EditorUtility.SetDirty(population);
            EditorUtility.SetDirty(runner);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Fulfilment.Editor.SceneBuilder.RegisterScenes();
            Debug.Log($"[Week3Lab] scene saved to {ScenePath}");
        }

        private static GameObject BuildBotPrefab(Material botMat, Material eyesMat)
        {
            GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bot.name = "Bot";
            bot.GetComponent<Renderer>().sharedMaterial = botMat;
            var brain = bot.AddComponent<Brain>(); // adds the required Rigidbody
            var body = bot.GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            GameObject eyes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyes.name = "Eyes_ProbeOrigin";
            UnityEngine.Object.DestroyImmediate(eyes.GetComponent<Collider>()); // a collider on the probe would disturb the sensor and the body
            eyes.transform.SetParent(bot.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 0.52f, 0.40f);
            eyes.transform.localScale = Vector3.one * 0.2f;
            eyes.GetComponent<Renderer>().sharedMaterial = eyesMat;
            brain.eyes = eyes.transform;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(bot);
            return prefab;
        }

        private static Material EnsureMaterial(string name, Color colour)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader not found");
            material = new Material(shader) { color = colour };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void BuildBatch()
        {
            int exitCode = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogError("[Week3Lab] FAILED " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }
    }
}
