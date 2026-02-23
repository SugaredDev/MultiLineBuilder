using UnityEditor;
using UnityEngine;
using System.IO;

namespace MultiClaw
{

[InitializeOnLoad]
public class VersionIncrementer
{
    
    const string VersionsPath = "Assets/Plugins/MultiClaw/Resources";
    const string ActiveVersionPath = "Assets/Plugins/MultiClaw/Resources/ActiveVersion.asset";
    
    static VersionIncrementer()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            EnsureActiveVersionExists();
            IncrementRevision();
        }
    }

    static void EnsureActiveVersionExists()
    {
        var activeVersion = AssetDatabase.LoadAssetAtPath<GameVersion>(ActiveVersionPath);
        
        if (activeVersion == null)
        {
            Debug.LogWarning("ActiveVersion.asset not found in Resources folder. Creating a new one.");
            
            if (!Directory.Exists(VersionsPath))
                Directory.CreateDirectory(VersionsPath);
            
            var newVersion = ScriptableObject.CreateInstance<GameVersion>();
            newVersion.name = "Active Version";
            newVersion.title = "Dev";
            newVersion.fileName = "Development";
            
            AssetDatabase.CreateAsset(newVersion, ActiveVersionPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log("ActiveVersion.asset created successfully.");
        }
    }

    static void IncrementRevision()
    {
        string version = PlayerSettings.bundleVersion;
        string[] parts = version.Split('.');

        if (parts.Length != 4)
        {
            Debug.LogWarning("Build version is not in the correct format. => Resetting to '0.0.0.000'.");
            parts = new string[] { "0", "0", "0", "000" };
        }

        if (!int.TryParse(parts[3], out int revision))
            revision = 0;

        revision++;

        string newVersion = $"{parts[0]}.{parts[1]}.{parts[2]}.{revision:D3}";

        PlayerSettings.bundleVersion = newVersion;

        Debug.Log($"Project Version updated to {newVersion}");
    }

}

}