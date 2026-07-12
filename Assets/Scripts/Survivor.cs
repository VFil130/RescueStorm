using Mirror;
using UnityEngine;

public class Survivor : NetworkBehaviour, IInteractable, IPickupable
{
    [SerializeField] private QuestSO quest;
    [SyncVar(hook = nameof(OnQuestCompletedChanged))] private bool questCompleted = false;
    [SyncVar] private SurvivorState state = SurvivorState.Waiting;
    [SyncVar] private uint carrierNetId;
    [SerializeField] private bool isHeavy = true;
    [SerializeField] private string survivorName;
    public bool IsHeavy => isHeavy;
    public string Name => survivorName;
    public GameObject GameObject => gameObject;
    private GameObject carrier;
    private GameManager gameManager;
    private GameObject lastInteractor;

    public enum SurvivorState { Waiting, Carried, Saved }
    public bool IsQuestCompleted => questCompleted;

    void Start()
    {
        if (isServer) gameManager = FindObjectOfType<GameManager>();
    }

    void Update()
    {
        if (!isServer || state != SurvivorState.Carried || carrier == null) return;
        transform.position = carrier.transform.position + Vector3.up * 0.5f;
    }

    public void Interact(GameObject interactor)
    {
        if (!isServer) return;
        if (questCompleted)
        {
            Pickup(interactor);
            return;
        }

        if (quest == null) return;

        if (quest.CheckCompletion(interactor, this))
        {
            questCompleted = true;
            lastInteractor = interactor;
            quest.OnQuestCompleted(this);
        }
    }

    public bool Pickup(GameObject picker)
    {
        if (state != SurvivorState.Waiting) return false;
        if (!questCompleted) return false;

        Player player = picker.GetComponent<Player>();
        if (player == null) return false;
        carrier = picker;
        carrierNetId = picker.GetComponent<NetworkIdentity>().netId;
        state = SurvivorState.Carried;
        return player.PickupItem(this);
    }

    public void Drop(Vector3 position)
    {
        if (state != SurvivorState.Carried) return;
        carrier = null;
        carrierNetId = 0;
        state = SurvivorState.Waiting;
        transform.position = position;
    }
    public void Use(GameObject GM)
    {
        return;
    }
    [Server]
    public void Save()
    {
        if (state == SurvivorState.Saved) return;
        state = SurvivorState.Saved;
        carrier = null;
        carrierNetId = 0;
        gameManager?.AddSavedSurvivor();
    }
    public void SetQuest(QuestSO newQuest)
    {
        quest = newQuest;
    }
    private void OnQuestCompletedChanged(bool oldValue, bool newValue) { }

    public SurvivorState GetState() => state;
    public GameObject GetCarrier() => carrier;
    public GameObject GetLastInteractor() => lastInteractor;
}