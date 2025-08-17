using UnityEngine;

[CreateAssetMenu(fileName = "Project Version #", menuName = "Builds/Project Version")]
public class ProjectVersion : ScriptableObject
{

    public string title = "Project";
    public enum ProjectType { Vanilla, Demo, Playtest }
    public ProjectType type;
    public uint steamID;
    [HideInInspector] public bool SteamDeck = false;

}