using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public GameObject[] roomPrefabs;
    public GameObject wallPrefab;
    public int maxRooms = 20;
    public float cellSize = 10f;
    public float spacing = 2f;
    public bool generateOnStart = true;
    public int gridBounds = 30;
    public SurvivorManager survivorManager;

    private List<GameObject> mandatoryRooms = new List<GameObject>();
    private List<RoomNode> rooms = new List<RoomNode>();
    private List<DoorSlot> openSlots = new List<DoorSlot>();
    private Dictionary<Vector2Int, GameObject> gridOccupied = new Dictionary<Vector2Int, GameObject>();
    private List<Vector3> roomPositions = new List<Vector3>();

    void Start()
    {
        if (generateOnStart) GenerateDungeon();
    }

    public void SetMandatoryRooms(List<GameObject> rooms)
    {
        mandatoryRooms = rooms;
    }

    public List<Vector3> GetRandomRoomPositions(int count)
    {
        List<Vector3> shuffled = new List<Vector3>(roomPositions);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rand = Random.Range(i, shuffled.Count);
            Vector3 temp = shuffled[i];
            shuffled[i] = shuffled[rand];
            shuffled[rand] = temp;
        }
        return shuffled.Take(count).ToList();
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();
        if (roomPrefabs.Length == 0) return;

        if (survivorManager == null)
            survivorManager = FindObjectOfType<SurvivorManager>();

        if (survivorManager != null)
        {
            survivorManager.InitializeSurvivors();
            List<string> requiredRoomNames = survivorManager.GetAllRequiredRooms();
            mandatoryRooms.Clear();
            foreach (string roomName in requiredRoomNames)
            {
                GameObject roomPrefab = FindRoomPrefabByName(roomName);
                if (roomPrefab != null) mandatoryRooms.Add(roomPrefab);
            }
        }

        List<GameObject> combinedPrefabs = new List<GameObject>();
        combinedPrefabs.AddRange(roomPrefabs);
        combinedPrefabs.AddRange(mandatoryRooms);

        GameObject startRoom = Instantiate(combinedPrefabs[0], Vector3.zero, Quaternion.identity);
        startRoom.transform.parent = this.transform;
        RoomNode startNode = new RoomNode(startRoom, Vector2Int.zero, GetRoomSize(startRoom));
        rooms.Add(startNode);
        roomPositions.Add(startRoom.transform.position);
        RegisterRoom(startNode);
        openSlots.AddRange(GetDoorSlots(startRoom, Vector2Int.zero));

        int attempts = 0;
        while (openSlots.Count > 0 && rooms.Count < maxRooms && attempts < 1000)
        {
            attempts++;
            int slotIndex = Random.Range(0, openSlots.Count);
            DoorSlot slot = openSlots[slotIndex];

            if (TryPlaceRoom(slot, combinedPrefabs))
            {
                openSlots.RemoveAt(slotIndex);
            }
            else
            {
                CloseSlot(slot);
                openSlots.RemoveAt(slotIndex);
            }
        }
        CloseRemainingSlots();
    }

    private GameObject FindRoomPrefabByName(string roomName)
    {
        foreach (var prefab in roomPrefabs)
        {
            if (prefab.name == roomName || prefab.name.Replace("(Clone)", "").Trim() == roomName)
            {
                return prefab;
            }
        }
        return null;
    }

    private Vector2Int GetRoomSize(GameObject room)
    {
        RoomData data = room.GetComponent<RoomData>();
        return data != null ? data.size : Vector2Int.one;
    }

    private bool TryPlaceRoom(DoorSlot slot, List<GameObject> prefabs)
    {
        List<GameObject> compatible = GetCompatibleRooms(slot.direction, prefabs);
        if (compatible.Count == 0) return false;

        for (int i = 0; i < 5; i++)
        {
            GameObject chosen = compatible[Random.Range(0, compatible.Count)];
            Vector2Int roomSize = GetRoomSize(chosen);
            Vector2Int targetGrid = slot.gridPosition + slot.direction;

            if (IsOverlapping(targetGrid, roomSize)) continue;
            if (IsOutsideBounds(targetGrid, roomSize)) continue;

            Vector3 worldPos = new Vector3(
                targetGrid.x * (cellSize + spacing),
                0,
                targetGrid.y * (cellSize + spacing)
            );

            Quaternion rotation = GetRotationForDirection(slot.direction);
            GameObject newRoom = Instantiate(chosen, worldPos, rotation);
            newRoom.transform.parent = this.transform;

            RoomNode node = new RoomNode(newRoom, targetGrid, roomSize);
            rooms.Add(node);
            roomPositions.Add(newRoom.transform.position);
            RegisterRoom(node);

            List<DoorSlot> newSlots = GetDoorSlots(newRoom, targetGrid);
            foreach (var newSlot in newSlots)
            {
                if (!IsSlotConnectedToExisting(newSlot))
                {
                    openSlots.Add(newSlot);
                }
            }
            return true;
        }
        return false;
    }

    private bool IsOverlapping(Vector2Int pos, Vector2Int size)
    {
        for (int x = pos.x; x < pos.x + size.x; x++)
        {
            for (int y = pos.y; y < pos.y + size.y; y++)
            {
                if (gridOccupied.ContainsKey(new Vector2Int(x, y))) return true;
            }
        }
        return false;
    }

    private bool IsOutsideBounds(Vector2Int pos, Vector2Int size)
    {
        int half = gridBounds / 2;
        return pos.x < -half || pos.x + size.x > half || pos.y < -half || pos.y + size.y > half;
    }

    private List<DoorSlot> GetDoorSlots(GameObject room, Vector2Int gridPos)
    {
        List<DoorSlot> slots = new List<DoorSlot>();
        DoorPoint[] doors = room.GetComponentsInChildren<DoorPoint>();
        foreach (var door in doors)
        {
            slots.Add(new DoorSlot
            {
                gridPosition = gridPos + GetDoorGridOffset(door.transform.localPosition, GetRoomSize(room)),
                direction = door.direction,
                doorObject = door.gameObject
            });
        }
        return slots;
    }

    private Vector2Int GetDoorGridOffset(Vector3 localPos, Vector2Int roomSize)
    {
        float halfX = roomSize.x * (cellSize + spacing) / 2f;
        float halfY = roomSize.y * (cellSize + spacing) / 2f;

        if (Mathf.Approximately(localPos.z, halfY)) return new Vector2Int(0, roomSize.y);
        if (Mathf.Approximately(localPos.z, -halfY)) return new Vector2Int(0, -1);
        if (Mathf.Approximately(localPos.x, halfX)) return new Vector2Int(roomSize.x, 0);
        if (Mathf.Approximately(localPos.x, -halfX)) return new Vector2Int(-1, 0);
        return Vector2Int.zero;
    }

    private List<GameObject> GetCompatibleRooms(Vector2Int direction, List<GameObject> prefabs)
    {
        List<GameObject> compatible = new List<GameObject>();
        foreach (var prefab in prefabs)
        {
            DoorPoint[] doors = prefab.GetComponentsInChildren<DoorPoint>();
            foreach (var door in doors)
            {
                if (door.direction.x == -direction.x && door.direction.y == -direction.y)
                {
                    compatible.Add(prefab);
                    break;
                }
            }
        }
        return compatible;
    }

    private Quaternion GetRotationForDirection(Vector2Int direction)
    {
        if (direction.x == 0 && direction.y == 1) return Quaternion.identity;
        if (direction.x == 0 && direction.y == -1) return Quaternion.Euler(0, 180, 0);
        if (direction.x == 1 && direction.y == 0) return Quaternion.Euler(0, 90, 0);
        if (direction.x == -1 && direction.y == 0) return Quaternion.Euler(0, -90, 0);
        return Quaternion.identity;
    }

    private void CloseSlot(DoorSlot slot)
    {
        Vector3 worldPos = new Vector3(
            slot.gridPosition.x * (cellSize + spacing),
            0,
            slot.gridPosition.y * (cellSize + spacing)
        );
        worldPos += GetDoorWorldOffset(slot.direction) * (cellSize / 2f);
        Quaternion rotation = GetRotationForDirection(slot.direction);
        Instantiate(wallPrefab, worldPos, rotation, this.transform);
    }

    private Vector3 GetDoorWorldOffset(Vector2Int direction)
    {
        if (direction.x == 0 && direction.y == 1) return new Vector3(0, 0, 1);
        if (direction.x == 0 && direction.y == -1) return new Vector3(0, 0, -1);
        if (direction.x == 1 && direction.y == 0) return new Vector3(1, 0, 0);
        if (direction.x == -1 && direction.y == 0) return new Vector3(-1, 0, 0);
        return Vector3.zero;
    }

    private void CloseRemainingSlots()
    {
        foreach (var slot in openSlots) CloseSlot(slot);
        openSlots.Clear();
    }

    private bool IsSlotConnectedToExisting(DoorSlot slot)
    {
        Vector2Int checkPos = slot.gridPosition + slot.direction;
        return gridOccupied.ContainsKey(checkPos);
    }

    private void RegisterRoom(RoomNode node)
    {
        for (int x = node.gridPosition.x; x < node.gridPosition.x + node.size.x; x++)
        {
            for (int y = node.gridPosition.y; y < node.gridPosition.y + node.size.y; y++)
            {
                gridOccupied[new Vector2Int(x, y)] = node.roomObject;
            }
        }
    }

    private void ClearDungeon()
    {
        foreach (Transform child in transform) DestroyImmediate(child.gameObject);
        rooms.Clear();
        openSlots.Clear();
        gridOccupied.Clear();
        mandatoryRooms.Clear();
        roomPositions.Clear();
    }

    private class RoomNode
    {
        public GameObject roomObject;
        public Vector2Int gridPosition;
        public Vector2Int size;

        public RoomNode(GameObject obj, Vector2Int pos, Vector2Int s)
        {
            roomObject = obj;
            gridPosition = pos;
            size = s;
        }
    }

    private class DoorSlot
    {
        public Vector2Int gridPosition;
        public Vector2Int direction;
        public GameObject doorObject;
    }
}