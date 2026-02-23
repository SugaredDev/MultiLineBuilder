using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace MultiClaw
{

public static class Constants
{
    
    public const string Path_Versions = "Assets/Plugins/MultiClaw/Resources/Game Versions";
    public const string Resources_Active = "Game Versions/Active Version";
    public const string Path_Active = "Assets/Plugins/MultiClaw/Resources/Game Versions/Active Version.asset";
    public const string Resources_Indicator = "Version Indication Font/Indicator";
    public const string Path_Indicator = "Assets/Plugins/MultiClaw/Resources/Version Indication Font";

#if UNITY_EDITOR
    public static GameVersion EnsureActiveVersionExists()
    {
        var activeVersion = AssetDatabase.LoadAssetAtPath<GameVersion>(Path_Active);
        
        if (activeVersion == null)
        {
            Debug.LogWarning("Active Version.asset not found in Resources folder. Creating a new one.");
            
            if (!Directory.Exists(Path_Versions))
                Directory.CreateDirectory(Path_Versions);
            
            activeVersion = ScriptableObject.CreateInstance<GameVersion>();
            activeVersion.name = "Active Version";
            activeVersion.title = "Dev";
            activeVersion.fileName = "Development";
            
            AssetDatabase.CreateAsset(activeVersion, Path_Active);
            AssetDatabase.SaveAssets();
            
            Debug.Log("Active Version.asset created successfully.");
        }
        
        return activeVersion;
    }
#endif

    public static bool IsDebugVersion(GameVersion version)
    {
        return version != null && version.debug;
    }

}

}
