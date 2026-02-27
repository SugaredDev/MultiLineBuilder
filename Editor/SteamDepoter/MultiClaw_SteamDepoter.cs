using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace MultiClaw
{

public class SteamDepoter : EditorWindow
{
    [System.Serializable]
    public class SteamConfig
    {
        public string steamUsername = "";
        public string steamPassword = "";
        public string steamContentBuilderPath = "";
        public string appId = "0000000";
        public string depotWindows = "0000001";
        public string depotLinux = "0000003";
        public string depotSteamDeck = "0000004";
        public string steamBranch = "development";
    }

    static SteamConfig config;
    Vector2 scrollPos;

    const string CONFIG_KEY = "MultiClaw_SteamConfig";

    // ==============================================================================

    [MenuItem("Tools/MultiClaw/Steam Depoter")]
    static void ShowWindow() => GetWindow<SteamDepoter>("Steam Depoter");



    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        EditorGUILayout.LabelField("Steam Configuration", EditorStyles.boldLabel);

        config.steamUsername = EditorGUILayout.TextField("Username", config.steamUsername);
        config.steamPassword = EditorGUILayout.PasswordField("Password", config.steamPassword);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("ContentBuilder Location", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        config.steamContentBuilderPath = EditorGUILayout.TextField(config.steamContentBuilderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Steam ContentBuilder Folder", "", "");
            if (!string.IsNullOrEmpty(path))
                config.steamContentBuilderPath = path;
        }
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("Download ContentBuilder"))
            Application.OpenURL("https://partner.steamgames.com/doc/sdk/uploading#1");
        
        if (!string.IsNullOrEmpty(config.steamContentBuilderPath) && !Directory.Exists(config.steamContentBuilderPath))
            EditorGUILayout.HelpBox("Path does not exist!", MessageType.Error);
        
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Depots", EditorStyles.boldLabel);
        config.appId = EditorGUILayout.TextField("App ID", config.appId);
        config.depotWindows = EditorGUILayout.TextField("Windows Depot", config.depotWindows);
        config.depotLinux = EditorGUILayout.TextField("Linux Depot", config.depotLinux);
        config.depotSteamDeck = EditorGUILayout.TextField("Steam Deck Depot", config.depotSteamDeck);
        config.steamBranch = EditorGUILayout.TextField("Branch", config.steamBranch);
        
        if (string.IsNullOrEmpty(config.steamBranch))
            EditorGUILayout.HelpBox("Depot won't auto go live on Steam without a branch", MessageType.Warning);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Save"))
        {
            SaveConfig();
            EditorUtility.DisplayDialog("Saved", "Configuration saved", "OK");
        }

        bool canUpload = !string.IsNullOrEmpty(config.steamUsername) && 
                         !string.IsNullOrEmpty(config.steamPassword) &&
                         !string.IsNullOrEmpty(config.steamContentBuilderPath) &&
                         Directory.Exists(config.steamContentBuilderPath) &&
                         Directory.Exists(GetBuildsPath());

        GUI.enabled = canUpload;
        GUI.backgroundColor = canUpload ? Color.green : Color.gray;
        
        if (GUILayout.Button("Upload to Steam", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Upload", $"Upload to '{config.steamBranch}'?", "Yes", "No"))
                UploadToSteam();
        }
        
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        if (!Directory.Exists(GetBuildsPath()))
            EditorGUILayout.HelpBox("No builds found", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    // ==============================================================================

    void OnEnable()
    {
        LoadConfig();
    }

    void LoadConfig()
    {
        string json = EditorPrefs.GetString(CONFIG_KEY, "");
        if (!string.IsNullOrEmpty(json))
            config = JsonUtility.FromJson<SteamConfig>(json);
        else
            config = new SteamConfig();
    }

    static void SaveConfig()
    {
        string json = JsonUtility.ToJson(config);
        EditorPrefs.SetString(CONFIG_KEY, json);
    }

    public static void UploadAfterBuild()
    {
        string json = EditorPrefs.GetString(CONFIG_KEY, "");
        if (string.IsNullOrEmpty(json)) return;
        
        config = JsonUtility.FromJson<SteamConfig>(json);
        if (string.IsNullOrEmpty(config.steamUsername) || string.IsNullOrEmpty(config.steamPassword)) return;
        
        UploadToSteam();
    }

    static void UploadToSteam()
    {
        SaveConfig();
        
        string buildsPath = GetBuildsPath();
        if (!Directory.Exists(buildsPath))
        {
            EditorUtility.DisplayDialog("Error", "No builds found", "OK");
            return;
        }

        var versionDirs = Directory.GetDirectories(buildsPath);
        if (versionDirs.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No build versions found", "OK");
            return;
        }

        foreach (var versionDir in versionDirs)
        {
            string versionName = Path.GetFileName(versionDir);
            GenerateVDFFiles(versionDir, versionName);
            bool success = ExecuteSteamCmd(versionName);
            
            if (success)
                UnityEngine.Debug.LogWarning($"Uploaded '{versionName}'");
            else
                UnityEngine.Debug.LogError($"Failed '{versionName}'");
        }
    }

    static void GenerateVDFFiles(string versionPath, string versionName)
    {
        string scriptsPath = GetSteamScriptsPath();
        if (!Directory.Exists(scriptsPath))
            Directory.CreateDirectory(scriptsPath);

        string outputPath = GetSteamOutputPath();
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        string appVdfPath = Path.Combine(scriptsPath, $"app_{config.appId}_{versionName}.vdf");
        GenerateAppBuildVDF(appVdfPath, versionPath, versionName, outputPath);

        string windowsPath = Path.Combine(versionPath, "Windows");
        if (Directory.Exists(windowsPath))
        {
            string depotVdfPath = Path.Combine(scriptsPath, $"depot_{config.depotWindows}_{versionName}.vdf");
            GenerateDepotVDF(depotVdfPath, config.depotWindows, windowsPath);
        }

        string linuxPath = Path.Combine(versionPath, "Linux");
        if (Directory.Exists(linuxPath))
        {
            string depotVdfPath = Path.Combine(scriptsPath, $"depot_{config.depotLinux}_{versionName}.vdf");
            GenerateDepotVDF(depotVdfPath, config.depotLinux, linuxPath);
        }

        string steamDeckPath = Path.Combine(versionPath, "Steam Deck");
        if (Directory.Exists(steamDeckPath))
        {
            string depotVdfPath = Path.Combine(scriptsPath, $"depot_{config.depotSteamDeck}_{versionName}.vdf");
            GenerateDepotVDF(depotVdfPath, config.depotSteamDeck, steamDeckPath);
        }

        string macPath = Path.Combine(versionPath, "macOS");
        if (Directory.Exists(macPath))
            UnityEngine.Debug.LogWarning("macOS builds found but no depot configured. Skipping macOS upload.");
    }

    static void GenerateAppBuildVDF(string filePath, string versionPath, string versionName, string outputPath)
    {
        List<string> depots = new List<string>();

        if (Directory.Exists(Path.Combine(versionPath, "Windows")))
            depots.Add($"\t\t\"{config.depotWindows}\"\t\"{GetSteamScriptsPath()}/depot_{config.depotWindows}_{versionName}.vdf\"");

        if (Directory.Exists(Path.Combine(versionPath, "Linux")))
            depots.Add($"\t\t\"{config.depotLinux}\"\t\"{GetSteamScriptsPath()}/depot_{config.depotLinux}_{versionName}.vdf\"");

        if (Directory.Exists(Path.Combine(versionPath, "Steam Deck")))
            depots.Add($"\t\t\"{config.depotSteamDeck}\"\t\"{GetSteamScriptsPath()}/depot_{config.depotSteamDeck}_{versionName}.vdf\"");

        string depotsSection = string.Join("\n", depots);

        string content = $@"""appbuild""
{{
	""appid"" ""{config.appId}""
	""desc"" ""v{PlayerSettings.bundleVersion}""
	""buildoutput"" ""{outputPath.Replace("\\", "/")}""
	""contentroot"" """"
	""setlive"" ""{config.steamBranch}""
	""preview"" ""0""
	""local""	""""
	""depots""
	{{
{depotsSection}
	}}
}}";

        File.WriteAllText(filePath, content);
    }

    static void GenerateDepotVDF(string filePath, string depotId, string contentPath)
    {
        string content = $@"""DepotBuildConfig""
{{
	""DepotID"" ""{depotId}""
	""contentroot"" ""{contentPath.Replace("\\", "/")}""
	""FileMapping""
	{{
		""LocalPath"" ""*""
		""DepotPath"" "".""
		""recursive"" ""1""
	}}
	""FileExclusion"" ""*.pdb""
}}";

        File.WriteAllText(filePath, content);
    }

    static bool ExecuteSteamCmd(string versionName)
    {
        string steamCmdPath = GetSteamCmdPath();
        if (!File.Exists(steamCmdPath))
        {
            UnityEngine.Debug.LogError("SteamCmd not found");
            return false;
        }

        #if !UNITY_EDITOR_WIN
        try
        {
            Process chmodProcess = new Process();
            chmodProcess.StartInfo.FileName = "chmod";
            chmodProcess.StartInfo.Arguments = $"-R +x \"{Path.GetDirectoryName(steamCmdPath)}\"";
            chmodProcess.StartInfo.UseShellExecute = false;
            chmodProcess.StartInfo.CreateNoWindow = true;
            chmodProcess.Start();
            chmodProcess.WaitForExit();
        }
        catch { }
        #endif

        string appVdfPath = Path.Combine(GetSteamScriptsPath(), $"app_{config.appId}_{versionName}.vdf");
        if (!File.Exists(appVdfPath))
        {
            UnityEngine.Debug.LogError("VDF not found");
            return false;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        string args = $"+login {config.steamUsername} {config.steamPassword} +run_app_build \"{appVdfPath}\" +quit";

        #if UNITY_EDITOR_WIN
        startInfo.FileName = steamCmdPath;
        startInfo.Arguments = args;
        startInfo.WorkingDirectory = Path.GetDirectoryName(steamCmdPath);
        startInfo.UseShellExecute = true;
        #else
        startInfo.WorkingDirectory = Path.GetDirectoryName(steamCmdPath);
        startInfo.UseShellExecute = true;
        
        string terminal = File.Exists("/usr/bin/gnome-terminal") ? "gnome-terminal" :
                         File.Exists("/usr/bin/konsole") ? "konsole" :
                         File.Exists("/usr/bin/xterm") ? "xterm" : "x-terminal-emulator";
        
        startInfo.FileName = terminal;
        startInfo.Arguments = terminal == "gnome-terminal" 
            ? $"-- bash -c '\"{steamCmdPath}\" {args}; read'"
            : $"-e bash -c '\"{steamCmdPath}\" {args}; read'";
        #endif

        try
        {
            Process process = Process.Start(startInfo);
            #if UNITY_EDITOR_WIN
            if (process != null)
            {
                process.WaitForExit();
                bool success = process.ExitCode == 0;
                process.Dispose();
                return success;
            }
            return false;
            #else
            return true;
            #endif
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed: {e.Message}");
            return false;
        }
    }

    static string GetBuildsPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds");
    }

    static string GetSteamContentBuilderPath() => config.steamContentBuilderPath ?? "";

    static string GetSteamScriptsPath()
    {
        return Path.Combine(GetSteamContentBuilderPath(), "scripts");
    }

    static string GetSteamOutputPath()
    {
        return Path.Combine(GetSteamContentBuilderPath(), "output");
    }

    static string GetSteamCmdPath()
    {
        string builderPath = Path.Combine(GetSteamContentBuilderPath(), "builder");
        
        #if UNITY_EDITOR_WIN
        return Path.Combine(builderPath, "steamcmd.exe");
        #elif UNITY_EDITOR_OSX
        return Path.Combine(GetSteamContentBuilderPath(), "builder_osx", "steamcmd.sh");
        #else
        return Path.Combine(GetSteamContentBuilderPath(), "builder_linux", "steamcmd.sh");
        #endif
    }
}

}