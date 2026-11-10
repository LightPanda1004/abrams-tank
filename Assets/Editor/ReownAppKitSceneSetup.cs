using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReownAppKitSceneSetup
{
    private const string DemoScenePath =
        "Assets/Tiny Teacup Studio/Military Base Pack/Demo/DemoScene.unity";

    [MenuItem("Abrams/Blockchain/Assign Reown AppKit Prefab")]
    public static void AssignToDemoScene()
    {
        if (!Application.isBatchMode &&
            EditorSceneManager.GetActiveScene().isDirty &&
            !EditorUtility.DisplayDialog(
                "Assign Reown AppKit",
                "The active scene has unsaved changes. Continue and open the demo scene?",
                "Continue",
                "Cancel"))
            return;

        GameObject prefab = FindAppKitPrefab();
        if (prefab == null)
            throw new InvalidOperationException(
                "Reown AppKit prefab was not found. Resolve the package first."
            );

        var scene = EditorSceneManager.OpenScene(
            DemoScenePath,
            OpenSceneMode.Single
        );
        GameDirector director =
            UnityEngine.Object.FindObjectOfType<GameDirector>(true);
        if (director == null)
            throw new InvalidOperationException(
                "GameDirector was not found in the demo scene."
            );

        var serializedDirector = new SerializedObject(director);
        SerializedProperty prefabProperty =
            serializedDirector.FindProperty("reownAppKitPrefab");
        if (prefabProperty == null)
            throw new InvalidOperationException(
                "GameDirector does not expose its Reown prefab field."
            );

        prefabProperty.objectReferenceValue = prefab;
        serializedDirector.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException(
                "The demo scene could not be saved."
            );

        Debug.Log("Assigned the Reown AppKit prefab to the demo scene.");
    }

    private static GameObject FindAppKitPrefab()
    {
        string[] prefabGuids = AssetDatabase.FindAssets(
            "Reown AppKit t:Prefab",
            new[] { "Packages" }
        );
        return prefabGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(
                "/Reown AppKit.prefab",
                StringComparison.Ordinal
            ))
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .FirstOrDefault(prefab => prefab != null);
    }
}
