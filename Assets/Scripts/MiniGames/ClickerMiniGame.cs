using Mirror;
using UnityEngine;

public class ClickerMiniGame : NetworkBehaviour, IMiniGame, IInteractable
{
    [SerializeField] private float clickPower = 5f;
    [SerializeField] private float decayRate = 2f;
    [SerializeField] private float maxProgress = 100f;

    private float currentProgress;
    private bool isCompleted;
    private Survivor linkedSurvivor;

    public string QuestId { get; private set; }
    public bool IsCompleted => isCompleted;

    private bool HasStateAuthority => NetworkServer.active || !NetworkClient.active;

    private void Start()
    {
        if (HasStateAuthority)
            ResetPuzzle();
    }

    private void Update()
    {
        if (!HasStateAuthority || isCompleted) return;

        currentProgress = Mathf.Max(0f, currentProgress - decayRate * Time.deltaTime);
    }

    public void Interact(GameObject interactor)
    {
        if (!HasStateAuthority || isCompleted) return;

        currentProgress = Mathf.Min(maxProgress, currentProgress + clickPower);
        if (currentProgress >= maxProgress)
            CompleteGame(true);
    }

    private void ResetPuzzle()
    {
        currentProgress = 0f;
        isCompleted = false;
    }

    public void CompleteGame(bool success)
    {
        if (!HasStateAuthority || isCompleted) return;

        isCompleted = true;
        linkedSurvivor?.CompleteMiniGame(success);
    }

    public void Initialize(string questId, Survivor survivor)
    {
        QuestId = questId;
        linkedSurvivor = survivor;
    }
    public void StartGame(Player player)
    {
        if (HasStateAuthority)
            ResetPuzzle();
    }
}
