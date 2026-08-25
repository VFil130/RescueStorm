using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMinigameQuest", menuName = "Quest/MinigameQuest")]
public class MinigameQuestSO : QuestSO
{
    [SerializeField] private GameObject miniGamePrefab;
    private IMiniGame miniGameInstance;

    public override void Initialize(Survivor survivor)
    {
        base.Initialize(survivor);
        owner = survivor;

        if (miniGamePrefab == null) return;

        Transform spawnPoint = GetMiniGameSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogError($"[MinigameQuest] '{questName}': no mini-game spawn point");
            return;
        }

        GameObject obj = Instantiate(miniGamePrefab, spawnPoint.position, spawnPoint.rotation);
        miniGameInstance = obj.GetComponent<IMiniGame>();
        if (miniGameInstance == null)
        {
            Destroy(obj);
            return;
        }

        miniGameInstance.Initialize(questId, survivor);
        if (!NetworkServer.active) return;

        NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            Debug.LogError(
                $"Minigame prefab '{miniGamePrefab.name}' needs NetworkIdentity.",
                obj);
            Destroy(obj);
            miniGameInstance = null;
            return;
        }

        NetworkServer.Spawn(obj);
    }

    public override bool CheckCompletion(GameObject interactor, Survivor survivor)
    {
        return miniGameInstance != null && miniGameInstance.IsCompleted;
    }

    public override void OnQuestCompleted(Survivor survivor)
    {
        Debug.Log("COMPLETE!!!!");
    }

    public override QuestRequirement GetRequirement()
    {
        return new QuestRequirement
        {
            questId = questId,
            requiredRoom = RequiredRoomName,
            requiredItem = RequiredItemName
        };
    }
}
