using System.Collections;
using System.Collections.Generic;
using Adrenak.UniVoice;
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
    private void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        inputHandler.OnTalkStarted.AddListener(StartTalking);
        inputHandler.OnTalkStopped.AddListener(StopTalking);
        isTalking = false;
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnTalkStarted.RemoveListener(StartTalking);
            inputHandler.OnTalkStopped.RemoveListener(StopTalking);
        }
        OnPlayerIdUpdated = null;
    }
    private void Update()
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

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        StartCoroutine(DisableVoiceInputWhenReady());
    }

    private IEnumerator DisableVoiceInputWhenReady()
    {
        while (!IsVoiceSessionReady)
        {
            yield return null;
        }

        UniVoiceMirrorSetupSample.ClientSession.InputEnabled = false;
        isTalking = false;
    }

    private void AssignPlayerId()
    {
        playerId = connectionToClient != null ? connectionToClient.connectionId : FindAvailableId();
        Debug.Log($"Игроку {netId} назначен ID: {playerId}");
    }

    private int FindAvailableId()
    {
        HashSet<int> usedIds = new HashSet<int>();

        foreach (VoiceChatController player in FindObjectsByType<VoiceChatController>(FindObjectsSortMode.None))
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
        audioSource.transform.SetParent(transform);
        audioSource.transform.localPosition = Vector3.zero;
    }
    private void StartTalking()
    {
        SetTalking(true);
    }

    private void StopTalking()
    {
        SetTalking(false);
    }

    private void SetTalking(bool talking)
    {
        if (!IsVoiceSessionReady || isTalking == talking) return;

        ClientSession<int> session = UniVoiceMirrorSetupSample.ClientSession;
        session.InputEnabled = talking;
        isTalking = talking;
    }

    public bool IsTalking()
    {
        return isTalking;
    }

    private static bool IsVoiceSessionReady =>
        UniVoiceMirrorSetupSample.HasSetUp && UniVoiceMirrorSetupSample.ClientSession != null;
}
