using System.Collections.Generic;
using UnityEngine;

public sealed class DungeonGenerationResult
{
    public int Seed { get; }
    public int LoopCount { get; }
    public IReadOnlyList<RoomData> Rooms { get; }
    public IReadOnlyList<RoomData> Modules { get; }
    public RoomData StartRoom { get; }
    public RoomData EvacuationRoom { get; }
    public IReadOnlyList<Transform> SurvivorSpawnPoints { get; }
    public IReadOnlyList<Transform> ItemSpawnPoints { get; }
    public IReadOnlyList<Transform> PlayerSpawnPoints { get; }
    public IReadOnlyList<Transform> EnemySpawnPoints { get; }

    public DungeonGenerationResult(
        int seed,
        int loopCount,
        IReadOnlyList<RoomData> rooms,
        IReadOnlyList<RoomData> modules,
        RoomData startRoom,
        RoomData evacuationRoom,
        IReadOnlyList<Transform> survivorSpawnPoints,
        IReadOnlyList<Transform> itemSpawnPoints,
        IReadOnlyList<Transform> playerSpawnPoints,
        IReadOnlyList<Transform> enemySpawnPoints)
    {
        Seed = seed;
        LoopCount = loopCount;
        Rooms = rooms;
        Modules = modules;
        StartRoom = startRoom;
        EvacuationRoom = evacuationRoom;
        SurvivorSpawnPoints = survivorSpawnPoints;
        ItemSpawnPoints = itemSpawnPoints;
        PlayerSpawnPoints = playerSpawnPoints;
        EnemySpawnPoints = enemySpawnPoints;
    }
}
