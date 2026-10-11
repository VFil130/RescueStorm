using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomData : MonoBehaviour
{
    [Header("Legacy grid data")]
    [Tooltip("Kept for compatibility with existing prefabs. The socket generator uses world bounds instead.")]
    public Vector2Int size = Vector2Int.one;

    [Header("Sockets")]
    [SerializeField] private List<DoorPoint> doors = new List<DoorPoint>();

    [Header("Placement")]
    [SerializeField] private RoomModuleKind moduleKind = RoomModuleKind.Room;
    [SerializeField, Min(0.01f)] private float selectionWeight = 1f;
    [SerializeField] private BoxCollider occupancyVolume;
    [SerializeField] private bool canBeEvacuationRoom = true;
    public Transform survivorSpawnPoint;
    public Transform miniGameSpawnPoint;
    public List<Transform> itemSpawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> playerSpawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> ventSpawnPoints = new List<Transform>();
    [SerializeField] private List<LeakSpawnPoint> leakSpawnPoints = new List<LeakSpawnPoint>();

    private Renderer[] cachedRenderers;
    private bool renderersCached;

    public float SelectionWeight => selectionWeight;
    public RoomModuleKind ModuleKind => moduleKind;
    public bool CanBeEvacuationRoom => canBeEvacuationRoom;
    public RoomRuntimeRole RuntimeRole { get; internal set; }
    public IReadOnlyList<Transform> PlayerSpawnPoints => playerSpawnPoints;
    public IReadOnlyList<Transform> VentSpawnPoints => ventSpawnPoints;
    public IReadOnlyList<LeakSpawnPoint> LeakSpawnPoints => GetLeakSpawnPoints();

    public IReadOnlyList<DoorPoint> GetDoorPoints()
    {
        doors.RemoveAll(door => door == null);
        if (doors.Count == 0) RefreshDoorPoints();

        return doors;
    }

    public IReadOnlyList<LeakSpawnPoint> GetLeakSpawnPoints()
    {
        leakSpawnPoints.RemoveAll(point => point == null);
        if (leakSpawnPoints.Count == 0) RefreshLeakSpawnPoints();

        return leakSpawnPoints;
    }

    public Bounds GetWorldBounds()
    {
        if (occupancyVolume != null)
        {
            return occupancyVolume.bounds;
        }

        if (!renderersCached)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            renderersCached = true;
        }

        if (cachedRenderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = cachedRenderers[0].bounds;
        for (int i = 1; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null) bounds.Encapsulate(cachedRenderers[i].bounds);
        }

        return bounds;
    }

    public void RefreshDoorPoints()
    {
        doors.Clear();
        doors.AddRange(GetComponentsInChildren<DoorPoint>(true));
    }

    public void RefreshLeakSpawnPoints()
    {
        leakSpawnPoints.Clear();
        leakSpawnPoints.AddRange(GetComponentsInChildren<LeakSpawnPoint>(true));
    }

    internal void ResetRuntimeState()
    {
        RuntimeRole = RoomRuntimeRole.Standard;
        foreach (DoorPoint door in GetDoorPoints())
        {
            door.ResetRuntimeState();
        }
    }
}

public enum RoomModuleKind
{
    Room = 0,
    Corridor = 1
}

[Flags]
public enum RoomRuntimeRole
{
    Standard = 0,
    Start = 1 << 0,
    Mandatory = 1 << 1,
    Evacuation = 1 << 2
}
