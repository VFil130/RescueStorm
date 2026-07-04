using Mirror;
using UnityEngine;
using Zenject;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float matchDuration = 300f;
    [SerializeField] private int survivorsToWin = 6;
    [SerializeField] private int maxSurvivors = 10;

    [SyncVar(hook = nameof(OnTimeLeftChanged))]
    private float timeLeft;

    [SyncVar(hook = nameof(OnSurvivorsSavedChanged))]
    private int survivorsSaved;

    [SyncVar] private bool isGameActive = false;
    [SyncVar] private bool isGameOver = false;

    private float matchTimer;
    private int totalSurvivors;

    [Inject] private AudioManager audioManager;
    [Inject] private ParticleManager particleManager;
    [Inject] private SceneManagerService sceneManager;

    public event System.Action<float> OnTimeUpdated;
    public event System.Action<int, int> OnSurvivorsUpdated;
    public event System.Action<bool> OnGameEnded;

    void Start()
    {
        if (isServer)
        {
            StartMatch();
        }
    }

    void Update()
    {
        if (!isServer || !isGameActive || isGameOver) return;

        matchTimer -= Time.deltaTime;
        timeLeft = matchTimer;

        if (matchTimer <= 0)
        {
            EndMatch(false);
        }
    }

    [Server]
    private void StartMatch()
    {
        matchTimer = matchDuration;
        timeLeft = matchDuration;
        survivorsSaved = 0;
        isGameActive = true;
        isGameOver = false;
        totalSurvivors = Random.Range(8, maxSurvivors + 1);

        RpcOnMatchStarted(totalSurvivors);
        Debug.Log($"Match started. Total survivors: {totalSurvivors}");
    }

    [Server]
    public void AddSavedSurvivor()
    {
        if (!isGameActive || isGameOver) return;

        survivorsSaved++;
        RpcOnSurvivorSaved(survivorsSaved);

        if (survivorsSaved >= totalSurvivors)
        {
            EndMatch(true);
        }
    }

    [Server]
    public void EndMatch(bool victory)
    {
        if (isGameOver) return;

        isGameActive = false;
        isGameOver = true;

        RpcOnMatchEnded(victory, survivorsSaved, totalSurvivors);
        Debug.Log($"Match ended. Victory: {victory}. Saved: {survivorsSaved}/{totalSurvivors}");
    }

    [ClientRpc]
    private void RpcOnMatchStarted(int total)
    {
        totalSurvivors = total;
        OnSurvivorsUpdated?.Invoke(0, total);
    }

    [ClientRpc]
    private void RpcOnSurvivorSaved(int saved)
    {
        survivorsSaved = saved;
        OnSurvivorsUpdated?.Invoke(saved, totalSurvivors);

        if (isLocalPlayer)
        {
            audioManager?.PlaySFX("SurvivorSaved", Vector3.zero);
            particleManager?.PlayParticle("SavedParticle", Vector3.zero);
        }
    }

    [ClientRpc]
    private void RpcOnMatchEnded(bool victory, int saved, int total)
    {
        isGameActive = false;
        isGameOver = true;
        OnGameEnded?.Invoke(victory);

        if (isLocalPlayer)
        {
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

        Debug.Log($"Match ended: {saved}/{total} saved. Victory: {victory}");
    }

    private void OnTimeLeftChanged(float oldValue, float newValue)
    {
        OnTimeUpdated?.Invoke(newValue);
    }

    private void OnSurvivorsSavedChanged(int oldValue, int newValue)
    {
        OnSurvivorsUpdated?.Invoke(newValue, totalSurvivors);
    }

    public float GetTimeLeft() => timeLeft;
    public int GetSurvivorsSaved() => survivorsSaved;
    public int GetTotalSurvivors() => totalSurvivors;
    public bool IsGameActive() => isGameActive;
    public bool IsGameOver() => isGameOver;
}