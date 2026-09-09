using Adrenak.UniMic;
using Adrenak.UniVoice;
using UnityEngine;

public sealed class SafeStreamedAudioSourceOutput : MonoBehaviour, IAudioOutput
{
    public StreamedAudioSource Stream { get; private set; }
    private bool disposed;

    private void Awake()
    {
        Stream = GetComponent<StreamedAudioSource>();
    }

    public void Feed(AudioFrame frame)
    {
        Stream.Feed(frame.frequency, frame.channelCount, Utils.Bytes.BytesToFloats(frame.samples));
    }

    public void Dispose()
    {
        if (disposed || this == null) return;
        disposed = true;
        Destroy(gameObject);
    }

    public class Factory : IAudioOutputFactory
    {
        public IAudioOutput Create()
        {
            var go = new GameObject("SafeStreamedAudioSourceOutput");
            DontDestroyOnLoad(go);
            go.AddComponent<StreamedAudioSource>();
            return go.AddComponent<SafeStreamedAudioSourceOutput>();
        }
    }
}