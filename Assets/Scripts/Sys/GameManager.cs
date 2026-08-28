using Mirror;
using UnityEngine;
using Zenject;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float matchDuration = 300f;
    [SerializeField, Min(0f)] private float leakTimeDrainPerLeak = 0.5f;
    [SerializeField, Min(1f)] private float maximumTimeDrainMultiplier = 2.5f;

    [Header("Enemy Spawning")]
    [SerializeField] private DungeonNetworkSessionCoordinator sessionCoordinator;
    [SerializeField, Min(0f)] private float enemySpawnDelay = 120f;
    [SerializeField, Min(1f)] private float enemyMinPlayerDistance = 12f;

    [SyncVar(hook = nameof(OnTimeLeftChanged))]
    private float timeLeft;
    [SyncVar(hook = nameof(OnSurvivorsSavedChanged))]
    private int survivorsSaved;
    [SyncVar(hook = nameof(OnTotalSurvivorsChanged))]
    private int totalSurvivors;
    [SyncVar] private bool isGameActive;
    [SyncVar] private bool isGameOver;
    [SyncVar(hook = nameof(OnActiveLeakCountChanged))]
    private int activeLeakCount;

    private float matchTimer;
    private float enemySpawnTimer;
    private bool enemySpawned;

    [Inject] private AudioManager audioManager;
    [Inject] private ParticleManager particleManager;

    public event System.Action<float> OnTimeUpdated;
    public event System.Action<int, int> OnSurvivorsUpdated;
    public event System.Action<bool> OnGameEnded;
    public event System.Action<int> OnActiveLeaksUpdated;
    public event System.Action OnLeakSpawned;

    private bool HasStateAuthority =>
        isServer || (!NetworkServer.active && !NetworkClient.active);

    public void StartMatch(int survivorCount)
    {
        if (!HasStateAuthority) return;
        if (survivorCount <= 0)
        {
            Debug.LogError("A match cannot start without spawned survivors.", this);
            return;
        }

        matchTimer = matchDuration;
        timeLeft = matchDuration;
        survivorsSaved = 0;
        totalSurvivors = survivorCount;
        activeLeakCount = 0;
        isGameActive = true;
        isGameOver = false;
        enemySpawnTimer = enemySpawnDelay;
        enemySpawned = sessionCoordinator == null;
        if (NetworkServer.active)
            RpcOnMatchStarted(survivorCount);
        else
        {
            OnSurvivorsUpdated?.Invoke(0, survivorCount);
            OnTimeUpdated?.Invoke(timeLeft);
            OnActiveLeaksUpdated?.Invoke(0);
        }
    }

    private void Update()
    {
        if (!HasStateAuthority || !isGameActive || isGameOver) return;

        matchTimer -= Time.deltaTime * GetTimeDrainMultiplier();
        timeLeft = Mathf.Max(0f, matchTimer);
        if (!NetworkServer.active)
            OnTimeUpdated?.Invoke(timeLeft);

        TickEnemySpawn();

        if (matchTimer <= 0f) EndMatch(false);
    }

    private void TickEnemySpawn()
    {
        if (enemySpawned || !NetworkServer.active) return;

        enemySpawnTimer -= Time.deltaTime;
        if (enemySpawnTimer <= 0f)
        {
            enemySpawned = true;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (!NetworkServer.active) return;
        if (sessionCoordinator == null)
        {
            Debug.LogError("Session coordinator is not assigned; cannot spawn the enemy.", this);
            return;
        }

        sessionCoordinator.SpawnEnemy(enemyMinPlayerDistance);
    }

    public void AddSavedSurvivor()
    {
        if (!HasStateAuthority || !isGameActive || isGameOver) return;

        survivorsSaved++;
        if (NetworkServer.active)
            RpcOnSurvivorSaved();
        if (survivorsSaved >= totalSurvivors) EndMatch(true);
    }

    public void EndMatch(bool victory)
    {
        if (!HasStateAuthority || isGameOver) return;

        isGameActive = false;
        isGameOver = true;
        SetActiveLeakCount(0);
        if (NetworkServer.active)
            RpcOnMatchEnded(victory);
        else
            OnGameEnded?.Invoke(victory);
    }

    public void SetActiveLeakCount(int count)
    {
        if (!HasStateAuthority) return;

        activeLeakCount = Mathf.Max(0, count);
        if (!NetworkServer.active)
            OnActiveLeaksUpdated?.Invoke(activeLeakCount);
    }

    public void NotifyLeakSpawned()
    {
        if (!HasStateAuthority) return;

        if (NetworkServer.active)
            RpcOnLeakSpawned();
        else
            OnLeakSpawned?.Invoke();
    }

    [ClientRpc]
    private void RpcOnMatchStarted(int total)
    {
        OnSurvivorsUpdated?.Invoke(0, total);
    }

    [ClientRpc]
    private void RpcOnSurvivorSaved()
    {
        audioManager?.PlaySFX("SurvivorSaved", Vector3.zero);
        particleManager?.PlayParticle("SavedParticle", Vector3.zero);
    }

    [ClientRpc]
    private void RpcOnMatchEnded(bool victory)
    {
        OnGameEnded?.Invoke(victory);
        if (victory)
        {
            audioManager?.PlayMusic("Victory");
            particleManager?.PlayParticle("VictoryParticle", Vector3.zero);
        }
        else
        {
            audioManager?.PlaySFX("Defeat", Vector3.zero);
        }
    }

    [ClientRpc]
    private void RpcOnLeakSpawned()
    {
        OnLeakSpawned?.Invoke();
    }

    private void OnTimeLeftChanged(float _, float newValue)
    {
        OnTimeUpdated?.Invoke(newValue);
    }

    private void OnSurvivorsSavedChanged(int _, int newValue)
    {
        OnSurvivorsUpdated?.Invoke(newValue, totalSurvivors);
    }

    private void OnTotalSurvivorsChanged(int _, int newValue)
    {
        OnSurvivorsUpdated?.Invoke(survivorsSaved, newValue);
    }

    private void OnActiveLeakCountChanged(int _, int newValue)
    {
        OnActiveLeaksUpdated?.Invoke(newValue);
    }

    public static float CalculateTimeDrainMultiplier(
        int leakCount,
        float drainPerLeak = 0.5f,
        float maximumMultiplier = 2.5f)
    {
        return Mathf.Min(
            Mathf.Max(1f, maximumMultiplier),
            1f + Mathf.Max(0, leakCount) * Mathf.Max(0f, drainPerLeak));
    }

    public float GetTimeLeft() => timeLeft;
    public int GetSurvivorsSaved() => survivorsSaved;
    public int GetTotalSurvivors() => totalSurvivors;
    public bool IsGameActive() => isGameActive;
    public bool IsGameOver() => isGameOver;
    public int GetActiveLeakCount() => activeLeakCount;
    public float GetTimeDrainMultiplier() => CalculateTimeDrainMultiplier(
        activeLeakCount,
        leakTimeDrainPerLeak,
        maximumTimeDrainMultiplier);
}
