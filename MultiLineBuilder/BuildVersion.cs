using UnityEngine;

[CreateAssetMenu(fileName = "Build Version #", menuName = "Builds/Build Version")]
public class BuildVersion : ScriptableObject
{

    public string title = "Project";
    public string fileName = "Application";
    public enum ProjectType { Vanilla, Demo, Playtest, Showcase }
    public ProjectType type;
    public uint steamAPI;
    [HideInInspector] public bool steamDeck = false;
    [HideInInspector] public bool buildEnabled = true;

}