using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MultiClaw
{

public class Builder : EditorWindow
{

    [System.Serializable]
    public class BuildConfig { public GameVersion configAsset; public bool buildEnabled; }

    class PlatformInfo
    {
        public bool isSteamDeck;
        public BuildTarget target;
        public string folder;
        public string extension;
        public PlatformInfo(bool steamDeck, BuildTarget target, string folder, string ext)
        {
            this.isSteamDeck = steamDeck;
            this.target = target;
            this.folder = folder;
            this.extension = ext;
        }
    }

    const string VersionsPath = "Assets/Plugins/MultiClaw/Resources";
    const string ActiveVersionPath = "Assets/Plugins/MultiClaw/Resources/ActiveVersion.asset";

    List<BuildConfig> buildVersions = new();
    Vector2 scroll;
    GameVersion inEditorVersion;
    bool buildWindows, buildMac, buildLinux, buildSteamDeck;

    readonly PlatformInfo[] platforms = {
        new(false, BuildTarget.StandaloneWindows64, "Windows", ".exe"),
        new(false, BuildTarget.StandaloneOSX, "macOS", ".app"),
        new(false, BuildTarget.StandaloneLinux64, "Linux", ".x86_64"),
        new(true, BuildTarget.StandaloneLinux64, "Steam Deck", ".x86_64")
    };

    [MenuItem("Tools/MultiClaw/Builder")]
    static void ShowWindow() => GetWindow<Builder>("MultiClaw | Builder");

    void OnEnable()
    {
        EnsureActiveVersionExists();
        RefreshVersionsList();
        inEditorVersion = AssetDatabase.LoadAssetAtPath<GameVersion>(ActiveVersionPath) ?? CreateDefaultVersion();
        EnsureActiveVersionMatchesAvailable();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Versions Folder"))
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(VersionsPath));
        if (GUILayout.Button("Refresh")) RefreshVersionsList();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Build Versions", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("+ New Version", GUILayout.Width(100)))
            CreateNewVersion();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < buildVersions.Count; i++) VersionEntry(i);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Box("", new GUIStyle(GUI.skin.box) { margin = new RectOffset(0, 0, 4, 4) }, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
        buildWindows = ColoredToggle("Windows", buildWindows);
        buildLinux = ColoredToggle("Linux", buildLinux);
        buildSteamDeck = ColoredToggle("Steam Deck", buildSteamDeck);
        buildMac = ColoredToggle("macOS", buildMac);

        GUILayout.Space(10);
        bool[] platformStates = { buildWindows, buildMac, buildLinux, buildSteamDeck };
        bool canBuild = platformStates.Any(p => p) && buildVersions.Any(v => v.buildEnabled);

        GUI.enabled = canBuild;
        GUI.backgroundColor = canBuild ? Color.green : Color.red;
        if (GUILayout.Button("Build Selected Versions")) BuildAll();
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
    }

    bool ColoredToggle(string label, bool value)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, new GUIStyle(EditorStyles.label) { normal = { textColor = value ? Color.green : Color.red } }, GUILayout.Width(150));
        bool result = EditorGUILayout.Toggle(value);
        EditorGUILayout.EndHorizontal();
        return result;
    }

    void VersionEntry(int build)
    {
        var version = buildVersions[build];
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        bool isActive = inEditorVersion != null && JsonUtility.ToJson(version.configAsset) == JsonUtility.ToJson(inEditorVersion);
        bool isActiveAsset = AssetDatabase.GetAssetPath(version.configAsset) == ActiveVersionPath;
        
        GUI.backgroundColor = isActive ? Color.yellow : Color.white;
        GUI.enabled = !isActiveAsset && version.configAsset != null;

        if (GUILayout.Button(isActive ? "Active In-Editor" : "Set Active", GUILayout.Width(110)))
        {
            EditorUtility.CopySerialized(version.configAsset, inEditorVersion);
            EditorUtility.SetDirty(inEditorVersion);
            inEditorVersion.name = "ActiveVersion";
        }

        GUI.enabled = true;
        GUI.backgroundColor = version.buildEnabled ? Color.green : Color.red;

        if (GUILayout.Button(version.buildEnabled ? "Enabled for build" : "Disabled for build", GUILayout.Width(110)))
            version.buildEnabled = !version.buildEnabled;

        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        
        GUI.enabled = !isActiveAsset;
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Delete", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("Delete Version", $"Are you sure you want to delete '{version.configAsset.title}'?", "Delete", "Cancel"))
            {
                var versionToDelete = version.configAsset;
                EditorApplication.delayCall += () => DeleteVersion(versionToDelete);
            }
        }

        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (version.configAsset != null)
        {
            GUI.enabled = !isActiveAsset;
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Title:", GUILayout.Width(140));
            version.configAsset.title = EditorGUILayout.TextField(version.configAsset.title);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Executable File Name:", GUILayout.Width(140));
            version.configAsset.fileName = EditorGUILayout.TextField(version.configAsset.fileName);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Debug:", GUILayout.Width(140));
            version.configAsset.debug = EditorGUILayout.Toggle(version.configAsset.debug);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Steam API:", GUILayout.Width(140));
            version.configAsset.steamAPI = (uint)EditorGUILayout.IntField((int)version.configAsset.steamAPI);
            EditorGUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(version.configAsset);
                AssetDatabase.SaveAssets();
            }
            
            GUI.enabled = true;
        }
        EditorGUILayout.EndVertical();
    }

    void BuildAll()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No scenes enabled in Build Settings.", "OK");
            return;
        }

        string buildsRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds");
        string originalJson = JsonUtility.ToJson(inEditorVersion);
        bool[] platformStates = { buildWindows, buildMac, buildLinux, buildSteamDeck };

        foreach (var version in buildVersions.Where(v => v.buildEnabled && v.configAsset != null))
        {
            for (int i = 0; i < platforms.Length; i++)
            {
                if (!platformStates[i]) continue;

                version.configAsset.steamDeck = platforms[i].isSteamDeck;
                EditorUtility.SetDirty(version.configAsset);
                EditorUtility.CopySerialized(version.configAsset, inEditorVersion);
                EditorUtility.SetDirty(inEditorVersion);

                BuildPlatform(version, platforms[i], buildsRoot, scenes);
            }
        }

        JsonUtility.FromJsonOverwrite(originalJson, inEditorVersion);
        EditorUtility.SetDirty(inEditorVersion);
        inEditorVersion.name = "ActiveVersion";
        AssetDatabase.SaveAssets();
        EditorUtility.RevealInFinder(buildsRoot);
        EditorUtility.DisplayDialog("Complete", "All builds finished!", "OK");
    }

    void BuildPlatform(BuildConfig version, PlatformInfo platform, string root, string[] scenes)
    {
        string folder = Path.Combine(root, version.configAsset.title, platform.folder);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, version.configAsset.fileName + platform.extension);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = platform.target,
            options = BuildOptions.None
        });

        Debug.Log($"Build {(report.summary.result == BuildResult.Succeeded ? "Succeeded" : "Failed")}: {path}");
    }

    void RefreshVersionsList()
    {
        buildVersions.Clear();
        buildWindows = buildMac = buildLinux = buildSteamDeck = false;
        
        if (!Directory.Exists(VersionsPath)) Directory.CreateDirectory(VersionsPath);

        var allVersions = AssetDatabase.FindAssets("t:GameVersion", new[] { VersionsPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<GameVersion>(path))
            .Where(asset => asset != null)
            .Select(asset => new BuildConfig { configAsset = asset })
            .ToList();
        
        var otherVersions = allVersions.Where(v => AssetDatabase.GetAssetPath(v.configAsset) != ActiveVersionPath).ToList();
        
        if (otherVersions.Count > 0)
            buildVersions = otherVersions;
        else
            buildVersions = allVersions;
        
        EnsureActiveVersionMatchesAvailable();
    }

    GameVersion CreateDefaultVersion()
    {
        if (!Directory.Exists(VersionsPath)) Directory.CreateDirectory(VersionsPath);
        var version = CreateInstance<GameVersion>();
        version.name = "ActiveVersion";
        version.title = "Debug";
        version.fileName = "Debug";
        AssetDatabase.CreateAsset(version, ActiveVersionPath);
        AssetDatabase.SaveAssets();
        return version;
    }

    void ShowActiveVersion()
    {
        EditorGUILayout.LabelField("Active Version", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        GUI.enabled = false;
        
        if (inEditorVersion != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Title:", GUILayout.Width(140));
            EditorGUILayout.TextField(inEditorVersion.title);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Executable File Name:", GUILayout.Width(140));
            EditorGUILayout.TextField(inEditorVersion.fileName);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Debug:", GUILayout.Width(140));
            EditorGUILayout.Toggle(inEditorVersion.debug);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Steam API:", GUILayout.Width(140));
            EditorGUILayout.IntField((int)inEditorVersion.steamAPI);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("No active version found.", MessageType.Warning);
        }
        
        GUI.enabled = true;
        EditorGUILayout.EndVertical();
    }

    void CreateNewVersion()
    {
        if (!Directory.Exists(VersionsPath)) Directory.CreateDirectory(VersionsPath);
        
        int versionNumber = 1;
        string assetPath;
        
        do
        {
            assetPath = Path.Combine(VersionsPath, $"Build Version {versionNumber}.asset");
            versionNumber++;
        } while (AssetDatabase.LoadAssetAtPath<GameVersion>(assetPath) != null);
        
        var newVersion = CreateInstance<GameVersion>();
        
        AssetDatabase.CreateAsset(newVersion, assetPath);
        AssetDatabase.SaveAssets();
        
        EditorApplication.delayCall += () =>
        {
            RefreshVersionsList();
            EditorGUIUtility.PingObject(newVersion);
        };
    }

    void DeleteVersion(GameVersion versionToDelete)
    {
        if (versionToDelete == null) return;
        
        string assetPath = AssetDatabase.GetAssetPath(versionToDelete);
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.SaveAssets();
        
        RefreshVersionsList();
    }

    void EnsureActiveVersionExists()
    {
        var activeVersion = AssetDatabase.LoadAssetAtPath<GameVersion>(ActiveVersionPath);
        
        if (activeVersion == null)
        {
            Debug.LogWarning("ActiveVersion.asset not found in Resources folder. => Creating a new one.");
            
            if (!Directory.Exists(VersionsPath))
                Directory.CreateDirectory(VersionsPath);
            
            var newVersion = CreateInstance<GameVersion>();
            newVersion.name = "Active Version";
            newVersion.title = "Dev";
            newVersion.fileName = "Development";
            
            AssetDatabase.CreateAsset(newVersion, ActiveVersionPath);
            AssetDatabase.SaveAssets();
        }
    }

    void EnsureActiveVersionMatchesAvailable()
    {
        if (inEditorVersion == null)
            inEditorVersion = AssetDatabase.LoadAssetAtPath<GameVersion>(ActiveVersionPath) ?? CreateDefaultVersion();
        
        if (buildVersions.Count == 0) return;
        
        bool foundMatch = false;
        foreach (var version in buildVersions)
        {
            if (version.configAsset != null && JsonUtility.ToJson(version.configAsset) == JsonUtility.ToJson(inEditorVersion))
            {
                foundMatch = true;
                break;
            }
        }
        
        if (!foundMatch && buildVersions.Count > 0 && buildVersions[0].configAsset != null && inEditorVersion != null)
        {
            EditorUtility.CopySerialized(buildVersions[0].configAsset, inEditorVersion);
            EditorUtility.SetDirty(inEditorVersion);
            inEditorVersion.name = "ActiveVersion";
            AssetDatabase.SaveAssets();
        }
    }
    
}

}