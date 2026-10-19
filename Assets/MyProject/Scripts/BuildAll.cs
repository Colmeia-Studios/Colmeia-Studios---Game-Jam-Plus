using System.IO;
using UnityEditor;
using UnityEngine;


public class BuildAll
{
    
    [MenuItem("Build/1. All Platforms")]
    public static void BuildAllPlatforms()
    {
        BuildPCPlatforms();
        BuildMobilePlatforms();
    }
    
    [MenuItem("Build/2. PC Platforms")]
    public static void BuildPCPlatforms()
    {
        BuildWebGL();
        BuildLinux();
        BuildWindows();
    }
    
    [MenuItem("Build/3. Mobile Platforms")]
    public static void BuildMobilePlatforms()
    {
        BuildAndroid ();
    }
    
    [MenuItem("Build/4. Windows")]
    public static void BuildWindows()
    {
        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        string projectName = PlayerSettings.productName;
        string bundleVersion = PlayerSettings.bundleVersion;
        
        // Windows

        BuildPlayerOptions windows = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"Builds/Windows/{projectName} (Windows V{bundleVersion})/{projectName}.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(windows);
        
        Debug.Log("Build Windows Concluída!");
    }
    
    [MenuItem("Build/5. Linux")]
    public static void BuildLinux()
    {
        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        string projectName = PlayerSettings.productName;
        string bundleVersion = PlayerSettings.bundleVersion;
        
        // Linux

        BuildPlayerOptions linux = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"Builds/Linux/{projectName} (Linux V{bundleVersion})/{projectName}.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(linux);
        
        Debug.Log("Build Linux Concluída!");
    }
    
    [MenuItem("Build/6. WebGL")]
    public static void BuildWebGL()
    {
        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        string projectName = PlayerSettings.productName;
        string bundleVersion = PlayerSettings.bundleVersion;
        
        // WebGL

        BuildPlayerOptions web = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"Builds/WebGL/{projectName} (WebGL V{bundleVersion})",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(web);
        
        Debug.Log("Build WebGL Concluída!");
    }
    
    [MenuItem("Build/7. Android")]
    public static void BuildAndroid()
    {
        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        string projectName = PlayerSettings.productName;
        string bundleVersion = PlayerSettings.bundleVersion;
        
        // Android

        BuildPlayerOptions android = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"Builds/Android/{projectName} (V{bundleVersion}).apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(android);
        
        Debug.Log("Build Android Concluída!");
    }
    
    [MenuItem("Build/8. Clear All")]
    public static void ClearBuilds()
    {
        string pathName = Path.Combine(Application.dataPath, "..", "Builds");
        pathName = Path.GetFullPath(pathName);

        if (Directory.Exists(pathName))
        {
            Directory.Delete(pathName, true);
        }

        Directory.CreateDirectory(pathName);

        Debug.Log($"Builds limpas: {pathName}");
    }
    
}
