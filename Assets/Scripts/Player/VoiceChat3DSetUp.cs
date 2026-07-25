using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Samples;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceChat3DSetUp : MonoBehaviour
{
    private Dictionary<int, bool> assignedPeers = new Dictionary<int, bool>();

    void Start()
    {
        var client = UniVoiceMirrorSetupSample.ClientSession.Client;
        client.OnPeerJoined += SetUpPeer;
    }

    void OnDestroy()
    {
        var client = UniVoiceMirrorSetupSample.ClientSession.Client;
        if (client != null)
            client.OnPeerJoined -= Setup3DForPeer;
    }

    private void SetUpPeer(int peerId)
    {
        Invoke(nameof(GetAllPeers), 1f);
    }

    private void GetAllPeers()
    {
        var client = UniVoiceMirrorSetupSample.ClientSession.Client;
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
        var localNetId = NetworkClient.connection.identity.netId;
        if (peerId == (int)localNetId) return;

        var output = UniVoiceMirrorSetupSample.ClientSession.PeerOutputs[peerId] as StreamedAudioSourceOutput;
        if (output == null) return;

        var audioSource = output.Stream.UnityAudioSource;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        var controllers = FindObjectsOfType<VoiceChatController>();

        foreach (var controller in controllers)
        {
            if (IsLocalPlayer(controller.gameObject) || controller.audioSource != null) continue;
            if (controller.GetPlayerId()== peerId)
            {
                controller.SetAudioSource(audioSource);
                assignedPeers[peerId] = true;
                Debug.Log($"Назначен AudioSource для {controller.name} (peerId: {peerId})");
                break;
            }
        }
    }

    private bool IsLocalPlayer(GameObject player)
    {
        var networkIdentity = player.GetComponent<NetworkIdentity>();
        if (networkIdentity == null) return false;
        return networkIdentity.isLocalPlayer;
    }
}