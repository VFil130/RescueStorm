using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class DungeonLayout
{
    private readonly List<DungeonPlacedModule> modules;

    public int Seed { get; }
    public int LoopCount { get; }
    public int SearchSteps { get; }
    public RoomData EvacuationRoom { get; }
    public IReadOnlyList<DungeonPlacedModule> Modules => modules;
    public int RoomCount => modules.Count(module => module.Data.ModuleKind == RoomModuleKind.Room);
    public int CorridorCount => modules.Count(module => module.Data.ModuleKind == RoomModuleKind.Corridor);

    public DungeonLayout(
        int seed,
        int loopCount,
        int searchSteps,
        IEnumerable<DungeonPlacedModule> modules,
        RoomData evacuationRoom)
    {
        Seed = seed;
        LoopCount = loopCount;
        SearchSteps = searchSteps;
        EvacuationRoom = evacuationRoom;
        this.modules = modules.ToList();
    }

    public IEnumerable<DungeonDoorwayConnection> EnumerateDoorways()
    {
        HashSet<DoorPoint> visited = new HashSet<DoorPoint>();
        foreach (DungeonPlacedModule module in modules)
        {
            foreach (DoorPoint socket in module.Data.GetDoorPoints())
            {
                if (socket == null || socket.ConnectedTo == null || visited.Contains(socket)) continue;

                DoorPoint connected = socket.ConnectedTo;
                visited.Add(socket);
                visited.Add(connected);
                yield return new DungeonDoorwayConnection(module, socket, connected);
            }
        }
    }

    public DungeonGenerationResult CreateResult()
    {
        List<RoomData> allModules = modules.Select(module => module.Data).ToList();
        List<RoomData> rooms = allModules
            .Where(module => module.ModuleKind == RoomModuleKind.Room)
            .ToList();
        RoomData startRoom = allModules.Count > 0 ? allModules[0] : null;

        return new DungeonGenerationResult(
            Seed,
            LoopCount,
            rooms,
            allModules,
            startRoom,
            EvacuationRoom,
            rooms.Where(room => room.survivorSpawnPoint != null)
                .Select(room => room.survivorSpawnPoint)
                .ToList(),
            rooms.SelectMany(room => room.itemSpawnPoints)
                .Where(point => point != null)
                .ToList(),
            rooms.SelectMany(room => room.PlayerSpawnPoints)
                .Where(point => point != null)
                .ToList(),
            rooms.SelectMany(room => room.VentSpawnPoints)
                .Where(point => point != null)
                .ToList());
    }
}

internal static class DungeonObjectLifecycle
{
    public static void Destroy(GameObject target)
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            target.SetActive(false);
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

internal sealed class DungeonPlacedModule
{
    public RoomData Data { get; }
    public Bounds Bounds { get; }
    public int Depth { get; }
    public int ConsecutiveCorridors { get; }

    public DungeonPlacedModule(RoomData data, Bounds bounds, int depth, int consecutiveCorridors)
    {
        Data = data;
        Bounds = bounds;
        Depth = depth;
        ConsecutiveCorridors = consecutiveCorridors;
    }
}

internal readonly struct DungeonDoorwayConnection
{
    public DungeonPlacedModule Module { get; }
    public DoorPoint First { get; }
    public DoorPoint Second { get; }

    public DungeonDoorwayConnection(DungeonPlacedModule module, DoorPoint first, DoorPoint second)
    {
        Module = module;
        First = first;
        Second = second;
    }
}
