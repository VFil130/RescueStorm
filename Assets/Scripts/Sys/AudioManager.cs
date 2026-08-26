using UnityEngine;

public class AudioManager
{
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private float masterVolume = 1f;
    private float musicVolume = 1f;
    private float sfxVolume = 1f;
    private float currentMusicVolume = 1f;

    public AudioManager()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

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
            float finalVolume = volume * masterVolume * sfxVolume;
            AudioSource.PlayClipAtPoint(clip, position, finalVolume);
        }
    }

    public void PlaySFX(string folderName, float volume = 1f)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(folderName);
        if (clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            sfxSource.volume = volume * masterVolume * sfxVolume;
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayMusic(string folderName, float volume = 1f)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(folderName);
        if (clips != null && clips.Length > 0)
        {
            musicSource.clip = clips[0];
            currentMusicVolume = volume;
            RefreshMusicVolume();
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
        RefreshMusicVolume();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        RefreshMusicVolume();
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    private void RefreshMusicVolume()
    {
        musicSource.volume = currentMusicVolume * masterVolume * musicVolume;
    }
}