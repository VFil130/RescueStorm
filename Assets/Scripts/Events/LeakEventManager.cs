using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeakEventManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject leakPrefab;
    [SerializeField, Min(1f)] private float minimumInterval = 90f;
    [SerializeField, Min(1f)] private float maximumInterval = 180f;
    [SerializeField, Min(1)] private int maximumActiveLeaks = 3;

    private readonly Dictionary<LeakHazard, LeakSpawnPoint> activeLeaks =
        new Dictionary<LeakHazard, LeakSpawnPoint>();
    private readonly List<LeakSpawnPoint> allSpawnPoints = new List<LeakSpawnPoint>();
    private readonly Queue<LeakSpawnPoint> spawnBag = new Queue<LeakSpawnPoint>();

    private Coroutine scheduleCoroutine;
    private System.Random random;
    private bool sessionRunning;
    private bool localSession;

    private bool HasStateAuthority =>
        CanMutateSessionState(NetworkServer.active, localSession);

    public GameObject LeakPrefab => leakPrefab;
    public int ActiveLeakCount => activeLeaks.Count;
    public int SpawnPointCount => allSpawnPoints.Count;

    private void Awake()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
    }

    public void BeginSession(DungeonGenerationResult result)
    {
        if (NetworkClient.active && !NetworkServer.active) return;
        if (result == null) return;

        localSession = !NetworkServer.active;
        ClearSessionState();
        allSpawnPoints.AddRange(result.Modules
            .Where(module => module != null && module.ModuleKind == RoomModuleKind.Corridor)
            .SelectMany(ResolveSpawnPoints)
            .Where(point => point != null)
            .Distinct());

        if (leakPrefab == null)
        {
            Debug.LogError("Leak event manager has no leak prefab.", this);
            return;
        }

        if (allSpawnPoints.Count == 0)
        {
            Debug.LogError("Generated dungeon has no configured leak spawn points.", this);
            return;
        }

        random = new System.Random(unchecked(result.Seed ^ 0x4C45414B));
        sessionRunning = true;
        UpdateActiveLeakCount();
        scheduleCoroutine = StartCoroutine(RunSchedule());
    }

    private static IEnumerable<LeakSpawnPoint> ResolveSpawnPoints(RoomData module)
    {
        IReadOnlyList<LeakSpawnPoint> configuredPoints = module.GetLeakSpawnPoints();
        if (configuredPoints.Count > 0)
            return configuredPoints;

        // Room prefabs maintained by the generation team already expose vent markers.
        // Treat those markers as the corridor leak locations when dedicated leak
        // markers have not been authored, so replacing the room pool cannot silently
        // disable the event system.
        return module.VentSpawnPoints
            .Where(point => point != null)
            .Select(point => point.GetComponent<LeakSpawnPoint>() ??
                             point.gameObject.AddComponent<LeakSpawnPoint>())
            .ToArray();
    }

    public void StopSession()
    {
        if (!HasStateAuthority) return;

        ClearSessionState();
        localSession = false;
    }

    private void ClearSessionState()
    {
        sessionRunning = false;
        if (scheduleCoroutine != null)
        {
            StopCoroutine(scheduleCoroutine);
            scheduleCoroutine = null;
        }

        foreach (LeakHazard leak in activeLeaks.Keys.ToList())
            DestroyLeak(leak);

        activeLeaks.Clear();
        allSpawnPoints.Clear();
        spawnBag.Clear();
        UpdateActiveLeakCount();
    }

    public void NotifyLeakRepaired(LeakHazard leak)
    {
        if (!HasStateAuthority || leak == null) return;

        activeLeaks.Remove(leak);
        UpdateActiveLeakCount();
        DestroyLeak(leak);
    }

    public void NotifyLeakDestroyed(LeakHazard leak)
    {
        if (!HasStateAuthority || leak == null) return;
        if (!activeLeaks.Remove(leak)) return;

        UpdateActiveLeakCount();
    }

    private IEnumerator RunSchedule()
    {
        while (sessionRunning && gameManager != null && gameManager.IsGameActive())
        {
            PruneDestroyedLeaks();

            while (sessionRunning &&
                   gameManager.IsGameActive() &&
                   activeLeaks.Count >= maximumActiveLeaks)
            {
                yield return null;
                PruneDestroyedLeaks();
            }

            if (!sessionRunning || !gameManager.IsGameActive()) break;

            float remaining = NextIntervalSeconds(random, minimumInterval, maximumInterval);
            while (sessionRunning && gameManager.IsGameActive() && remaining > 0f)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }

            if (!sessionRunning || !gameManager.IsGameActive()) break;
            SpawnNextLeak();
        }

        sessionRunning = false;
        scheduleCoroutine = null;
        if (!sessionRunning || gameManager == null || !gameManager.IsGameActive())
            CleanupActiveLeaks();
    }

    private bool SpawnNextLeak()
    {
        LeakSpawnPoint point = TakeNextSpawnPoint();
        if (point == null) return false;

        GameObject instance = Instantiate(
            leakPrefab,
            point.Position,
            point.Rotation);
        LeakHazard leak = instance.GetComponent<LeakHazard>();
        if (leak == null)
        {
            Debug.LogError("Leak prefab needs a LeakHazard component.", instance);
            Destroy(instance);
            return false;
        }


        if (NetworkServer.active)
            NetworkServer.Spawn(instance);

        leak.Initialize(this);
        activeLeaks[leak] = point;

        UpdateActiveLeakCount();
        gameManager?.NotifyLeakSpawned();
        return true;
    }

    private LeakSpawnPoint TakeNextSpawnPoint()
    {
        if (spawnBag.Count == 0) RefillSpawnBag();

        while (spawnBag.Count > 0)
        {
            LeakSpawnPoint candidate = spawnBag.Dequeue();
            if (candidate != null && !activeLeaks.ContainsValue(candidate))
                return candidate;
        }

        return null;
    }

    private void RefillSpawnBag()
    {
        List<LeakSpawnPoint> candidates = allSpawnPoints
            .Where(point => point != null && !activeLeaks.ContainsValue(point))
            .ToList();

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (candidates[i], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[i]);
        }

        foreach (LeakSpawnPoint candidate in candidates)
            spawnBag.Enqueue(candidate);
    }

    private void PruneDestroyedLeaks()
    {
        foreach (LeakHazard leak in activeLeaks.Keys
                     .Where(leak => leak == null)
                     .ToList())
        {
            activeLeaks.Remove(leak);
        }

        UpdateActiveLeakCount();
    }

    private void CleanupActiveLeaks()
    {
        foreach (LeakHazard leak in activeLeaks.Keys.ToList())
            DestroyLeak(leak);

        activeLeaks.Clear();
        spawnBag.Clear();
        UpdateActiveLeakCount();
    }

    private static void DestroyLeak(LeakHazard leak)
    {
        if (leak == null) return;

        if (NetworkServer.active)
            NetworkServer.Destroy(leak.gameObject);
        else
            Destroy(leak.gameObject);
    }

    private void UpdateActiveLeakCount()
    {
        gameManager?.SetActiveLeakCount(activeLeaks.Count);
    }

    public static float NextIntervalSeconds(
        System.Random source,
        float minimum,
        float maximum)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        float min = Mathf.Max(1f, Mathf.Min(minimum, maximum));
        float max = Mathf.Max(min, Mathf.Max(minimum, maximum));
        return min + (float)source.NextDouble() * (max - min);
    }

    public static bool CanMutateSessionState(bool serverActive, bool localSession)
    {
        return serverActive || localSession;
    }

    private void OnDestroy()
    {
        if (HasStateAuthority) StopSession();
    }
}
