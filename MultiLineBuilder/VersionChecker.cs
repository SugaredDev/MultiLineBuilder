using UnityEngine;

public static class VersionChecker
{

    public static ProjectVersion version { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnRuntimeMethodLoad()
    {
        version = Resources.Load<ProjectVersion>("Version");

        if (version == null)
            Debug.LogError("Version.asset not found in Resources folder.");
        else
            Debug.Log($"Loaded {version.type} build version: {version.title} (Steam ID:{version.steamID}).");
    }
    
}
