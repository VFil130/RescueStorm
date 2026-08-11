using Mirror;
using Unity.VisualScripting;
using UnityEngine;

public class ClickerMiniGame : NetworkBehaviour, IMiniGame, IInteractable
{
    [SerializeField] private float clickPower = 5f;
    [SerializeField] private float decayRate = 2f;
    [SerializeField] private float maxProgress = 100f;

    private float currentProggress = 0f;
    private bool isCompleted = false;
    private Survivor linkedSurvivor;

    public string QuestId { get; private set; }
    public bool IsCompleted => isCompleted;

    void Start()
    {
        if (!isServer) return;
        ResetPuzzle();
    }
    void Update()
    {
        if (!isServer || isCompleted) return;

        currentProggress -= decayRate * Time.deltaTime;
        currentProggress = Mathf.Max(0, currentProggress);
        Debug.Log($"Прогресс: {currentProggress}/{maxProgress}");

        if (currentProggress >= maxProgress)
        {
            CompleteGame(true);
        }
    }
    public void Interact(GameObject interactor)
    {
        if (isCompleted) return;
        currentProggress += clickPower;
        currentProggress = Mathf.Min(maxProgress, currentProggress);
        Debug.Log($"Прогресс: {currentProggress}/{maxProgress}");
    }
    private void ResetPuzzle()
    {
        currentProggress = 0f;
    }
    public void CompleteGame(bool success)
    {
        isCompleted = true;
        linkedSurvivor?.CompleteMiniGame(success);
    }
    public void Initialize (string questId, Survivor survivor)
    {
        QuestId = questId;
        linkedSurvivor = survivor;
    }
    public void StartGame(Player player)
    {
        //пусто
    }
}
