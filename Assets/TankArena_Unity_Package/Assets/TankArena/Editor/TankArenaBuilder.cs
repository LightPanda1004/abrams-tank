#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TankArenaBuilder
{
    private const string Root = "Assets/TankArena";

    [MenuItem("Tools/Tank Arena/Create Arena Scene")]
    public static void CreateArena()
    {
        string rawPath = Path.Combine(Application.dataPath, "TankArena/Heightmaps/Arena_Height_513x513_R16.raw");
        if (!File.Exists(rawPath)) { Debug.LogError("Tank Arena: Missing heightmap: " + rawPath); return; }
        const int resolution = 513;
        byte[] bytes = File.ReadAllBytes(rawPath);
        if (bytes.Length != resolution * resolution * 2) { Debug.LogError("Tank Arena: Unexpected heightmap size."); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var data = new TerrainData();
        data.heightmapResolution = resolution;
        data.size = new Vector3(500f, 95f, 500f);
        float[,] heights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                int offset = (y * resolution + x) * 2;
                ushort value = (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
                heights[y, x] = value / 65535f;
            }
        data.SetHeights(0, 0, heights);
        string terrainAsset = Root + "/TankArenaTerrain.asset";
        AssetDatabase.DeleteAsset(terrainAsset);
        AssetDatabase.CreateAsset(data, terrainAsset);

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Arena_Albedo.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Arena_Normal.png");
        if (albedo != null)
        {
            var layer = new TerrainLayer();
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = normal;
            // The source is an entire arena texture, so project it once over the whole terrain.
            layer.tileSize = new Vector2(500, 500);
            string layerAsset = Root + "/ArenaTerrainLayer.terrainlayer";
            AssetDatabase.DeleteAsset(layerAsset);
            AssetDatabase.CreateAsset(layer, layerAsset);
            data.terrainLayers = new[] { layer };
            var alpha = new float[data.alphamapResolution, data.alphamapResolution, 1];
            for (int y = 0; y < data.alphamapResolution; y++)
                for (int x = 0; x < data.alphamapResolution; x++) alpha[y, x, 0] = 1f;
            data.SetAlphamaps(0, 0, alpha);
        }
        var terrainGO = Terrain.CreateTerrainGameObject(data);
        terrainGO.name = "Tank Arena - Terrain";
        terrainGO.transform.position = Vector3.zero;
        var terrain = terrainGO.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 5;
        terrain.basemapDistance = 1000;
        // Terrain.CreateTerrainGameObject adds a TerrainCollider.

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(250, 350, -240);
        camera.transform.LookAt(new Vector3(250, 35, 250));
        camera.farClipPlane = 1800;

        // Useful empty transforms for placing tanks; move them to suit your gameplay.
        var spawnParent = new GameObject("Tank Spawn Points");
        CreateSpawn(spawnParent.transform, "Spawn A", data, 175, 200);
        CreateSpawn(spawnParent.transform, "Spawn B", data, 330, 310);

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "TankArena/Scenes"));
        string scenePath = Root + "/Scenes/TankArenaDemo.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = terrainGO;
        Debug.Log("Tank Arena created: " + scenePath + ". Add your own tank prefabs to the spawn points.");
    }

    private static void CreateSpawn(Transform parent, string name, TerrainData data, float x, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(x, data.GetInterpolatedHeight(x / data.size.x, z / data.size.z) + 2f, z);
    }
}
#endif
