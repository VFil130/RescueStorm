using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFetchItemQuest", menuName = "Quest/FetchItemQuest")]
public class FetchItemQuestSO : QuestSO
{
    public string requiredItemName;

    public override bool CheckCompletion(GameObject interactor, Survivor survivor)
    {
        var player = interactor.GetComponent<Player>();
        if (player == null) return false;
        Debug.Log("GetPlayer");
        var item = player.GetSelectedItem();
        if (item == null) return false;
        Debug.Log("GetSelItem", item);
        return item.itemName == requiredItemName;
    }

    public override void OnQuestCompleted(Survivor survivor)
    {
        var player = survivor.GetLastInteractor();
        if (player == null) return;

        var item = player.GetComponent<Player>().GetSelectedItem();
        if (item != null)
        {
            NetworkServer.Destroy(item.gameObject);
            player.GetComponent<Player>().RemoveSelectedItem();
        }
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