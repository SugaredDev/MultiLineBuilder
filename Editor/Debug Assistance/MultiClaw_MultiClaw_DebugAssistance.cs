using UnityEditor;
using UnityEngine;
using System.IO;

namespace MultiClaw
{

public class DebugAssistance : EditorWindow
{

    Font chosenFont;

    [MenuItem("Tools/MultiClaw/Debug Assistance")]
    static void ShowWindow()
    {
        GetWindow<DebugAssistance>("MultiClaw | Debug Assistance");
    }

    void OnEnable()
    {
        LoadCurrentFont();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Version Indicator Font", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        Font resourceFont = Resources.Load<Font>(Constants.Resources_Indicator);

        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("Change Font for Version Indication:", EditorStyles.label);
        chosenFont = (Font)EditorGUILayout.ObjectField(chosenFont, typeof(Font), false);
        
        EditorGUILayout.Space();
        
        GUI.enabled = chosenFont != null;
        GUI.backgroundColor = resourceFont != null ? Color.green : Color.red;
        
        if (GUILayout.Button("Update Indicator Font"))
        {
            UpdateFont();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        if (IsFontSetCorrectly())
        {
            EditorGUILayout.Space(5);
            GUI.color = Color.green;
            EditorGUILayout.LabelField("✓ Font is set correctly", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
        
        EditorGUILayout.EndVertical();
        
        if (resourceFont == null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("No Indicator font is currently set. The version indicator will use the default Unity font.", MessageType.Warning);
        }
    }

    void LoadCurrentFont()
    {
        chosenFont = Resources.Load<Font>(Constants.Resources_Indicator);
    }

    bool IsFontSetCorrectly()
    {
        if (chosenFont == null)
            return false;

        Font resourceFont = Resources.Load<Font>(Constants.Resources_Indicator);
        if (resourceFont == null)
            return false;

        string chosenPath = AssetDatabase.GetAssetPath(chosenFont);
        string resourcePath = AssetDatabase.GetAssetPath(resourceFont);

        return chosenPath == resourcePath;
    }

    void UpdateFont()
    {
        if (chosenFont == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a font first.", "OK");
            return;
        }

        if (!Directory.Exists(Constants.Path_Indicator))
            Directory.CreateDirectory(Constants.Path_Indicator);

        string sourcePath = AssetDatabase.GetAssetPath(chosenFont);
        
        if (string.IsNullOrEmpty(sourcePath))
        {
            EditorUtility.DisplayDialog("Error", "Could not find the selected font asset.", "OK");
            return;
        }

        string extension = Path.GetExtension(sourcePath);
        string destinationPath = Constants.Path_Indicator + "/Indicator" + extension;

        if (File.Exists(destinationPath))
            AssetDatabase.DeleteAsset(destinationPath);

        AssetDatabase.CopyAsset(sourcePath, destinationPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", "Indicator font updated successfully!", "OK");
    }

}

}
