using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

public class FindAllPlayers : MonoBehaviour
{
    public static FindAllPlayers instance;
    public void Start()
    {
        instance = this;
    }
    [ContextMenu("Найти игроков")]
    public void FindPlayers()
    {
        var players = FindObjectsOfType<VoiceChatController>();

        Debug.Log($"Игроков: {players.Length}");
        foreach (var p in players)
        {
            Debug.Log($"{p.name} | VoiceChat: {p.GetComponent<VoiceChatController>() != null}");
        }
    }
}