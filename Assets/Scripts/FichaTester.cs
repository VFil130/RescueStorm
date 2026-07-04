using System.Collections;
using UnityEngine;
using Zenject;

public class FichaTester : MonoBehaviour
{
    [Inject] private AudioManager audioManager;
    [Inject] private ParticleManager particleManager;

    private int testCounter = 0;

    void Start()
    {
        StartCoroutine(RunTestsRoutine());
    }

    IEnumerator RunTestsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            testCounter++;
            Debug.Log($"=== TEST #{testCounter} ===");
            RunAudioTests();
            RunParticleTests();

            if (testCounter % 3 == 0)
            {
                float newVolume = Random.Range(0.2f, 1f);
                audioManager.SetMasterVolume(newVolume);
                Debug.Log($"Volume changed to: {newVolume:F2}");
            }

            if (testCounter % 2 == 0)
            {
                audioManager.StopMusic();
                Debug.Log("Music stopped");
            }
            else
            {
                audioManager.PlayMusic("Music", 0.5f);
                Debug.Log("Music started");
            }
        }
    }

    void RunAudioTests()
    {
        Vector3 randomPos = new Vector3(
            Random.Range(-5f, 5f),
            Random.Range(0f, 3f),
            Random.Range(-5f, 5f)
        );
        audioManager.PlaySFX("SFX", randomPos, Random.Range(0.3f, 0.9f));
        Debug.Log($"SFX with position: {randomPos}");
        audioManager.PlaySFX("SFX", Random.Range(0.3f, 0.9f));
        Debug.Log("SFX without position");
    }

    void RunParticleTests()
    {
        particleManager.PlayParticle("Particles/1", Vector3.zero);
    }

    void OnDestroy()
    {
        if (audioManager != null)
        {
            audioManager.StopMusic();
        }
    }
}