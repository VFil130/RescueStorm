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

    public void InitializeSurvivors()
    {
        if (availableQuests == null || availableQuests.Count == 0) return;
        if (survivorPrefab == null) return;

        var shuffledQuests = new List<QuestSO>(availableQuests);
        shuffledQuests = shuffledQuests.OrderBy(x => Random.value).ToList();

        int questCount = Mathf.Min(survivorsCount, shuffledQuests.Count);
        assignedQuests = shuffledQuests.Take(questCount).ToList();
    }

    public void SpawnSurvivorsAtPositions(List<Vector3> positions)
    {
        if (survivorPrefab == null) return;
        if (!NetworkServer.active) return;

        for (int i = 0; i < assignedQuests.Count && i < positions.Count; i++)
        {
            Vector3 spawnPos = positions[i] + Vector3.up * 2f;
            GameObject survivorObj = Instantiate(survivorPrefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(survivorObj);

            Survivor survivor = survivorObj.GetComponent<Survivor>();
            if (survivor != null)
            {
                survivor.SetQuest(assignedQuests[i]);
                survivors.Add(survivor);
            }

            if (!string.IsNullOrEmpty(assignedQuests[i].requiredItem))
            {
                GameObject itemPrefab = Resources.Load<GameObject>($"Items/{assignedQuests[i].requiredItem}");
                if (itemPrefab != null)
                {
                    Vector3 itemPos = positions[i] + Vector3.up * 1.5f;
                    GameObject item = Instantiate(itemPrefab, itemPos, Quaternion.identity);
                    NetworkServer.Spawn(item);
                }
            }
        }
    }

    public List<Survivor> GetSurvivors() => survivors;
    public List<QuestSO> GetAssignedQuests() => assignedQuests;
}