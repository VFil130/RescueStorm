using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFetchItemQuest", menuName = "Quest/FetchItemQuest")]
public class FetchItemQuestSO : QuestSO
{
    [HideInInspector]
    public string requiredItemName;

    public override bool CheckCompletion(GameObject interactor, Survivor survivor)
    {
        if (interactor == null) return false;

        Player player = interactor.GetComponent<Player>();
        if (player == null) return false;

        IPickupable item = player.GetSelectedIPickable();
        if (item == null) return false;

        return item.Name == RequiredItemName;
    }

    public override void OnQuestCompleted(Survivor survivor)
    {
        GameObject interactor = survivor.GetLastInteractor();
        if (interactor == null) return;

        Player player = interactor.GetComponent<Player>();
        if (player == null) return;

        GameObject item = player.GetSelectedItem();
        if (item != null)
        {
            player.RemoveSelectedItem();
            if (NetworkServer.active)
                NetworkServer.Destroy(item);
            else
                Destroy(item);
        }
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
