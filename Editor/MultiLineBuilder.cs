using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.IO;

public class MultiLineBuilder : EditorWindow
{

    [System.Serializable]
    public class BuildVersion { public ProjectVersion configAsset; }

    const string SaveKey = "MultiLineBuilder_SavedVersions";
    List<BuildVersion> buildVersions = new();
    Vector2 scroll;
    bool buildWindows, buildMac, buildLinux, buildSteamDeck;

    [MenuItem("Builds/MultiLineBuilder")]
    static void ShowWindow() => GetWindow<MultiLineBuilder>("MultiLine Builder");

    void OnEnable() => LoadVersions();

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < buildVersions.Count; i++) DrawVersionEntry(i);

        if (GUILayout.Button("Add New Version"))
        {
            buildVersions.Add(new BuildVersion());
            SaveVersions();
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(10);

        GUIStyle lineStyle = new GUIStyle(GUI.skin.box);
        lineStyle.margin = new RectOffset(0, 0, 4, 4);
        GUILayout.Box(GUIContent.none, lineStyle, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
        buildWindows = ColoredToggle("Windows", buildWindows);
        buildMac = ColoredToggle("macOS", buildMac);
        buildLinux = ColoredToggle("Linux", buildLinux);
        buildSteamDeck = ColoredToggle("Steam Deck (Linux)", buildSteamDeck);

        GUILayout.Space(10);

        bool noTarget = !(buildWindows || buildMac || buildLinux || buildSteamDeck);
        bool noVersions = buildVersions.Count == 0;

        GUI.backgroundColor = noVersions ? Color.red : noTarget ? new Color(0f, 1f, 0f, 0.25f) : Color.green;
        if (GUILayout.Button("Build Selected Versions"))
        {
            if (noVersions) { EditorUtility.DisplayDialog("Error", "Add at least one version.", "OK"); return; }
            if (noTarget) { EditorUtility.DisplayDialog("Error", "Select at least one platform.", "OK"); return; }
            BuildAllVersions();
        }
        GUI.backgroundColor = Color.white;
    }

    bool ColoredToggle(string label, bool value)
    {
        EditorGUILayout.BeginHorizontal();
        GUIStyle style = new(EditorStyles.label) { normal = { textColor = value ? Color.green : Color.red } };
        GUILayout.Label(label, style, GUILayout.Width(150));
        bool result = EditorGUILayout.Toggle(value);
        EditorGUILayout.EndHorizontal();
        return result;
    }

    void DrawVersionEntry(int i)
    {
        var version = buildVersions[i];
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(30)))
        {
            buildVersions.RemoveAt(i);
            SaveVersions();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.backgroundColor = Color.white;

        GUI.enabled = version.configAsset != null;
        var injectedPath = "Assets/Resources/Version.asset";
        ProjectVersion injected = AssetDatabase.LoadAssetAtPath<ProjectVersion>(injectedPath);
        GUI.backgroundColor = injected == version.configAsset ? Color.green : Color.white;

        if (GUILayout.Button("Set In-Editor", GUILayout.Width(90)))
        {
            InjectConfigToResources(version.configAsset);
            Debug.Log($"{version.configAsset.title} is now Editor's active version.");
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        var old = version.configAsset;
        version.configAsset = (ProjectVersion)EditorGUILayout.ObjectField("Version Pre-Asset:", version.configAsset, typeof(ProjectVersion), false);
        if (old != version.configAsset) SaveVersions();

        if (version.configAsset != null)
        {
            EditorGUILayout.LabelField("Title:", version.configAsset.title);
            EditorGUILayout.LabelField("Type:", version.configAsset.type.ToString());
            EditorGUILayout.LabelField("Steam ID:", version.configAsset.steamID.ToString());
        }

        EditorGUILayout.EndVertical();
    }

    void BuildAllVersions()
    {
        string[] scenes = GetEnabledScenes();
        string buildsRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds");

        foreach (var version in buildVersions)
        {
            if (version.configAsset == null)
            {
                Debug.LogWarning("Missing Pre-Asset => Skipping build.");
                continue;
            }

            InjectConfigToResources(version.configAsset);
            version.configAsset.SteamDeck = buildSteamDeck;
            EditorUtility.SetDirty(version.configAsset);

            if (buildWindows)
                BuildForPlatform(version, BuildTarget.StandaloneWindows64, "Windows", ".exe", buildsRoot, scenes);
            if (buildMac)
                BuildForPlatform(version, BuildTarget.StandaloneOSX, "macOS", ".app", buildsRoot, scenes);
            if (buildLinux)
                BuildForPlatform(version, BuildTarget.StandaloneLinux64, "Linux", "", buildsRoot, scenes);
            if (buildSteamDeck)
                BuildForPlatform(version, BuildTarget.StandaloneLinux64, "SteamDeck", "", buildsRoot, scenes);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.RevealInFinder(buildsRoot);
        EditorUtility.DisplayDialog("Build Complete", "All builds finished!", "OK");
    }

    void InjectConfigToResources(ProjectVersion configAsset)
    {
        string resourcesDir = "Assets/Resources";
        string targetPath = Path.Combine(resourcesDir, "Version.asset");

        if (!Directory.Exists(resourcesDir)) Directory.CreateDirectory(resourcesDir);

        AssetDatabase.DeleteAsset(targetPath);
        AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(configAsset), targetPath);
        AssetDatabase.Refresh();
    }

    void BuildForPlatform(BuildVersion version, BuildTarget target, string osFolder, string ext, string root, string[] scenes)
    {
        var c = version.configAsset;
        string folder = Path.Combine(root, c.title, osFolder);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, c.title + ext);

        var options = new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, options = BuildOptions.None };
        var report = BuildPipeline.BuildPlayer(options);

        Debug.Log(report.summary.result == BuildResult.Succeeded ? $"Build succeeded: {path}" : $"Build failed: {path}");
    }

    string[] GetEnabledScenes()
    {
        List<string> scenes = new();
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.enabled) scenes.Add(scene.path);
        return scenes.ToArray();
    }

    void SaveVersions()
    {
        EditorPrefs.SetString(SaveKey, JsonUtility.ToJson(new VersionListWrapper(buildVersions)));
    }

    void LoadVersions()
    {
        if (!EditorPrefs.HasKey(SaveKey)) return;
        var wrapper = JsonUtility.FromJson<VersionListWrapper>(EditorPrefs.GetString(SaveKey));
        buildVersions = wrapper.list ?? new List<BuildVersion>();
    }

    [System.Serializable]
    class VersionListWrapper { public List<BuildVersion> list; public VersionListWrapper(List<BuildVersion> list) => this.list = list; }

}