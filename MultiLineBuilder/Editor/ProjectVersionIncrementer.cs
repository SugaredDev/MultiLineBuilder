using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ProjectVersionIncrementer
{
    static ProjectVersionIncrementer()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            IncrementRevision();
        }
    }

    private static void IncrementRevision()
    {
        string version = PlayerSettings.bundleVersion;
        string[] parts = version.Split('.');

        if (parts.Length != 4)
        {
            Debug.LogWarning("Build version is not in the format 'major.minor.build.revision'. Resetting to '0.0.0.0'.");
            parts = new string[] { "0", "0", "0", "0" };
        }

        if (!int.TryParse(parts[3], out int revision))
        {
            revision = 0;
        }

        revision++;

        string newVersion = $"{parts[0]}.{parts[1]}.{parts[2]}.{revision}";

        PlayerSettings.bundleVersion = newVersion;

        Debug.Log($"Project Version updated to {newVersion}");
    }
}
