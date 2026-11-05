using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TankValley.Editor
{
    /// <summary>Creates the native Terrain asset on first open; all other geometry is already saved.</summary>
    [InitializeOnLoad]
    public static class ValleyTerrainInstaller
    {
        private const string Root = "Assets/TankValley";
        private const string MapScene = Root + "/Scenes/TankValley.unity";
        private const string Generated = Root + "/Generated";
        private const string TerrainPath = Generated + "/ValleyTerrain.asset";
        private const int Resolution = 1025;
        private static bool installing;

        static ValleyTerrainInstaller()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += CheckOpenScenes;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == MapScene) EditorApplication.delayCall += CheckOpenScenes;
        }

        private static void CheckOpenScenes()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CheckOpenScenes;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || installing) return;
            Scene scene = SceneManager.GetSceneByPath(MapScene);
            if (scene.IsValid() && scene.isLoaded) Install(scene);
        }

        [MenuItem("Tools/Tank Valley/Open Map")]
        public static void OpenMap()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(MapScene);
            Install(scene);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(384, 25, 384), Quaternion.Euler(55, 0, 0), 650);
        }

        [MenuItem("Tools/Tank Valley/Finish Terrain Setup")]
        public static void FinishSetup()
        {
            Scene scene = SceneManager.GetSceneByPath(MapScene);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                OpenMap();
                return;
            }
            Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (installing) return;
            installing = true;
            try
            {
                bool wasDirty = scene.isDirty;
                GameObject map = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("TANK VALLEY"));
                if (map == null) return;
                AdaptMaterials();
                if (map.GetComponentInChildren<Terrain>(true) != null) return;
                EditorUtility.DisplayProgressBar("Tank Valley", "Applying the supplied heightmap to Unity Terrain...", .4f);
                if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Root, "Generated");

                TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
                if (data == null)
                {
                    TextAsset raw = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Heightmaps/Valley_1025x1025_u16le.raw.bytes");
                    if (raw == null || raw.bytes.Length != Resolution * Resolution * 2)
                        throw new InvalidDataException("The 1025 x 1025 16-bit heightmap is missing or has the wrong size.");
                    byte[] bytes = raw.bytes;
                    float[,] heights = new float[Resolution, Resolution];
                    for (int z = 0; z < Resolution; ++z)
                        for (int x = 0; x < Resolution; ++x)
                        {
                            int i = (z * Resolution + x) * 2;
                            heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
                        }
                    data = new TerrainData { name = "ValleyTerrain", heightmapResolution = Resolution, size = new Vector3(768, 128, 768), alphamapResolution = 512, baseMapResolution = 1024 };
                    data.SetHeights(0, 0, heights);
                    TerrainLayer layer = new TerrainLayer
                    {
                        name = "Valley Ground - grass, roads and rock",
                        diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Valley_Ground_Albedo.png"),
                        tileSize = new Vector2(768, 768), tileOffset = Vector2.zero, metallic = 0, smoothness = .1f
                    };
                    AssetDatabase.CreateAsset(layer, Generated + "/ValleyGround.terrainlayer");
                    data.terrainLayers = new[] { layer };
                    float[,,] alpha = new float[512, 512, 1];
                    for (int z = 0; z < 512; ++z)
                        for (int x = 0; x < 512; ++x) alpha[z, x, 0] = 1f;
                    data.SetAlphamaps(0, 0, alpha);
                    AssetDatabase.CreateAsset(data, TerrainPath);
                }

                GameObject ground = Terrain.CreateTerrainGameObject(data);
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.name = "Valley Terrain - heightmap applied";
                ground.transform.SetParent(map.transform, false);
                ground.transform.localPosition = Vector3.zero;
                Terrain terrain = ground.GetComponent<Terrain>();
                terrain.heightmapPixelError = 4;
                terrain.basemapDistance = 1800;
                terrain.drawInstanced = true;
                string terrainShader = GraphicsSettings.currentRenderPipeline == null ? "Nature/Terrain/Standard" :
                    GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("HDRender") ? "HDRP/TerrainLit" : "Universal Render Pipeline/Terrain/Lit";
                Shader shader = Shader.Find(terrainShader);
                if (shader != null)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/TerrainSurface.mat");
                    if (material == null)
                    {
                        material = new Material(shader) { name = "Valley Terrain Surface" };
                        AssetDatabase.CreateAsset(material, Generated + "/TerrainSurface.mat");
                    }
                    terrain.materialTemplate = material;
                }
                Transform fallback = map.transform.Find("Heightmap Terrain - mesh fallback");
                if (fallback != null) fallback.gameObject.SetActive(false);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                // Save only this supplied scene, and never auto-save pre-existing user edits.
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
                Debug.Log("Tank Valley: native Terrain created from the included heightmap. Buildings, forests, cover and colliders are ready.");
            }
            catch (Exception error)
            {
                Debug.LogError("Tank Valley terrain setup: " + error + "\nThe saved terrain mesh remains available. Retry Tools > Tank Valley > Finish Terrain Setup.");
            }
            finally
            {
                installing = false;
                EditorUtility.ClearProgressBar();
            }
        }

        private static void AdaptMaterials()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            string pipelineName = pipeline == null ? "BuiltIn" : pipeline.GetType().Name;
            string sessionKey = "TankValley.Materials." + pipelineName;
            if (SessionState.GetBool(sessionKey, false)) return;
            string shaderName = pipeline == null ? "Standard" : pipelineName.Contains("HDRender") ? "HDRP/Lit" : "Universal Render Pipeline/Lit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) return;
            foreach (string id in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));
                if (mat == null || mat.shader == shader) continue;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : .1f;
                mat.shader = shader;
                foreach (string key in new[] { "_Color", "_BaseColor" }) if (mat.HasProperty(key)) mat.SetColor(key, color);
                foreach (string key in new[] { "_MainTex", "_BaseMap", "_BaseColorMap" }) if (mat.HasProperty(key)) mat.SetTexture(key, tex);
                foreach (string key in new[] { "_Glossiness", "_Smoothness" }) if (mat.HasProperty(key)) mat.SetFloat(key, gloss);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            SessionState.SetBool(sessionKey, true);
        }

        [MenuItem("Tools/Tank Valley/Validate Open Map")]
        public static void ValidateOpenMap()
        {
            Scene scene = SceneManager.GetSceneByPath(MapScene);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the Tank Valley scene first.");
            int meshes = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) throw new InvalidOperationException("Missing mesh: " + filter.name);
                    ++meshes;
                }
            Terrain t = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>()).FirstOrDefault();
            if (t == null || t.terrainData == null || t.terrainData.heightmapResolution != Resolution)
                throw new InvalidOperationException("Native terrain setup has not finished.");
            if (scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>()).Any(r => r.sharedMaterials.Any(m => m == null)))
                throw new InvalidOperationException("A renderer is missing a material.");
            Debug.Log("Tank Valley validation passed: " + meshes + " mesh instances, native Terrain and materials found.");
        }
    }
}
