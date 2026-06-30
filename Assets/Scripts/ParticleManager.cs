using UnityEngine;

public class ParticleManager
{
    public void PlayParticle(string folderName, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = Resources.Load<GameObject>(folderName);
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
        GameObject prefab = Resources.Load<GameObject>(folderName);
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
        GameObject prefab = Resources.Load<GameObject>(folderName);
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