using System.Collections.Generic;
using Adrenak.UniVoice.Samples;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
public class VoiceChatController : NetworkBehaviour
{
    private PlayerInputHandler inputHandler;
    private bool isTalking;
    public AudioSource audioSource;
    [SyncVar(hook = nameof(OnPlayerIdChanged))]
    private int playerId = -1;
    public System.Action<int> OnPlayerIdUpdated;
    void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        inputHandler.OnTalkStarted.AddListener(StartTalking);
        inputHandler.OnTalkStopped.AddListener(StopTalking);
        var clientSession = UniVoiceMirrorSetupSample.ClientSession;
        clientSession.InputEnabled = false;
        isTalking = false;
    }

    void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnTalkStarted.RemoveListener(StartTalking);
            inputHandler.OnTalkStopped.RemoveListener(StopTalking);
        }
        OnPlayerIdUpdated = null;
    }
    void Update()
    {
        if (audioSource != null)
        {
            audioSource.transform.position = transform.position;
        }
    }
    public override void OnStartServer()
    {
        AssignPlayerId();
    }

    public override void OnStartClient()
    {
        if (isLocalPlayer)
        {
            Debug.Log($"Мой ID: {playerId}");
        }
    }

    private void AssignPlayerId()
    {
        int newId = FindAvailableId();
        playerId = newId;
        Debug.Log($"Игроку {netId} назначен ID: {playerId}");
    }

    private int FindAvailableId()
    {
        HashSet<int> usedIds = new HashSet<int>();

        foreach (var player in FindObjectsOfType<VoiceChatController>())
        {
            if (player != this && player.playerId >= 0)
            {
                usedIds.Add(player.playerId);
            }
        }

        int candidateId = 0;
        while (usedIds.Contains(candidateId))
        {
            candidateId++;
        }

        return candidateId;
    }

    private void OnPlayerIdChanged(int oldId, int newId)
    {
        playerId = newId;
        OnPlayerIdUpdated?.Invoke(newId);

        if (isLocalPlayer)
        {
            Debug.Log($"Мой ID изменился на: {newId}");
        }
    }

    public int GetPlayerId()
    {
        return playerId;
    }
    public void SetAudioSource(AudioSource source)
    {
        audioSource = source;
    }
    private void StartTalking()
    {
        if (!UniVoiceMirrorSetupSample.HasSetUp)
            return;

        var clientSession = UniVoiceMirrorSetupSample.ClientSession;
        if (clientSession != null && !isTalking)
        {
            clientSession.InputEnabled = true;
            isTalking = true;
        }
    }

    private void StopTalking()
    {
        if (!UniVoiceMirrorSetupSample.HasSetUp)
            return;

        var clientSession = UniVoiceMirrorSetupSample.ClientSession;
        if (clientSession != null && isTalking)
        {
            clientSession.InputEnabled = false;
            isTalking = false;
        }
    }

    public bool IsTalking()
    {
        return isTalking;
    }
}