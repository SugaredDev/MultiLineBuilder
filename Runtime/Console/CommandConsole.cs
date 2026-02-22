using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;

// To mark a function as a command, add '[Command]' attribute to it, make it's input 'string[]' and make it static.
// You can also specify command name like '[Command("otherthatfunctionname")]'.

namespace Sugared
{

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class CommandAttribute : Attribute
{

    public string Name { get; set; }
    
    public CommandAttribute() { }
    
    public CommandAttribute(string name) => Name = name;
        
}

public class CommandConsole : MonoBehaviour
{

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var cloud = GameObject.Find("Cloud");
        if (cloud == null)
        { 
            cloud = new GameObject("Cloud");
            DontDestroyOnLoad(cloud);
        }   
        cloud.AddComponent<CommandConsole>();
    }

    public void Awake()
    {
        SetConsole();
        SetCommands();
    }

    void OnValidate()
    {
        SetCommands();
    }

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }
    
    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void Update()
    {
        if (VersionSystem.version == null || VersionSystem.version.type != BuildVersion.ProjectType.Playtest)
            return;

        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            ToggleConsole();
    }

    void ToggleConsole()
    {
        showConsole = !showConsole;

        Time.timeScale = showConsole ? 0f : 1f;
        
        if (showConsole)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;
            
            grabFocus = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Confined;
            InputSystem.actions.FindActionMap("Player").Disable();
        }
        else
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
            InputSystem.actions.FindActionMap("Player").Enable();
        }
        
        var inputModule = FindFirstObjectByType<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            var go = new GameObject("UIInput");
            inputModule = go.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(go);
        }
        inputModule.enabled = !showConsole;

        input = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────

    [HideInInspector] public static CommandConsole console;
    void SetConsole()
    {
        if (console == null)
        {
            console = this;
            DontDestroyOnLoad(console);
        }
        else
            Destroy(gameObject);
    }

    Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();
    Dictionary<string, object> commandInstances = new Dictionary<string, object>();
    
    void SetCommands()
    {
        commands.Clear();
        commandInstances.Clear();
        
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        foreach (Assembly assembly in assemblies)
        {
            try
            {
                Type[] types = assembly.GetTypes();
                
                foreach (Type type in types)
                {
                    MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    foreach (MethodInfo method in methods)
                    {
                        CommandAttribute commandAttr = method.GetCustomAttribute<CommandAttribute>();
                        if (commandAttr == null)
                            continue;
                        
                        if (method.ReturnType != typeof(void))
                            continue;
                        
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string[]))
                            continue;
                        
                        string commandName = string.IsNullOrEmpty(commandAttr.Name) ? method.Name.ToLower() : commandAttr.Name.ToLower();
                        
                        if (method.IsStatic)
                        {
                            Action<string[]> action = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), method);
                            RegisterCommand(commandName, action);
                        }
                        else
                        {
                            object instance = null;
                            
                            if (!commandInstances.TryGetValue(type.FullName, out instance))
                            {
                                try
                                {
                                    instance = Activator.CreateInstance(type);
                                    commandInstances[type.FullName] = instance;
                                }
                                catch
                                {
                                    Debug.LogWarning($"Could not create instance of {type.Name} for command {commandName}");
                                    continue;
                                }
                            }
                            
                            Action<string[]> action = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), instance, method);
                            RegisterCommand(commandName, action);
                        }
                    }
                }
            }
            catch
            {
                continue;
            }
        }
    }

    void RegisterCommand(string command, Action<string[]> callback)
    {
        if (!commands.ContainsKey(command))
            commands.Add(command, callback);
    }

    HashSet<string> words = new();
    public void RegisterKnownWord(string word)
    {
        words.Add(word);
    }

    bool showConsole = false;
    bool grabFocus = false;
    bool previousCursorVisible;
    CursorLockMode previousCursorLockState;
    string input;
    int historyIndex = -1;
    Vector2 scroll;

    [HideInInspector] public List<string> log = new List<string>();
    [HideInInspector] public List<string> commandHistory = new List<string>();
    void ExecuteCommand(string commandLine)
    {
        commandHistory.Add(commandLine);
        historyIndex = -1;

        string[] parts = commandLine.Split(' ');
        if (parts.Length == 0) return;

        string command = parts[0].ToLower();
        string[] args = new string[parts.Length - 1];
        Array.Copy(parts, 1, args, 0, args.Length);

        if (commands.TryGetValue(command, out var callback))
            callback.Invoke(args);
        else
            log[log.Count - 1] = $"Unknown command: {command}";
    }

    public void Result(string result)
    {
        Log($"Result: {result}");
    }

    void Log(string message)
    {
        if (log.Count >= 10)
            log.RemoveAt(0);
        log.Add(message);
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            Log("Error: " + logString);
        else if (type == LogType.Warning)
            Log("Warning: " + logString);
        else if (type == LogType.Log)
            Log("Debug: " + logString);
    }

    bool NextHistory(int move)
    {
        if (commandHistory.Count == 0)
        {
            return false;
        }

        if (historyIndex == -1 && move > 0)
        {
            return false;
        }
        if (historyIndex == -1 && move < 0)
        {
            historyIndex = commandHistory.Count - 1;
            input = commandHistory[historyIndex];
            return true;
        }

        if (historyIndex == 0 && move < 0)
        {
            return false;
        }

        if (historyIndex == commandHistory.Count -1 && move > 0)
        {
            historyIndex = -1;
            input = "";
            return false;
        }

        historyIndex = historyIndex + move;
        input = commandHistory[historyIndex];
        return true;
    }

    bool AutoComplete()
    {
        if (input.EndsWith(" "))
        {
            return false;
        }

        var parts = input.Split(" ");

        var lastWord = parts[parts.Length - 1];

        List<string> candidates = new();

        foreach (string command in commands.Keys)
        {
            if (command.StartsWith(lastWord))
            {
                candidates.Add(command);
            }
        }
        foreach (string word in words)
        {
            if (word.StartsWith(lastWord))
            {
                candidates.Add(word);
            }
        }

        candidates.Sort();

        if (candidates.Count == 0)
        {
            return false;
        }

        var prefix = LongestCommonPrefix(candidates);
        var previousWords = "";
        if (parts.Length > 1)
        {
            previousWords = string.Join(" ", parts.Take(parts.Length - 1)) + " ";
        } 
        input = previousWords + prefix;

        if (candidates.Count > 1)
        {
            var sb = new StringBuilder();
            foreach (string candidate in candidates)
            {
                sb.Append(candidate + "   ");
            }
            Log(sb.ToString());
        }

        return true;
    }

    string LongestCommonPrefix(List<string> inputs)
    {
        var prefix = "";
        var shortestInputLength = int.MaxValue;
        foreach (string input in inputs)
        {
            if (input.Length < shortestInputLength)
                shortestInputLength = input.Length;
        }

        for (int i = 0; i < shortestInputLength; i++)
        {
            char? candidate = null;
            foreach (string input in inputs)
            {
                char nextChar = input.Split(prefix, 2)[^1][0];
                if (candidate == null)
                {
                    candidate = nextChar;
                }
                else if (candidate != nextChar)
                {
                    return prefix;
                }
            }
            prefix = prefix + candidate;
        }

        return prefix;
    }

    // ─────────────────────────────────────────────────────────────

    void OnGUI() // Visuals
    {
        if (!showConsole) return;

        float width = Screen.width;
        float height = Screen.height / 3;
        GUI.Box(new Rect(0, 0, width, height), "");

        Rect textFieldRect = new Rect(0, Mathf.Clamp(log.Count * Mathf.FloorToInt(height * 0.10f), 0, height), width, height / 10);
        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = Mathf.FloorToInt(textFieldRect.height * 0.7f);
        textFieldStyle.fixedHeight = textFieldRect.height;
        textFieldStyle.alignment = TextAnchor.MiddleLeft;

        scroll = GUI.BeginScrollView(new Rect(0, 0, width, Mathf.Clamp(log.Count * Mathf.FloorToInt(height * 0.10f), 0, textFieldRect.height * 10)), scroll, new Rect(0, 0, width - 40, 20 * log.Count));
        for (int i = 0; i < log.Count; i++)
        {
            Color originalColor = GUI.color;
            if (log[i].StartsWith("Error:"))
                GUI.color = Color.red;
            else if (log[i].StartsWith("Warning:"))
                GUI.color = Color.yellow;
            else if (log[i].StartsWith("Debug:"))
                GUI.color = Color.cyan;
            else if (log[i].StartsWith("Unknown command:"))
                GUI.color = Color.magenta;
            else if (log[i].StartsWith("> "))
                GUI.color = Color.green;
            else
                GUI.color = Color.white;
            GUI.Label(new Rect(0, i * Mathf.FloorToInt(height * 0.10f), width, 0), log[i], textFieldStyle);
            GUI.color = originalColor;
        }
        GUI.EndScrollView();

        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, Color.black);
        backgroundTexture.Apply();
        GUIStyle customStyle = textFieldStyle;
        customStyle.focused.background = backgroundTexture;

        var moveToEnd = false;

        if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.UpArrow) //TODO: We should avoid doing inputs in such way, New Input System would be better.
        {
            moveToEnd = NextHistory(-1) || moveToEnd;
            Event.current.Use();
        }
        if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.DownArrow)
        {
            moveToEnd = NextHistory(1) || moveToEnd;
            Event.current.Use();
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
        {
            moveToEnd = AutoComplete() || moveToEnd;
            Event.current.Use();
        }

        GUI.SetNextControlName("InputField");
        input = GUI.TextField(textFieldRect, input, customStyle);

        if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                Log($"> {input.ToLower()}");
                ExecuteCommand(input);
                input = string.Empty;
            }
            Event.current.Use();
        }

        if (moveToEnd)
        {
            TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            editor.cursorIndex = input.Length;
            editor.selectIndex = input.Length;
        }

        if (grabFocus)
        {
            grabFocus = false;
            GUI.FocusControl("InputField");
        }
    }

}

}