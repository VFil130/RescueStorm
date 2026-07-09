using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SurvivorManager : NetworkBehaviour
{
    [SerializeField] private GameObject survivorPrefab;
    [SerializeField] private List<QuestSO> availableQuests;
    [SerializeField] private int survivorsCount = 5;

    private List<Survivor> survivors = new List<Survivor>();
    private List<QuestSO> assignedQuests = new List<QuestSO>();

    public List<QuestRequirement> GetAllQuestRequirements()
    {
        var requirements = new List<QuestRequirement>();
        foreach (var quest in assignedQuests)
        {
            requirements.Add(quest.GetRequirement());
        }
        return requirements;
    }

    public List<string> GetAllRequiredRooms()
    {
        return assignedQuests
            .Where(q => !string.IsNullOrEmpty(q.requiredRoom))
            .Select(q => q.requiredRoom)
            .Distinct()
            .ToList();
    }

    public List<string> GetAllRequiredItems()
    {
        return assignedQuests
            .Where(q => !string.IsNullOrEmpty(q.requiredItem))
            .Select(q => q.requiredItem)
            .Distinct()
            .ToList();
    }

    [Server]
    public void InitializeSurvivors()
    {
        if (availableQuests == null || availableQuests.Count == 0) return;
        if (survivorPrefab == null) return;

        var shuffledQuests = new List<QuestSO>(availableQuests);
        shuffledQuests = shuffledQuests.OrderBy(x => Random.value).ToList();

        int questCount = Mathf.Min(survivorsCount, shuffledQuests.Count);
        assignedQuests = shuffledQuests.Take(questCount).ToList();

        for (int i = 0; i < questCount; i++)
        {
            var survivor = Instantiate(survivorPrefab);
            NetworkServer.Spawn(survivor);

            var survivorComponent = survivor.GetComponent<Survivor>();
            if (survivorComponent != null)
            {
                survivorComponent.SetQuest(assignedQuests[i]);
                survivors.Add(survivorComponent);
            }
        }
    }

    public List<Survivor> GetSurvivors() => survivors;
    public List<QuestSO> GetAssignedQuests() => assignedQuests;
}