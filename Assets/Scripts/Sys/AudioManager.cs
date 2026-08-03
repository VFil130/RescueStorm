using UnityEngine;

public class AudioManager
{
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private float masterVolume = 1f;

    public AudioManager()
    {
        GameObject audioRoot = new GameObject("AudioManager");
        Object.DontDestroyOnLoad(audioRoot);

        musicSource = audioRoot.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSource = audioRoot.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 1f;
        sfxSource.minDistance = 1f;
        sfxSource.maxDistance = 50f;
        sfxSource.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    public void PlaySFX(string folderName, Vector3 position, float volume = 1f)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(folderName);
        if (clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            float finalVolume = volume * masterVolume;
            AudioSource.PlayClipAtPoint(clip, position, finalVolume);
        }
    }

    public void PlaySFX(string folderName, float volume = 1f)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(folderName);
        if (clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            sfxSource.volume = volume * masterVolume;
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayMusic(string folderName, float volume = 1f)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(folderName);
        if (clips != null && clips.Length > 0)
        {
            musicSource.clip = clips[0];
            musicSource.volume = volume * masterVolume;
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicSource.volume / (masterVolume + 0.001f) * masterVolume;
    }
}