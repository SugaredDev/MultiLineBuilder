using UnityEngine;

namespace MultiClaw
{

[CreateAssetMenu(fileName = "Build Version #", menuName = "Builds/Build Version")]
public class GameVersion : ScriptableObject
{

    public string title = "Project";
    public string fileName = "Application";
    public bool debug = true;
    public uint steamAPI;
    [HideInInspector] public bool steamDeck = false;
    [HideInInspector] public bool buildEnabled = true;

}

}