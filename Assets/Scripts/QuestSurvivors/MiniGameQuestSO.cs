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

        if (miniGamePrefab != null)
        {
            GameObject obj = Instantiate(miniGamePrefab);
            miniGameInstance = obj.GetComponent<IMiniGame>();
            if (miniGameInstance != null)
            {
                miniGameInstance.Initialize(questId, survivor);
            }
        }
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
            requiredRoom = requiredRoom,
            requiredItem = requiredItem
        };
    }
}
