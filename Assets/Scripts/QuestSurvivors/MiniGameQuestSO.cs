using UnityEngine;

[CreateAssetMenu(fileName = "NewMinigameQuest", menuName = "Quest/MinigameQuest")]
public class MinigameQuestSO : QuestSO
{
    [SerializeField] private IMiniGame miniGame;

    public void SetMiniGame(IMiniGame game)
    {
        miniGame = game;
    }

    public override bool CheckCompletion(GameObject interactor, Survivor survivor)
    {
        if (miniGame == null) return false;
        return miniGame.IsCompleted;
    }

    public override void OnQuestCompleted(Survivor survivor)
    {
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