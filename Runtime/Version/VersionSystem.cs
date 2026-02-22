using UnityEngine;

namespace Sugared
{

public class VersionSystem : MonoBehaviour
{

    public static BuildVersion version { get; private set; }
    static bool showGUI = true;

    public static void ShowVersion()
    {
        showGUI = !showGUI;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var cloud = GameObject.Find("Cloud");
        if (cloud == null)
        {
            cloud = new GameObject("Cloud");
            DontDestroyOnLoad(cloud);
        }
        cloud.AddComponent<VersionSystem>();

        version = Resources.Load<BuildVersion>("ActiveVersion");

        if (version == null)
            Debug.LogError("Game Version => ActiveVersion.asset not found in Resources folder. (Assets/Plugins/MultiLineBuilder/Resources)");
    }

    GUIStyle versionStyle;
    Font customFont;
    void Awake()
    {
        customFont = Resources.Load<Font>("GUI");
        if (customFont == null)
            Debug.LogWarning("Version Indication => GUI Font not found in Assets/Plugins/MultiLineBuilder/Resources/GUI. Using default GUI font.");

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
        if (!showGUI || version == null || version.type != BuildVersion.ProjectType.Playtest)
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