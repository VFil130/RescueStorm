using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public GameObject[] roomPrefabs;
    public GameObject wallPrefab;
    public int maxRooms = 20;
    public float roomSize = 10f;
    public float spacing = 2f;
    public bool generateOnStart = true;
    public int gridBounds = 20;

    private List<RoomNode> rooms = new List<RoomNode>();
    private List<DoorSlot> openSlots = new List<DoorSlot>();
    private Dictionary<Vector2Int, GameObject> gridOccupied = new Dictionary<Vector2Int, GameObject>();

    void Start()
    {
        if (generateOnStart) GenerateDungeon();
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();

        if (roomPrefabs.Length == 0) return;

        GameObject startRoom = Instantiate(roomPrefabs[0], Vector3.zero, Quaternion.identity);
        startRoom.transform.parent = this.transform;
        RoomNode startNode = new RoomNode(startRoom, Vector2Int.zero);
        rooms.Add(startNode);
        RegisterRoom(startNode);
        openSlots.AddRange(GetDoorSlots(startRoom, Vector2Int.zero));

        int attempts = 0;
        while (openSlots.Count > 0 && rooms.Count < maxRooms && attempts < 1000)
        {
            attempts++;
            int slotIndex = Random.Range(0, openSlots.Count);
            DoorSlot slot = openSlots[slotIndex];

            if (TryPlaceRoom(slot))
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

    private bool TryPlaceRoom(DoorSlot slot)
    {
        Vector2Int targetGrid = slot.gridPosition + slot.direction;

        if (gridOccupied.ContainsKey(targetGrid)) return false;
        if (IsOutsideBounds(targetGrid)) return false;

        List<GameObject> compatibleRooms = GetCompatibleRooms(slot.direction);
        if (compatibleRooms.Count == 0) return false;

        GameObject chosen = compatibleRooms[Random.Range(0, compatibleRooms.Count)];
        Vector3 worldPos = new Vector3(targetGrid.x * (roomSize + spacing), 0, targetGrid.y * (roomSize + spacing));

        Quaternion rotation = GetRotationForDirection(slot.direction);
        GameObject newRoom = Instantiate(chosen, worldPos, rotation);
        newRoom.transform.parent = this.transform;

        RoomNode node = new RoomNode(newRoom, targetGrid);
        rooms.Add(node);
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

    private void CloseSlot(DoorSlot slot)
    {
        Vector3 worldPos = new Vector3(
            slot.gridPosition.x * (roomSize + spacing) + GetDoorWorldOffset(slot.direction).x,
            0,
            slot.gridPosition.y * (roomSize + spacing) + GetDoorWorldOffset(slot.direction).z
        );
        Quaternion rotation = GetRotationForDirection(slot.direction);
        Instantiate(wallPrefab, worldPos, rotation, this.transform);
    }

    private void CloseRemainingSlots()
    {
        foreach (var slot in openSlots)
        {
            CloseSlot(slot);
        }
        openSlots.Clear();
    }

    private List<DoorSlot> GetDoorSlots(GameObject room, Vector2Int gridPos)
    {
        List<DoorSlot> slots = new List<DoorSlot>();
        DoorPoint[] doors = room.GetComponentsInChildren<DoorPoint>();
        foreach (var door in doors)
        {
            slots.Add(new DoorSlot
            {
                gridPosition = gridPos,
                direction = door.direction,
                doorObject = door.gameObject
            });
        }
        return slots;
    }

    private List<GameObject> GetCompatibleRooms(Vector2Int direction)
    {
        List<GameObject> compatible = new List<GameObject>();
        foreach (var prefab in roomPrefabs)
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

    private Vector3 GetDoorWorldOffset(Vector2Int direction)
    {
        float half = (roomSize + spacing) / 2f;
        if (direction.x == 0 && direction.y == 1) return new Vector3(0, 0, half);
        if (direction.x == 0 && direction.y == -1) return new Vector3(0, 0, -half);
        if (direction.x == 1 && direction.y == 0) return new Vector3(half, 0, 0);
        if (direction.x == -1 && direction.y == 0) return new Vector3(-half, 0, 0);
        return Vector3.zero;
    }

    private bool IsOutsideBounds(Vector2Int pos)
    {
        int half = gridBounds / 2;
        return Mathf.Abs(pos.x) > half || Mathf.Abs(pos.y) > half;
    }

    private bool IsSlotConnectedToExisting(DoorSlot slot)
    {
        Vector2Int neighborPos = slot.gridPosition + slot.direction;
        return gridOccupied.ContainsKey(neighborPos);
    }

    private void RegisterRoom(RoomNode node)
    {
        gridOccupied[node.gridPosition] = node.roomObject;
    }

    private void ClearDungeon()
    {
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
        rooms.Clear();
        openSlots.Clear();
        gridOccupied.Clear();
    }

    private class RoomNode
    {
        public GameObject roomObject;
        public Vector2Int gridPosition;

        public RoomNode(GameObject obj, Vector2Int pos)
        {
            roomObject = obj;
            gridPosition = pos;
        }
    }

    private class DoorSlot
    {
        public Vector2Int gridPosition;
        public Vector2Int direction;
        public GameObject doorObject;
    }
}