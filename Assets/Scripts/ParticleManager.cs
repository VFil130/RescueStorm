using System.Collections.Generic;
using UnityEngine;

public class ParticleManager
{
    private const string RootFolder = "Particles/";

    private static readonly Dictionary<string, GameObject> looping = new();
    private static readonly List<string> muted = new();

    public static ParticleManager Instance { get; } = new ParticleManager();

    private static GameObject Load(string name)
    {
        return Resources.Load<GameObject>(RootFolder + name);
    }

    public void ShowLoopingParticle(string folderName, Transform parent)
    {
        if (muted.Contains(folderName)) return;

        if (looping.TryGetValue(folderName, out GameObject existing))
        {
            if (existing != null)
            {
                existing.SetActive(true);
                return;
            }

            looping.Remove(folderName);
        }

        GameObject prefab = Load(folderName);
        if (prefab == null) return;

        looping[folderName] = Object.Instantiate(
            prefab,
            parent != null ? parent.position : Vector3.zero,
            parent != null ? parent.rotation : Quaternion.identity,
            parent);
    }

    public void SetLoopingParticleVisible(string folderName, bool visible)
    {
        if (!looping.TryGetValue(folderName, out GameObject instance) || instance == null) return;
        instance.SetActive(visible);
    }

    public void DestroyLoopingParticle(string folderName)
    {
        if (!looping.TryGetValue(folderName, out GameObject instance)) return;
        looping.Remove(folderName);
        if (instance != null) Object.Destroy(instance);
    }

    public void SetParticleMuted(string folderName, bool isMuted)
    {
        if (isMuted)
        {
            if (!muted.Contains(folderName)) muted.Add(folderName);
            DestroyLoopingParticle(folderName);
        }
        else
        {
            muted.Remove(folderName);
        }
    }

    public void PlayParticle(string folderName, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = Load(folderName);
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab, position, rotation);
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                Object.Destroy(instance, ps.main.duration);
            }
        }
    }

    public void PlayParticle(string folderName, Vector3 position)
    {
        PlayParticle(folderName, position, Quaternion.identity);
    }

    public void PlayParticleOnObject(string folderName, GameObject target)
    {
        GameObject prefab = Load(folderName);
        if (prefab != null && target != null)
        {
            GameObject instance = Object.Instantiate(prefab, target.transform);
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                Object.Destroy(instance, ps.main.duration);
            }
        }
    }

    public void PlayParticleOnObject(string folderName, GameObject target, Vector3 localOffset)
    {
        GameObject prefab = Load(folderName);
        if (prefab != null && target != null)
        {
            GameObject instance = Object.Instantiate(prefab, target.transform);
            instance.transform.localPosition = localOffset;
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                Object.Destroy(instance, ps.main.duration);
            }
        }
    }
}