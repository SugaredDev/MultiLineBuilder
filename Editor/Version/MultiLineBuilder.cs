using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sugared
{

public class MultiLineBuilder : EditorWindow
{

    [System.Serializable]
    public class BuildConfig { public BuildVersion configAsset; public bool buildEnabled; }

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

    const string VersionsPath = "Assets/Plugins/MultiLineBuilder/Resources";
    const string ActiveVersionPath = "Assets/Plugins/MultiLineBuilder/Resources/ActiveVersion.asset";

    List<BuildConfig> buildVersions = new();
    Vector2 scroll;
    BuildVersion inEditorVersion;
    bool buildWindows, buildMac, buildLinux, buildSteamDeck;

    readonly PlatformInfo[] platforms = {
        new(false, BuildTarget.StandaloneWindows64, "Windows", ".exe"),
        new(false, BuildTarget.StandaloneOSX, "macOS", ".app"),
        new(false, BuildTarget.StandaloneLinux64, "Linux", ".x86_64"),
        new(true, BuildTarget.StandaloneLinux64, "Steam Deck", ".x86_64")
    };

    [MenuItem("Tools/MultiLine Builder")]
    static void ShowWindow() => GetWindow<MultiLineBuilder>("MultiLine Builder");

    void OnEnable()
    {
        RefreshVersionsList();
        inEditorVersion = AssetDatabase.LoadAssetAtPath<BuildVersion>(ActiveVersionPath) ?? CreateDefaultVersion();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Versions Folder"))
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(VersionsPath));
        if (GUILayout.Button("Refresh")) RefreshVersionsList();
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < buildVersions.Count; i++) VersionEntry(i);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Box("", new GUIStyle(GUI.skin.box) { margin = new RectOffset(0, 0, 4, 4) }, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
        buildWindows = ColoredToggle("Windows", buildWindows);
        buildMac = ColoredToggle("macOS", buildMac);
        buildLinux = ColoredToggle("Linux", buildLinux);
        buildSteamDeck = ColoredToggle("Steam Deck", buildSteamDeck);

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
        GUI.backgroundColor = isActive ? Color.yellow : Color.white;
        GUI.enabled = !isActive && version.configAsset != null;

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
        EditorGUILayout.EndHorizontal();

        if (version.configAsset != null)
        {
            EditorGUILayout.LabelField("Title:", version.configAsset.title);
            EditorGUILayout.LabelField("File:", version.configAsset.fileName);
            EditorGUILayout.LabelField("Type:", version.configAsset.type.ToString());
            EditorGUILayout.LabelField("Steam API:", version.configAsset.steamAPI.ToString());
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

        buildVersions = AssetDatabase.FindAssets("t:BuildVersion", new[] { VersionsPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path != ActiveVersionPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<BuildVersion>(path))
            .Where(asset => asset != null)
            .Select(asset => new BuildConfig { configAsset = asset })
            .ToList();
    }

    BuildVersion CreateDefaultVersion()
    {
        if (!Directory.Exists(VersionsPath)) Directory.CreateDirectory(VersionsPath);
        var version = CreateInstance<BuildVersion>();
        version.name = "ActiveVersion";
        AssetDatabase.CreateAsset(version, ActiveVersionPath);
        AssetDatabase.SaveAssets();
        return version;
    }
    
}

}