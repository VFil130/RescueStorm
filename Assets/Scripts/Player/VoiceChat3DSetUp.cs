using Adrenak.UniVoice;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Samples;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceChat3DSetUp : MonoBehaviour
{
    private readonly HashSet<int> configuredPeers = new HashSet<int>();
    private IAudioClient<int> subscribedClient;

    private IEnumerator Start()
    {
        while (!NetworkClient.active)
            yield return null;

        // The persistent menu setup may start before Mirror and UniVoice are ready.
        while (!UniVoiceMirrorSetupSample.HasSetUp || UniVoiceMirrorSetupSample.ClientSession?.Client == null)
        {
            yield return null;
        }

        subscribedClient = UniVoiceMirrorSetupSample.ClientSession.Client;
        subscribedClient.OnPeerJoined += OnPeerJoined;
        ConfigurePeerAudioSources();
        SchedulePeerScan();
    }

    private void OnDestroy()
    {
        CancelInvoke();
        if (subscribedClient != null)
        {
            subscribedClient.OnPeerJoined -= OnPeerJoined;
            subscribedClient = null;
        }
    }

    private void OnPeerJoined(int peerId)
    {
        SchedulePeerScan();
    }

    private void SchedulePeerScan()
    {
        Invoke(nameof(ConfigurePeerAudioSources), 1f);
    }

    private void ConfigurePeerAudioSources()
    {
        ClientSession<int> session = UniVoiceMirrorSetupSample.ClientSession;
        IAudioClient<int> client = session?.Client;
        if (client == null) return;

        foreach (int peerId in client.PeerIDs)
        {
            if (configuredPeers.Contains(peerId)) continue;
            ConfigurePeerAudio(peerId);
        }
    }

    private void ConfigurePeerAudio(int peerId)
    {
        ClientSession<int> session = UniVoiceMirrorSetupSample.ClientSession;
        if (session == null || session.Client == null) return;
        if (peerId == session.Client.ID) return;

        if (!session.PeerOutputs.TryGetValue(peerId, out IAudioOutput peerOutput)) return;
        StreamedAudioSourceOutput output = peerOutput as StreamedAudioSourceOutput;
        if (output == null) return;

        AudioSource audioSource = output.Stream.UnityAudioSource;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        VoiceChatController[] controllers = FindObjectsByType<VoiceChatController>(FindObjectsSortMode.None);

        foreach (VoiceChatController controller in controllers)
        {
            if (IsLocalPlayer(controller.gameObject) || controller.audioSource != null) continue;
            if (controller.GetPlayerId() == peerId)
            {
                controller.SetAudioSource(audioSource);
                configuredPeers.Add(peerId);
                Debug.Log($"Назначен AudioSource для {controller.name} (peerId: {peerId})");
                break;
            }
        }
    }

    private static bool IsLocalPlayer(GameObject player)
    {
        NetworkIdentity networkIdentity = player.GetComponent<NetworkIdentity>();
        return networkIdentity != null && networkIdentity.isLocalPlayer;
    }
}
