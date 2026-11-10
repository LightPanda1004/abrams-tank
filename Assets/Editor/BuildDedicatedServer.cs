using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildDedicatedServer
{
    private const string DemoScenePath =
        "Assets/Tiny Teacup Studio/Military Base Pack/Demo/DemoScene.unity";
    private const string OutputPath =
        "server/game-server/AbramsServer.x86_64";

    [MenuItem("Abrams/Build/Linux Dedicated Game Server")]
    public static void BuildLinuxServer()
    {
        if (!File.Exists(DemoScenePath))
            throw new FileNotFoundException(
                "The playable DemoScene is missing.",
                DemoScenePath
            );

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScenePath =
            EditorSceneManager.GetActiveScene().path;
        InsecureHttpOption previousInsecureHttpOption =
            PlayerSettings.insecureHttpOption;
        try
        {
            EditorSceneManager.OpenScene(
                DemoScenePath,
                OpenSceneMode.Single
            );
            GameDirector director =
                UnityEngine.Object.FindObjectOfType<GameDirector>(true);
            if (director == null || director.multiplayerTankPrefab == null)
                throw new InvalidOperationException(
                    "DemoScene must have a GameDirector with " +
                    "multiplayerTankPrefab assigned."
                );

            string absoluteOutputPath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(absoluteOutputPath)
            );

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { DemoScenePath },
                locationPathName = absoluteOutputPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            };
            PlayerSettings.insecureHttpOption =
                InsecureHttpOption.AlwaysAllowed;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Linux dedicated-server build failed with result " +
                    report.summary.result + "."
                );
            }

            Debug.Log(
                "Linux dedicated server built at " + absoluteOutputPath +
                ". Use server/start-lan.sh to run the LAN lobby and " +
                "dedicated game server."
            );
        }
        finally
        {
            PlayerSettings.insecureHttpOption =
                previousInsecureHttpOption;
            if (!string.IsNullOrEmpty(previousScenePath) &&
                previousScenePath != DemoScenePath)
                EditorSceneManager.OpenScene(
                    previousScenePath,
                    OpenSceneMode.Single
                );
        }
    }
}
