using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class Scenes : EditorWindow
{

    [MenuItem("Tools/MultiClaw/Scenes Switcher")]
    public static void ShowWindow()
    {
        GetWindow<Scenes>("MultiClaw | Scenes Switcher");
    }

    void OpenScene(string path)
    {
        if (Application.isPlaying)
            SceneManager.LoadScene(Path.GetFileNameWithoutExtension(path));
        else
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }
    }

    Vector2 scrollPosition;
    void OnGUI()
    {
        GUILayout.Label("Scenes", EditorStyles.boldLabel);
        GUILayout.Space(10);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        var scenes = EditorBuildSettings.scenes;

        if (scenes.Length == 0)
            EditorGUILayout.HelpBox("No scenes found in Build Settings.", MessageType.Warning);

        string activeScenePath = EditorSceneManager.GetActiveScene().path;

        foreach (var scene in scenes)
        {
            if (!scene.enabled) continue;

            string scenePath = scene.path;
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);

            Color originalColor = GUI.backgroundColor;
            if (scenePath == activeScenePath)
                GUI.backgroundColor = Color.green;

            if (GUILayout.Button(sceneName))
                OpenScene(scenePath);

            GUI.backgroundColor = originalColor;
        }

        GUILayout.EndScrollView();
    }

}