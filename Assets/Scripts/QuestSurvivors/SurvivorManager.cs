using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class SurvivorManager : NetworkBehaviour
{
    [SerializeField] private float survivorSpawnHeight = 2f;
    private const float QuestItemSpawnHeight = 1.5f;

    [Header("Survivor spawning")]
    [InspectorName("Survivor Character")]
    [Tooltip("Character prefab spawned for every selected quest.")]
    [SerializeField] private GameObject survivorPrefab;
    [InspectorName("Quests Per Level")]
    [Tooltip("Maximum number of survivors and quests selected for one generated level.")]
    [SerializeField, Min(0)] private int survivorsCount = 5;

    [Header("Quest selection")]
    [InspectorName("Quest Pool")]
    [Tooltip("Quests that may be assigned to survivors in this scene.")]
    [SerializeField] private List<QuestSO> availableQuests;
    [InspectorName("Legacy Item Lookup")]
    [Tooltip("Fallback lookup for old quests that still store an item name instead of a prefab reference.")]
    [SerializeField] private List<GameObject> questItemPrefabs = new List<GameObject>();

    private readonly List<Survivor> survivors = new List<Survivor>();
    private List<QuestSO> assignedQuests = new List<QuestSO>();
    private readonly Dictionary<QuestSO, Transform> questMiniGamePoints = new Dictionary<QuestSO, Transform>();

    public List<QuestRequirement> GetAllQuestRequirements()
    {
        return assignedQuests.Select(quest => quest.GetRequirement()).ToList();
    }

    public List<string> GetAllRequiredRooms()
    {
        return assignedQuests
            .Select(quest => quest.RequiredRoomName)
            .Where(roomName => !string.IsNullOrEmpty(roomName))
            .Distinct()
            .ToList();
    }

    public List<string> GetAllRequiredItems()
    {
        return assignedQuests
            .Select(quest => quest.RequiredItemName)
            .Where(itemName => !string.IsNullOrEmpty(itemName))
            .Distinct()
            .ToList();
    }

    public List<GameObject> ResolveRequiredRoomPrefabs(
        DungeonGenerator generator,
        out List<string> missingRoomNames)
    {
        List<GameObject> roomPrefabs = assignedQuests
            .Select(quest => quest.RequiredRoomPrefab)
            .Where(prefab => prefab != null)
            .Distinct()
            .ToList();

        IEnumerable<string> legacyRoomNames = assignedQuests
            .Where(quest => quest.RequiredRoomPrefab == null)
            .Select(quest => quest.RequiredRoomName);
        roomPrefabs.AddRange(generator.ResolveRoomPrefabs(legacyRoomNames, out missingRoomNames));
        return roomPrefabs;
    }

    public void InitializeSurvivors()
    {
        assignedQuests.Clear();
        if (availableQuests == null || availableQuests.Count == 0) return;
        if (survivorPrefab == null) return;

        List<QuestSO> shuffledQuests = availableQuests
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        int questCount = Mathf.Min(survivorsCount, shuffledQuests.Count);
        assignedQuests = shuffledQuests.Take(questCount).ToList();
    }

    public List<string> GetAssignedQuestIds()
    {
        return assignedQuests
            .Where(quest => quest != null)
            .Select(quest => quest.questId)
            .ToList();
    }

    public bool AssignQuestsById(
        IEnumerable<string> questIds,
        out List<string> missingQuestIds)
    {
        missingQuestIds = new List<string>();
        assignedQuests = new List<QuestSO>();
        if (questIds == null) return true;

        Dictionary<string, QuestSO> questsById = (availableQuests ?? new List<QuestSO>())
            .Where(quest => quest != null && !string.IsNullOrWhiteSpace(quest.questId))
            .GroupBy(quest => quest.questId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (string questId in questIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (questsById.TryGetValue(questId, out QuestSO quest))
                assignedQuests.Add(quest);
            else
                missingQuestIds.Add(questId);
        }

        return missingQuestIds.Count == 0;
    }

    public QuestSO ResolveQuestById(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return null;

        return (availableQuests ?? new List<QuestSO>())
            .FirstOrDefault(quest =>
                quest != null &&
                string.Equals(quest.questId, questId, StringComparison.Ordinal));
    }

    public void SpawnSurvivorsAtPositions(List<Vector3> positions)
    {
        if (survivorPrefab == null) return;
        if (!NetworkServer.active) return;

        SpawnAssignedSurvivors(positions, networked: true);
    }

    public void SpawnSurvivorsLocallyAtPositions(List<Vector3> positions)
    {
        if (survivorPrefab == null) return;
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("Local survivor spawning is unavailable while Mirror is active.", this);
            return;
        }

        SpawnAssignedSurvivors(positions, networked: false);
    }

    public List<Vector3> ResolveSurvivorPositions(DungeonGenerationResult result)
    {
        List<Vector3> positions = new List<Vector3>();
        if (result == null || assignedQuests.Count == 0) return positions;

        questMiniGamePoints.Clear();

        List<RoomData> availableRooms = result.Rooms
            .Where(room => room != null)
            .ToList();
        HashSet<RoomData> usedRooms = new HashSet<RoomData>();

        foreach (QuestSO quest in assignedQuests)
        {
            string requiredRoomName = quest.RequiredRoomName;
            RoomData room;
            if (string.IsNullOrWhiteSpace(requiredRoomName))
            {
                room = FindAvailableRoom(availableRooms, usedRooms);
            }
            else
            {
                room = FindQuestRoom(availableRooms, usedRooms, requiredRoomName);
                if (room == null)
                {
                    Debug.LogError(
                        $"Generated dungeon has no mandatory room '{requiredRoomName}' for quest '{quest.questName}'.",
                        this);
                    return new List<Vector3>();
                }
            }

            if (room == null) break;

            usedRooms.Add(room);
            questMiniGamePoints[quest] = room.miniGameSpawnPoint;
            positions.Add(GetSurvivorSpawnPosition(room));
        }

        return positions;
    }

    private static RoomData FindAvailableRoom(
        IEnumerable<RoomData> rooms,
        ISet<RoomData> usedRooms)
    {
        RoomData roomWithSpawnPoint = rooms.FirstOrDefault(room =>
            !usedRooms.Contains(room) && room.survivorSpawnPoint != null);

        return roomWithSpawnPoint ?? rooms.FirstOrDefault(room => !usedRooms.Contains(room));
    }

    private static RoomData FindQuestRoom(
        IEnumerable<RoomData> rooms,
        ISet<RoomData> usedRooms,
        string requiredRoom)
    {
        if (string.IsNullOrWhiteSpace(requiredRoom)) return null;

        string expectedName = NormalizeRoomName(requiredRoom);
        return rooms.FirstOrDefault(room =>
            !usedRooms.Contains(room) &&
            (room.RuntimeRole & RoomRuntimeRole.Mandatory) != 0 &&
            string.Equals(
                NormalizeRoomName(room.name),
                expectedName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRoomName(string roomName)
    {
        return roomName.Replace("(Clone)", string.Empty).Trim();
    }

    private static Vector3 GetSurvivorSpawnPosition(RoomData room)
    {
        return room.survivorSpawnPoint != null
            ? room.survivorSpawnPoint.position
            : room.transform.position;
    }

    private void SpawnAssignedSurvivors(List<Vector3> positions, bool networked)
    {
        if (positions == null) return;

        for (int i = 0; i < assignedQuests.Count && i < positions.Count; i++)
        {
            QuestSO quest = assignedQuests[i];
            if (questMiniGamePoints.TryGetValue(quest, out Transform miniGamePoint))
                quest.SetMiniGameSpawnPoint(miniGamePoint);

            Vector3 survivorPosition = positions[i] + Vector3.up * survivorSpawnHeight;
            GameObject survivorObject = Instantiate(survivorPrefab, survivorPosition, Quaternion.identity);

            Survivor survivor = survivorObject.GetComponent<Survivor>();
            if (survivor != null)
            {
                survivor.SetQuest(quest);
                survivors.Add(survivor);
            }

            if (networked) NetworkServer.Spawn(survivorObject);

            if (!string.IsNullOrEmpty(quest.RequiredItemName))
            {
                SpawnQuestItem(quest, positions[i], networked);
            }
        }
    }

    private void SpawnQuestItem(QuestSO quest, Vector3 roomSpawnPosition, bool networked)
    {
        GameObject itemPrefab = quest.ItemPrefab != null
            ? quest.ItemPrefab
            : ResolveQuestItemPrefab(quest.RequiredItemName);
        if (itemPrefab == null)
        {
            Debug.LogError($"Quest item prefab '{quest.RequiredItemName}' is not configured.", this);
            return;
        }

        Vector3 itemPosition = roomSpawnPosition + Vector3.up * QuestItemSpawnHeight;
        GameObject item = Instantiate(itemPrefab, itemPosition, Quaternion.identity);
        if (networked) NetworkServer.Spawn(item);
    }

    private GameObject ResolveQuestItemPrefab(string itemName)
    {
        foreach (GameObject prefab in questItemPrefabs)
        {
            if (prefab == null) continue;

            ItemObject item = prefab.GetComponent<ItemObject>();
            string configuredName = item != null ? item.Name : prefab.name;
            if (string.Equals(configuredName, itemName, StringComparison.OrdinalIgnoreCase))
                return prefab;
        }

        return null;
    }

    public List<Survivor> GetSurvivors() => survivors;
    public List<QuestSO> GetAssignedQuests() => assignedQuests;
}
