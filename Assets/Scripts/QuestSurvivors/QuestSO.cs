using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "Quest/Quest")]
public abstract class QuestSO : ScriptableObject
{
    public string questId;
    public string questName;
    public string requiredRoom;
    public string requiredItem;
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