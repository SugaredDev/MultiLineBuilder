using UnityEditor;
using UnityEngine;

namespace MultiClaw
{

[InitializeOnLoad]
public class VersionIncrementer
{
    static VersionIncrementer()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            Constants.EnsureActiveVersionExists();
            IncrementRevision();
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