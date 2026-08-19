using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "Quest/Quest")]
public abstract class QuestSO : ScriptableObject
{
    [Header("Quest")]
    [Tooltip("Stable identifier used by quest logic and save data.")]
    public string questId;
    [Tooltip("Name shown to players and designers.")]
    public string questName;

    [Header("Generated content")]
    [SerializeField]
    [Tooltip("Room prefab that must be included when this quest is assigned.")]
    private GameObject requiredRoomPrefab;
    [Tooltip("Item prefab spawned for this quest.")]
    public GameObject itemPrefab;

    [HideInInspector]
    public string requiredRoom;
    [HideInInspector]
    public string requiredItem;

    protected Survivor owner;
    protected bool isCompleted;

    public GameObject RequiredRoomPrefab => requiredRoomPrefab;
    public GameObject ItemPrefab => itemPrefab;
    public string RequiredRoomName => requiredRoomPrefab != null ? requiredRoomPrefab.name : requiredRoom;
    public string RequiredItemName
    {
        get
        {
            if (itemPrefab == null) return requiredItem;

            ItemObject item = itemPrefab.GetComponent<ItemObject>();
            return item != null ? item.Name : itemPrefab.name;
        }
    }

    public virtual void Initialize(Survivor survivor)
    {
        owner = survivor;
        isCompleted = false;
    }

    public abstract bool CheckCompletion(GameObject interactor, Survivor survivor);
    public abstract void OnQuestCompleted(Survivor survivor);
    public abstract QuestRequirement GetRequirement();
}
public class QuestRequirement
{
    public string questId;
    public string requiredRoom;
    public string requiredItem;
}
