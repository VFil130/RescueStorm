using System.Collections.Generic;
using UnityEngine;

public class VentManager : MonoBehaviour
{
    public static VentManager Instance { get; private set; }

    private readonly List<Vent> vents = new List<Vent>();

    public int VentCount => vents.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(Vent vent)
    {
        if (vent != null && !vents.Contains(vent))
            vents.Add(vent);
    }

    public void Unregister(Vent vent)
    {
        vents.Remove(vent);
    }

    public Vent GetRandomVent(Vent exclude = null)
    {
        if (vents.Count <= 1) return null;

        List<Vent> candidates = new List<Vent>();
        foreach (Vent v in vents)
        {
            if (v != null && v != exclude)
                candidates.Add(v);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public Vent GetClosestVent(Vector3 position, Vent exclude = null)
    {
        Vent closest = null;
        float minDist = float.MaxValue;

        foreach (Vent v in vents)
        {
            if (v == null || v == exclude) continue;
            float dist = Vector3.Distance(position, v.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = v;
            }
        }

        return closest;
    }

    public Vent GetVentFarFromPlayers(float minPlayerDistance)
    {
        if (vents.Count == 0) return null;

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        List<Vent> safeVents = new List<Vent>();
        Vent fallback = null;
        float fallbackScore = float.MinValue;

        foreach (Vent v in vents)
        {
            if (v == null) continue;

            float closestPlayerDist = float.MaxValue;
            foreach (Player player in players)
            {
                if (player.Health <= 0) continue;
                float dist = Vector3.Distance(v.transform.position, player.transform.position);
                if (dist < closestPlayerDist)
                    closestPlayerDist = dist;
            }

            if (closestPlayerDist >= minPlayerDistance)
            {
                safeVents.Add(v);
                continue;
            }

            if (closestPlayerDist > fallbackScore)
            {
                fallbackScore = closestPlayerDist;
                fallback = v;
            }
        }

        if (safeVents.Count > 0)
            return safeVents[Random.Range(0, safeVents.Count)];

        return fallback;
    }

    public Vent[] GetAllVents()
    {
        return vents.ToArray();
    }
}
