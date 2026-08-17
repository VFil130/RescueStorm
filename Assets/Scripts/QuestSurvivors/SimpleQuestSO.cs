using UnityEngine;

[CreateAssetMenu(fileName = "NewSimpleQuest", menuName = "Quest/SimpleQuest")]
public class SimpleQuestSO : QuestSO
{
    public override bool CheckCompletion(GameObject interactor, Survivor survivor)
    {
        return true;
    }

    public override void OnQuestCompleted(Survivor survivor)
    {
        Debug.Log("Simple quest completed!");
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