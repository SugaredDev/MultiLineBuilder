using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace MultiClaw
{

public static class Constants
{
    public const string VersionsPath = "Assets/Plugins/MultiClaw/Resources/Game Versions";
    public const string ActiveVersionPath = "Assets/Plugins/MultiClaw/Resources/Game Versions/Active Version.asset";
    public const string ActiveVersionResourceName = "Game Versions/Active Version";
    public const string GUIFontResourceName = "GUI";

#if UNITY_EDITOR
    public static GameVersion EnsureActiveVersionExists()
    {
        var activeVersion = AssetDatabase.LoadAssetAtPath<GameVersion>(ActiveVersionPath);
        
        if (activeVersion == null)
        {
            Debug.LogWarning("ActiveVersion.asset not found in Resources folder. Creating a new one.");
            
            if (!Directory.Exists(VersionsPath))
                Directory.CreateDirectory(VersionsPath);
            
            activeVersion = ScriptableObject.CreateInstance<GameVersion>();
            activeVersion.name = "Active Version";
            activeVersion.title = "Dev";
            activeVersion.fileName = "Development";
            
            AssetDatabase.CreateAsset(activeVersion, ActiveVersionPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log("ActiveVersion.asset created successfully.");
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
