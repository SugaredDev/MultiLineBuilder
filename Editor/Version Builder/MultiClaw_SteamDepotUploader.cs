using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace MultiClaw
{

public class SteamDepotUploader : EditorWindow
{
    [System.Serializable]
    public class SteamConfig
    {
        public string steamUsername = "";
        public string appId = "0000000";
        public string depotWindows = "0000001";
        public string depotLinux = "0000003";
        public string depotSteamDeck = "0000004";
        public string steamBranch = "development";
        public bool autoUploadAfterBuild = false;
    }

    static SteamConfig config;
    static string temporaryPassword = "";
    Vector2 scrollPos;
    bool showAdvanced = false;

    const string CONFIG_KEY = "MultiClaw_SteamConfig";
    const string STEAM_CONTENT_BUILDER_PATH = "Assets/MultiClaw/Steam ContentBuilder";

    [MenuItem("Tools/MultiClaw/Steam Depot Uploader")]
    static void ShowWindow() => GetWindow<SteamDepotUploader>("Steam Depot Uploader");

    void OnEnable()
    {
        LoadConfig();
        temporaryPassword = "";
    }

    void OnDisable()
    {
        temporaryPassword = "";
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        EditorGUILayout.LabelField("Steam Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        config.steamUsername = EditorGUILayout.TextField("Steam Username", config.steamUsername);
        temporaryPassword = EditorGUILayout.PasswordField("Steam Password", temporaryPassword);
        EditorGUILayout.HelpBox("Password is never saved - you must enter it each session", MessageType.Info);
        EditorGUILayout.Space(5);

        config.steamBranch = EditorGUILayout.TextField("Steam Branch", config.steamBranch);
        EditorGUILayout.HelpBox("Branch to upload to (e.g., 'development', 'beta', 'production')", MessageType.Info);
        EditorGUILayout.Space(5);

        config.autoUploadAfterBuild = EditorGUILayout.Toggle("Auto-Upload After Build", config.autoUploadAfterBuild);
        EditorGUILayout.HelpBox("Note: Auto-upload requires manual password entry and is not fully supported", MessageType.Warning);
        EditorGUILayout.Space(10);

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Settings", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            config.appId = EditorGUILayout.TextField("App ID", config.appId);
            config.depotWindows = EditorGUILayout.TextField("Windows Depot ID", config.depotWindows);
            config.depotLinux = EditorGUILayout.TextField("Linux Depot ID", config.depotLinux);
            config.depotSteamDeck = EditorGUILayout.TextField("Steam Deck Depot ID", config.depotSteamDeck);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        EditorGUILayout.Space(5);

        bool canUpload = !string.IsNullOrEmpty(config.steamUsername) && 
                         !string.IsNullOrEmpty(temporaryPassword) &&
                         Directory.Exists(GetBuildsPath());

        GUI.enabled = canUpload;
        GUI.backgroundColor = canUpload ? Color.green : Color.gray;
        
        if (GUILayout.Button("Upload Current Builds to Steam", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Upload to Steam", 
                $"Upload builds to Steam branch '{config.steamBranch}'?", 
                "Upload", "Cancel"))
            {
                UploadToSteam();
            }
        }
        
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Save Configuration"))
        {
            SaveConfig();
            EditorUtility.DisplayDialog("Saved", "Steam configuration saved successfully!", "OK");
        }

        if (!Directory.Exists(GetBuildsPath()))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("No builds found. Use Version Builder to create builds first.", MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
    }

    void LoadConfig()
    {
        string json = EditorPrefs.GetString(CONFIG_KEY, "");
        if (!string.IsNullOrEmpty(json))
        {
            config = JsonUtility.FromJson<SteamConfig>(json);
        }
        else
        {
            config = new SteamConfig();
        }
    }

    static void SaveConfig()
    {
        string json = JsonUtility.ToJson(config);
        EditorPrefs.SetString(CONFIG_KEY, json);
    }

    public static void UploadAfterBuild()
    {
        string json = EditorPrefs.GetString(CONFIG_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            UnityEngine.Debug.LogWarning("Steam config not found. Skipping auto-upload.");
            return;
        }

        config = JsonUtility.FromJson<SteamConfig>(json);
        
        if (!config.autoUploadAfterBuild)
        {
            UnityEngine.Debug.Log("Auto-upload disabled. Skipping Steam upload.");
            return;
        }

        UnityEngine.Debug.LogWarning("Auto-upload after build is not supported (password required). Please upload manually from Steam Depot Uploader window.");
    }

    static void UploadToSteam()
    {
        SaveConfig();
        
        string buildsPath = GetBuildsPath();
        if (!Directory.Exists(buildsPath))
        {
            UnityEngine.Debug.LogError("Builds directory not found: " + buildsPath);
            EditorUtility.DisplayDialog("Error", "Builds directory not found!", "OK");
            return;
        }

        var versionDirs = Directory.GetDirectories(buildsPath);
        if (versionDirs.Length == 0)
        {
            UnityEngine.Debug.LogError("No build versions found in: " + buildsPath);
            EditorUtility.DisplayDialog("Error", "No build versions found!", "OK");
            return;
        }

        foreach (var versionDir in versionDirs)
        {
            string versionName = Path.GetFileName(versionDir);
            GenerateVDFFiles(versionDir, versionName);
            
            bool success = ExecuteSteamCmd(versionName);
            
            if (success)
                UnityEngine.Debug.LogWarning($"Successfully uploaded '{versionName}' to Steam branch '{config.steamBranch}'");
            else
                UnityEngine.Debug.LogError($"Failed to upload '{versionName}' to Steam");
        }

        temporaryPassword = "";

        EditorUtility.DisplayDialog("Upload Complete", 
            $"Steam depot upload finished!\nBranch: {config.steamBranch}", "OK");
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
        UnityEngine.Debug.Log($"Generated app build VDF: {filePath}");
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
        UnityEngine.Debug.Log($"Generated depot VDF: {filePath}");
    }

    static bool ExecuteSteamCmd(string versionName)
    {
        string steamCmdPath = GetSteamCmdPath();
        if (!File.Exists(steamCmdPath))
        {
            UnityEngine.Debug.LogError($"SteamCmd not found at: {steamCmdPath}");
            EditorUtility.DisplayDialog("Error", "SteamCmd executable not found!", "OK");
            return false;
        }

        #if !UNITY_EDITOR_WIN
        try
        {
            string builderDir = Path.GetDirectoryName(steamCmdPath);
            
            Process chmodProcess = new Process();
            chmodProcess.StartInfo.FileName = "chmod";
            chmodProcess.StartInfo.Arguments = $"-R +x \"{builderDir}\"";
            chmodProcess.StartInfo.UseShellExecute = false;
            chmodProcess.StartInfo.CreateNoWindow = true;
            chmodProcess.Start();
            chmodProcess.WaitForExit();
            UnityEngine.Debug.Log($"Set execute permissions for all files in: {builderDir}");
        }
        catch (System.Exception e) 
        {
            UnityEngine.Debug.LogWarning($"Could not set execute permissions: {e.Message}");
        }
        #endif

        string appVdfPath = Path.Combine(GetSteamScriptsPath(), $"app_{config.appId}_{versionName}.vdf");
        
        if (!File.Exists(appVdfPath))
        {
            UnityEngine.Debug.LogError($"App VDF not found: {appVdfPath}");
            return false;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        
        UnityEngine.Debug.Log($"Executing SteamCmd...");
        UnityEngine.Debug.Log($"VDF Path: {appVdfPath}");

        #if UNITY_EDITOR_WIN
        startInfo.FileName = steamCmdPath;
        startInfo.Arguments = $"+login {config.steamUsername} {temporaryPassword} +run_app_build \"{appVdfPath}\" +quit";
        startInfo.WorkingDirectory = Path.GetDirectoryName(steamCmdPath);
        startInfo.UseShellExecute = true;
        startInfo.CreateNoWindow = false;
        #else
        startInfo.WorkingDirectory = Path.GetDirectoryName(steamCmdPath);
        
        if (File.Exists("/usr/bin/gnome-terminal"))
        {
            startInfo.FileName = "gnome-terminal";
            startInfo.Arguments = $"-- bash -c '\"{steamCmdPath}\" +login {config.steamUsername} {temporaryPassword} +run_app_build \"{appVdfPath}\" +quit; echo; echo \"Press Enter to close...\"; read'";
        }
        else if (File.Exists("/usr/bin/konsole"))
        {
            startInfo.FileName = "konsole";
            startInfo.Arguments = $"-e bash -c '\"{steamCmdPath}\" +login {config.steamUsername} {temporaryPassword} +run_app_build \"{appVdfPath}\" +quit; echo; echo \"Press Enter to close...\"; read'";
        }
        else if (File.Exists("/usr/bin/xterm"))
        {
            startInfo.FileName = "xterm";
            startInfo.Arguments = $"-e bash -c '\"{steamCmdPath}\" +login {config.steamUsername} {temporaryPassword} +run_app_build \"{appVdfPath}\" +quit; echo; echo \"Press Enter to close...\"; read'";
        }
        else
        {
            startInfo.FileName = "x-terminal-emulator";
            startInfo.Arguments = $"-e bash -c '\"{steamCmdPath}\" +login {config.steamUsername} {temporaryPassword} +run_app_build \"{appVdfPath}\" +quit; echo; echo \"Press Enter to close...\"; read'";
        }
        
        startInfo.UseShellExecute = true;
        startInfo.CreateNoWindow = false;
        #endif

        try
        {
            Process process = Process.Start(startInfo);
            
            #if UNITY_EDITOR_WIN
            if (process != null)
            {
                process.WaitForExit();
                int exitCode = process.ExitCode;
                process.Dispose();
                
                if (exitCode == 0)
                {
                    UnityEngine.Debug.LogWarning("SteamCmd completed successfully");
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"SteamCmd exited with code: {exitCode}");
                    return false;
                }
            }
            return false;
            #else
            UnityEngine.Debug.LogWarning("SteamCmd launched in terminal. Check terminal window for progress.");
            return true;
            #endif
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to execute SteamCmd: {e.Message}");
            UnityEngine.Debug.LogError($"Stack trace: {e.StackTrace}");
            return false;
        }
    }

    static string GetBuildsPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds");
    }

    static string GetSteamContentBuilderPath()
    {
        return Path.Combine(Application.dataPath, "MultiClaw", "Steam ContentBuilder");
    }

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