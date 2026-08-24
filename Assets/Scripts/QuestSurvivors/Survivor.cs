using Mirror;
using UnityEngine;

public class Survivor : NetworkBehaviour, IInteractable, IPickupable
{
    public enum SurvivorState
    {
        Waiting,
        Carried,
        Saved
    }

    [SerializeField] private QuestSO quest;
    [SerializeField] private bool isHeavy = true;
    [SerializeField] private string survivorName;
    [SerializeField] private GameObject itemPrefab;

    [SyncVar(hook = nameof(OnQuestCompletedChanged))] private bool questCompleted;
    [SyncVar] private SurvivorState state = SurvivorState.Waiting;
    [SyncVar] private uint carrierNetId;
    [SyncVar(hook = nameof(OnQuestIdChanged))] private string questId = string.Empty;

    private GameObject carrier;
    private GameManager gameManager;
    private GameObject lastInteractor;
    private GameObject indicatorInstance;

    public bool IsHeavy => isHeavy;
    public string Name => survivorName;
    public GameObject GameObject => gameObject;
    public bool IsQuestCompleted => questCompleted;

    private bool HasStateAuthority => NetworkServer.active || !NetworkClient.active;

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        if (quest != null) SpawnIndicator();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyQuestId(questId);
    }

    private void Update()
    {
        RotateIndicator();
        if (!HasStateAuthority || state != SurvivorState.Carried || carrier == null) return;
        transform.position = carrier.transform.position + Vector3.up * 0.5f;
    }

    public void Interact(GameObject interactor)
    {
        if (!HasStateAuthority) return;
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

    public void CompleteMiniGame(bool success)
    {
        if (!HasStateAuthority) return;
        if (questCompleted) return;
        if (quest == null) return;

        if (success && quest.CheckCompletion(null, this))
        {
            questCompleted = true;
            quest.OnQuestCompleted(this);
        }
    }

    public void Pickup(GameObject picker)
    {
        if (!HasStateAuthority) return;
        if (state != SurvivorState.Waiting) return;
        if (!questCompleted) return;

        Player player = picker.GetComponent<Player>();
        if (player == null) return;
        if (!player.PickupItem(this)) return;

        carrier = picker;
        NetworkIdentity identity = picker.GetComponent<NetworkIdentity>();
        carrierNetId = identity != null ? identity.netId : 0;
        state = SurvivorState.Carried;
    }

    public void Drop(Vector3 position)
    {
        if (state != SurvivorState.Carried) return;
        carrier = null;
        carrierNetId = 0;
        state = SurvivorState.Waiting;
        transform.position = position;
    }

    public void Use(GameObject _)
    {
    }

    public void Save()
    {
        if (!HasStateAuthority) return;
        if (state == SurvivorState.Saved) return;
        state = SurvivorState.Saved;
        carrier = null;
        carrierNetId = 0;
        gameManager?.AddSavedSurvivor();
    }

    public void SetQuest(QuestSO newQuest)
    {
        quest = newQuest;
        questId = newQuest != null ? newQuest.questId : string.Empty;
        quest?.Initialize(this);
        SpawnIndicator();
    }

    private void SpawnIndicator()
    {
        if (itemPrefab == null || indicatorInstance != null) return;

        indicatorInstance = Instantiate(itemPrefab, transform.position + Vector3.up, Quaternion.identity);
        indicatorInstance.transform.SetParent(transform);

        Material indicatorMat = Resources.Load<Material>("Indicator");
        if (indicatorMat != null)
        {
            MeshRenderer[] renderers = indicatorInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.material = indicatorMat;
            }
        }

        Collider[] colliders = indicatorInstance.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private void RotateIndicator()
    {
        if (indicatorInstance == null) return;

        indicatorInstance.transform.Rotate(Vector3.up, 30f * Time.deltaTime);
    }

    public void SetIndicatorColor(Color color)
    {
        if (indicatorInstance == null) return;

        MeshRenderer[] renderers = indicatorInstance.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.material.color = color;
        }
    }
    public void SetIndicator(GameObject gm)
    {
        itemPrefab = gm;
    }
    private void OnDestroy()
    {
        if (indicatorInstance != null) Destroy(indicatorInstance);
    }

    private void OnQuestCompletedChanged(bool _, bool completed)
    {
        if (completed) SetIndicatorColor(Color.green);
    }

    private void OnQuestIdChanged(string _, string newQuestId)
    {
        ApplyQuestId(newQuestId);
    }

    private void ApplyQuestId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        SurvivorManager manager = FindFirstObjectByType<SurvivorManager>();
        QuestSO resolvedQuest = manager != null ? manager.ResolveQuestById(id) : null;
        if (resolvedQuest == null)
        {
            Debug.LogError($"Could not resolve quest '{id}' for survivor '{name}'.", this);
            return;
        }

        quest = resolvedQuest;
        SpawnIndicator();
    }

    public SurvivorState GetState() => state;
    public GameObject GetCarrier() => carrier;
    public GameObject GetLastInteractor() => lastInteractor;
}
