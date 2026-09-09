using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Samples;
using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class VoiceChat3DSetUp : MonoBehaviour
{
    private Dictionary<int, bool> assignedPeers = new Dictionary<int, bool>();

    void Start()
    {
        StartCoroutine(SetupWhenReady());
    }

    private bool subscribed;

    private System.Collections.IEnumerator SetupWhenReady()
    {
        while (UniVoiceMirrorSetupSample.ClientSession == null)
            yield return null;

        UniVoiceMirrorSetupSample.ClientSession.OutputFactory =
            new SafeStreamedAudioSourceOutput.Factory();

        var client = UniVoiceMirrorSetupSample.ClientSession.Client;
        if (client != null && !subscribed)
        {
            client.OnPeerJoined += SetUpPeer;
            subscribed = true;
        }
    }

    void OnDestroy()
    {
        var session = UniVoiceMirrorSetupSample.ClientSession;
        if (session == null || session.Client == null) return;
        session.Client.OnPeerJoined -= SetUpPeer;
    }

    private void SetUpPeer(int peerId)
    {
        if (!gameObject.activeInHierarchy) return;
        Invoke(nameof(GetAllPeers), 1f);
    }

    private void GetAllPeers()
    {
        var client = UniVoiceMirrorSetupSample.ClientSession.Client;
        if (client == null) return;

        foreach (var peerId in client.PeerIDs)
        {
            if (!assignedPeers.ContainsKey(peerId))
            {
                Setup3DForPeer(peerId);
            }
        }
    }

    private void Setup3DForPeer(int peerId)
    {
        var session = UniVoiceMirrorSetupSample.ClientSession;
        if (session == null || session.Client == null) return;

        var localNetId = NetworkClient.connection.identity.netId;
        if (peerId == (int)localNetId) return;

        if (session.PeerOutputs == null ||
            !session.PeerOutputs.TryGetValue(peerId, out var outValue))
        {
            return;
        }

        StreamedAudioSource stream = GetStream(outValue);
        if (stream == null) return;
        if (stream.UnityAudioSource == null) return;

        var audioSource = stream.UnityAudioSource;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        foreach (var controller in FindObjectsOfType<VoiceChatController>())
        {
            if (IsLocalPlayer(controller.gameObject) || controller.audioSource != null) continue;
            if (controller.GetPlayerId() == peerId)
            {
                controller.SetAudioSource(audioSource);
                assignedPeers[peerId] = true;
                Debug.Log($"Назначен AudioSource для {controller.name} (peerId: {peerId})");
                break;
            }
        }
    }

    private static StreamedAudioSource GetStream(IAudioOutput output)
    {
        if (output is SafeStreamedAudioSourceOutput safe)
            return safe.Stream;
        if (output is StreamedAudioSourceOutput legacy)
            return legacy.Stream;
        return null;
    }

    private bool IsLocalPlayer(GameObject player)
    {
        var networkIdentity = player.GetComponent<NetworkIdentity>();
        if (networkIdentity == null) return false;
        return networkIdentity.isLocalPlayer;
    }
}