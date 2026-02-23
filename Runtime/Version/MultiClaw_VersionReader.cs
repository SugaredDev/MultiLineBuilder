using UnityEngine;

namespace MultiClaw
{

public class VersionReader : MonoBehaviour
{

    public static GameVersion version { get; private set; }
    static bool showGUI = true;

    public static void ShowVersion() => showGUI = !showGUI;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var cloud = GameObject.Find("MultiClaw");
        if (cloud == null)
        {
            cloud = new GameObject("MultiClaw");
            DontDestroyOnLoad(cloud);
        }
        
        var versionObject = new GameObject("VersionSystem");
        versionObject.transform.SetParent(cloud.transform);
        versionObject.AddComponent<VersionReader>();

        version = Resources.Load<GameVersion>("ActiveVersion");

        if (version == null)
            Debug.LogError("Game Version => ActiveVersion.asset not found in Resources folder. (Assets/Plugins/MultiClaw/Resources)");
    }

    GUIStyle versionStyle;
    Font customFont;
    void Awake()
    {
        customFont = Resources.Load<Font>("GUI");
        if (customFont == null)
            Debug.LogWarning("Version Indication => GUI Font not found in Assets/Plugins/MultiClaw/Resources/GUI. Using default GUI font.");

        versionStyle = new GUIStyle
        {
            normal = { textColor = new Color(1f, 1f, 1f, 0.1f) },
            alignment = TextAnchor.MiddleCenter,
        };
        
        if (customFont != null)
            versionStyle.font = customFont;
    }

    void OnGUI()
    {
        if (!showGUI || version == null || version.type != GameVersion.ProjectType.Playtest)
            return;

        versionStyle.fontSize = Mathf.Max(5, Screen.height / 50);

        string versionText = $"{version.title}.{Application.version}";

        Vector2 textSize = versionStyle.CalcSize(new GUIContent(versionText));
        float gap = Screen.height * 0.01f;

        float x = (Screen.width - textSize.x) / 2;
        float y = Screen.height - textSize.y - gap;

        GUI.Label(new Rect(x, y, textSize.x, textSize.y), versionText, versionStyle);
    }

}

}