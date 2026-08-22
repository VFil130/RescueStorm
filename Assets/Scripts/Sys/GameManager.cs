using Mirror;
using UnityEngine;
using Zenject;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float matchDuration = 300f;

    [SyncVar(hook = nameof(OnTimeLeftChanged))]
    private float timeLeft;
    [SyncVar(hook = nameof(OnSurvivorsSavedChanged))]
    private int survivorsSaved;
    [SyncVar(hook = nameof(OnTotalSurvivorsChanged))]
    private int totalSurvivors;
    [SyncVar] private bool isGameActive;
    [SyncVar] private bool isGameOver;

    private float matchTimer;

    [Inject] private AudioManager audioManager;
    [Inject] private ParticleManager particleManager;

    public event System.Action<float> OnTimeUpdated;
    public event System.Action<int, int> OnSurvivorsUpdated;
    public event System.Action<bool> OnGameEnded;

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
        isGameActive = true;
        isGameOver = false;
        if (NetworkServer.active)
            RpcOnMatchStarted(survivorCount);
        else
            OnSurvivorsUpdated?.Invoke(0, survivorCount);
    }

    private void Update()
    {
        if (!HasStateAuthority || !isGameActive || isGameOver) return;

        matchTimer -= Time.deltaTime;
        timeLeft = Mathf.Max(0f, matchTimer);
        if (matchTimer <= 0f) EndMatch(false);
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
        if (NetworkServer.active)
            RpcOnMatchEnded(victory);
        else
            OnGameEnded?.Invoke(victory);
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

    public float GetTimeLeft() => timeLeft;
    public int GetSurvivorsSaved() => survivorsSaved;
    public int GetTotalSurvivors() => totalSurvivors;
    public bool IsGameActive() => isGameActive;
    public bool IsGameOver() => isGameOver;
}
