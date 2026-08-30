using System.Collections.Generic;
using UnityEngine;

public class AiCoordinator : MonoBehaviour
{
    public static AiCoordinator Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float updateInterval = 60f;
    [SerializeField] private float isolationBonusRange = 5f;

    private float updateTimer;
    private Transform bestTarget;
    private Vector3 bestTargetPosition;
    private float bestIsolationScore;

    private readonly List<Player> players = new List<Player>();

    public bool HasTarget => bestTarget != null;
    public Vector3 TargetPosition => bestTargetPosition;
    public float IsolationScore => bestIsolationScore;

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

    private void LateUpdate()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0f)
        {
            updateTimer = updateInterval;
            ScanForIsolatedPlayers();
        }
    }

    private void ScanForIsolatedPlayers()
    {
        players.Clear();
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player player in allPlayers)
        {
            if (player.Health > 0)
                players.Add(player);
        }

        bestTarget = null;
        bestIsolationScore = 0f;

        for (int i = 0; i < players.Count; i++)
        {
            Player candidate = players[i];
            float closestAllyDistance = float.MaxValue;

            for (int j = 0; j < players.Count; j++)
            {
                if (i == j) continue;
                float dist = Vector3.Distance(
                    candidate.transform.position,
                    players[j].transform.position);
                if (dist < closestAllyDistance)
                    closestAllyDistance = dist;
            }

            float isolationScore;
            if (players.Count == 1)
            {
                isolationScore = closestAllyDistance;
            }
            else
            {
                isolationScore = closestAllyDistance;
                if (closestAllyDistance > isolationBonusRange)
                    isolationScore *= 1.5f;
            }

            if (isolationScore > bestIsolationScore)
            {
                bestIsolationScore = isolationScore;
                bestTarget = candidate.transform;
                bestTargetPosition = candidate.transform.position;
            }
        }
    }
}
