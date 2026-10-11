using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class DungeonLayoutSettings
{
    public IReadOnlyList<GameObject> RoomPrefabs { get; set; }
    public GameObject StartRoomPrefab { get; set; }
    public IReadOnlyList<GameObject> MandatoryRoomPrefabs { get; set; }
    public GameObject UnusedDoorBlockerPrefab { get; set; }
    public int TargetRoomCount { get; set; }
    public int MaxCorridorCount { get; set; }
    public int MaxSearchSteps { get; set; }
    public float OverlapTolerance { get; set; }
    public float SocketPositionTolerance { get; set; }
    public float SocketAngleTolerance { get; set; }
    public float FloorHeightTolerance { get; set; }
    public int DesiredLoopCount { get; set; }
    public int MaxConsecutiveCorridors { get; set; }
    public float PreferredLayoutRadius { get; set; }
    public float BranchDepthWeight { get; set; }
    public float CompactnessWeight { get; set; }
    public float LoopClosureBonus { get; set; }
    public bool CloseUnusedSockets { get; set; }
}

internal sealed class DungeonLayoutBuilder
{
    private readonly Transform origin;
    private readonly Transform generatedRoot;
    private readonly DungeonLayoutSettings settings;
    private readonly int seed;
    private readonly System.Random random;
    private readonly List<DungeonPlacedModule> modules = new List<DungeonPlacedModule>();
    private Dictionary<GameObject, int> remainingMandatoryRooms;
    private readonly Dictionary<GameObject, Stack<GameObject>> instancePool =
        new Dictionary<GameObject, Stack<GameObject>>();
    private List<GameObject> placementPrefabs;
    private int searchSteps;
    private int loopCount;

    public int RoomCount => modules.Count(module => module.Data.ModuleKind == RoomModuleKind.Room);
    public int CorridorCount => modules.Count(module => module.Data.ModuleKind == RoomModuleKind.Corridor);
    public int LoopCount => loopCount;
    public int SearchSteps => searchSteps;
    public int DesiredLoopCount => Mathf.Min(settings.DesiredLoopCount, settings.TargetRoomCount / 5);

    public DungeonLayoutBuilder(
        Transform origin,
        Transform generatedRoot,
        DungeonLayoutSettings settings,
        int seed)
    {
        this.origin = origin;
        this.generatedRoot = generatedRoot;
        this.settings = settings;
        this.seed = seed;
        random = new System.Random(seed);
        ResetMandatoryRoomCounts();
    }

    public bool TryBuild()
    {
        if (!CreateStartRoom()) return false;

        bool succeeded = TryGrowLayout();
        return succeeded &&
               RoomCount == settings.TargetRoomCount &&
               CountRemainingMandatoryModules() == 0;
    }

    public void ConnectRemainingSockets()
    {
        loopCount += ConnectRemainingMatchingSockets();
    }

    public bool TryComplete(out DungeonLayout layout)
    {
        layout = null;
        RoomData evacuationRoom = AssignEvacuationRoom();
        if (evacuationRoom == null) return false;

        SealUnusedSockets();
        layout = new DungeonLayout(seed, loopCount, searchSteps, modules, evacuationRoom);
        return true;
    }

    private RoomData AssignEvacuationRoom()
    {
        DungeonPlacedModule startRoom = modules.FirstOrDefault(module =>
            module.Depth == 0 &&
            module.Data.ModuleKind == RoomModuleKind.Room &&
            module.Data.CanBeEvacuationRoom);

        DungeonPlacedModule evacuation = startRoom ?? modules
            .Where(module => module.Depth > 0 &&
                             module.Data.ModuleKind == RoomModuleKind.Room &&
                             module.Data.CanBeEvacuationRoom)
            .OrderByDescending(module => module.Depth)
            .ThenByDescending(module =>
                Vector3.SqrMagnitude(module.Data.transform.position - origin.position))
            .FirstOrDefault();

        if (evacuation != null)
            evacuation.Data.RuntimeRole |= RoomRuntimeRole.Evacuation;

        return evacuation?.Data;
    }

    private void SealUnusedSockets()
    {
        if (!settings.CloseUnusedSockets) return;

        foreach (OpenSocket openSocket in CollectOpenSockets())
        {
            if (settings.UnusedDoorBlockerPrefab != null)
            {
                Vector3 direction = openSocket.Socket.GetWorldDirection(openSocket.Module.Data.transform);
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                GameObject wall = UnityEngine.Object.Instantiate(
                    settings.UnusedDoorBlockerPrefab,
                    openSocket.Socket.transform.position,
                    rotation,
                    generatedRoot);
                wall.name = $"SocketCap_{openSocket.Module.Data.name}";
            }

            openSocket.Socket.Seal();
        }
    }

    public List<Vector3> GetRandomRoomPositions(int count)
    {
        List<Vector3> positions = modules
            .Where(module => module.Data.ModuleKind == RoomModuleKind.Room)
            .Select(module => module.Data.transform.position)
            .ToList();
        Shuffle(positions);
        return positions.Take(Mathf.Max(0, count)).ToList();
    }

    private bool TryGrowLayout()
    {
        if (++searchSteps > settings.MaxSearchSteps) return false;

        bool roomGoalReached = RoomCount == settings.TargetRoomCount &&
                               CountRemainingMandatoryModules() == 0;
        if (roomGoalReached) return true;

        int roomsLeft = settings.TargetRoomCount - RoomCount;
        if (roomsLeft < 0 || CountRemainingMandatoryRooms() > roomsLeft) return false;
        if (CorridorCount > settings.MaxCorridorCount) return false;

        foreach (OpenSocket target in OrderOpenSockets(CollectOpenSockets()))
        {
            foreach (GameObject prefab in BuildCandidatePrefabs(roomsLeft, target))
            {
                if (TryPlaceModule(prefab, target)) return true;
            }
        }

        return false;
    }

    private bool TryPlaceModule(GameObject prefab, OpenSocket target)
    {
        GameObject instance = RentInstance(prefab);

        RoomData data = instance.GetComponent<RoomData>();
        if (data == null)
        {
            DungeonObjectLifecycle.Destroy(instance);
            return false;
        }

        data.ResetRuntimeState();
        List<DoorPoint> candidateSockets = OrderCandidateSockets(data, target);

        foreach (DoorPoint candidateSocket in candidateSockets)
        {
            if (++searchSteps > settings.MaxSearchSteps) break;

            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (!AlignSockets(target, data, candidateSocket)) continue;

            Bounds bounds = data.GetWorldBounds();
            if (OverlapsPlacedModules(bounds)) continue;

            bool consumedMandatory = TryConsumeMandatory(prefab);
            data.RuntimeRole = consumedMandatory ? RoomRuntimeRole.Mandatory : RoomRuntimeRole.Standard;

            target.Socket.Connect(candidateSocket);
            candidateSocket.Connect(target.Socket);

            int consecutiveCorridors = data.ModuleKind == RoomModuleKind.Corridor
                ? target.Module.ConsecutiveCorridors + 1
                : 0;
            DungeonPlacedModule placed = new DungeonPlacedModule(
                data,
                bounds,
                target.Module.Depth + 1,
                consecutiveCorridors);
            modules.Add(placed);

            List<SocketConnection> loopConnections = ConnectMatchingOpenSockets(placed, candidateSocket);
            loopCount += loopConnections.Count;

            if (TryGrowLayout()) return true;

            DisconnectSockets(loopConnections);
            loopCount -= loopConnections.Count;
            modules.RemoveAt(modules.Count - 1);
            target.Socket.Disconnect(candidateSocket);
            candidateSocket.Disconnect(target.Socket);
            data.RuntimeRole = RoomRuntimeRole.Standard;
            if (consumedMandatory) RestoreMandatory(prefab);
        }

        ReturnInstance(prefab, instance);
        return false;
    }

    private bool AlignSockets(OpenSocket target, RoomData candidateRoom, DoorPoint candidateSocket)
    {
        Vector3 targetDirection = target.Socket.GetWorldDirection(target.Module.Data.transform);
        Vector3 candidateDirection = candidateSocket.GetWorldDirection(candidateRoom.transform);
        float rawAngle = Vector3.SignedAngle(candidateDirection, -targetDirection, Vector3.up);
        float snappedAngle = Mathf.Round(rawAngle / 90f) * 90f;

        if (Mathf.Abs(Mathf.DeltaAngle(rawAngle, snappedAngle)) > settings.SocketAngleTolerance)
            return false;

        candidateRoom.transform.rotation = Quaternion.AngleAxis(snappedAngle, Vector3.up);
        candidateRoom.transform.position += target.Socket.transform.position - candidateSocket.transform.position;

        if (Mathf.Abs(candidateRoom.transform.position.y - target.Module.Data.transform.position.y) >
            settings.FloorHeightTolerance)
            return false;

        float positionError = Vector3.Distance(
            target.Socket.transform.position,
            candidateSocket.transform.position);
        float directionDot = Vector3.Dot(
            target.Socket.GetWorldDirection(target.Module.Data.transform),
            candidateSocket.GetWorldDirection(candidateRoom.transform));

        return positionError <= settings.SocketPositionTolerance && directionDot <= -0.999f;
    }

    private bool OverlapsPlacedModules(Bounds candidateBounds)
    {
        Bounds candidate = ShrinkBounds(candidateBounds, settings.OverlapTolerance);
        foreach (DungeonPlacedModule module in modules)
        {
            Bounds existing = ShrinkBounds(module.Bounds, settings.OverlapTolerance);
            if (candidate.Intersects(existing)) return true;
        }

        return false;
    }

    private static Bounds ShrinkBounds(Bounds bounds, float amount)
    {
        Vector3 size = bounds.size;
        size.x = Mathf.Max(0.01f, size.x - amount * 2f);
        size.y = Mathf.Max(0.01f, size.y - amount * 2f);
        size.z = Mathf.Max(0.01f, size.z - amount * 2f);
        bounds.size = size;
        return bounds;
    }

    private bool CreateStartRoom()
    {
        GameObject prefab = settings.StartRoomPrefab != null
            ? settings.StartRoomPrefab
            : settings.RoomPrefabs.FirstOrDefault(room => room != null);
        if (prefab == null) return false;

        GameObject instance = UnityEngine.Object.Instantiate(
            prefab,
            origin.position,
            origin.rotation,
            generatedRoot);
        instance.name = prefab.name;
        RoomData data = instance.GetComponent<RoomData>();
        if (data == null)
        {
            DungeonObjectLifecycle.Destroy(instance);
            return false;
        }

        data.ResetRuntimeState();
        bool consumedMandatory = TryConsumeMandatory(prefab);
        data.RuntimeRole = RoomRuntimeRole.Start;
        if (consumedMandatory) data.RuntimeRole |= RoomRuntimeRole.Mandatory;
        modules.Add(new DungeonPlacedModule(data, data.GetWorldBounds(), 0, 0));
        return true;
    }

    private List<OpenSocket> CollectOpenSockets()
    {
        List<OpenSocket> sockets = new List<OpenSocket>();
        foreach (DungeonPlacedModule module in modules)
        {
            foreach (DoorPoint socket in module.Data.GetDoorPoints())
            {
                if (socket != null && !socket.IsConnected && !socket.IsSealed)
                    sockets.Add(new OpenSocket(module, socket));
            }
        }

        return sockets;
    }

    private List<OpenSocket> OrderOpenSockets(List<OpenSocket> sockets)
    {
        Vector3 layoutCenter = CalculateLayoutCenter();
        return sockets
            .Select(socket => new ScoredOpenSocket(
                socket,
                socket.Module.Depth * settings.BranchDepthWeight +
                HorizontalDistance(socket.Socket.transform.position, layoutCenter) * settings.CompactnessWeight +
                (float)random.NextDouble()))
            .OrderBy(entry => entry.Score)
            .Select(entry => entry.Socket)
            .ToList();
    }

    private List<DoorPoint> OrderCandidateSockets(RoomData candidateRoom, OpenSocket target)
    {
        Vector3 layoutCenter = CalculateLayoutCenter();
        Vector3 startPosition = modules.Count > 0 ? modules[0].Bounds.center : origin.position;
        List<ScoredDoorPoint> scored = new List<ScoredDoorPoint>();
        // The placed layout does not change while scoring, so the open sockets are identical for every door.
        List<OpenSocket> existingOpenSockets = CollectOpenSockets();

        foreach (DoorPoint socket in candidateRoom.GetDoorPoints().Where(socket => socket != null))
        {
            candidateRoom.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (!AlignSockets(target, candidateRoom, socket)) continue;

            Bounds bounds = candidateRoom.GetWorldBounds();
            if (OverlapsPlacedModules(bounds)) continue;

            int closures = CountPotentialLoopConnections(candidateRoom, socket, target, existingOpenSockets);
            float centerDistance = HorizontalDistance(bounds.center, layoutCenter);
            float radialDistance = HorizontalDistance(bounds.center, startPosition);
            float outsidePreferredRadius = Mathf.Max(0f, radialDistance - settings.PreferredLayoutRadius);
            float score = closures * settings.LoopClosureBonus -
                          centerDistance * settings.CompactnessWeight -
                          outsidePreferredRadius * settings.CompactnessWeight * 4f +
                          (float)random.NextDouble();
            scored.Add(new ScoredDoorPoint(socket, score));
        }

        candidateRoom.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return scored
            .OrderByDescending(entry => entry.Score)
            .Select(entry => entry.Socket)
            .ToList();
    }

    private Vector3 CalculateLayoutCenter()
    {
        if (modules.Count == 0) return origin.position;

        Vector3 sum = Vector3.zero;
        foreach (DungeonPlacedModule module in modules) sum += module.Bounds.center;
        return sum / modules.Count;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private int CountPotentialLoopConnections(
        RoomData candidateRoom,
        DoorPoint attachmentSocket,
        OpenSocket target,
        List<OpenSocket> existingOpenSockets)
    {
        int count = 0;
        foreach (DoorPoint candidateSocket in candidateRoom.GetDoorPoints())
        {
            if (candidateSocket == null || candidateSocket == attachmentSocket) continue;

            foreach (OpenSocket existing in existingOpenSockets)
            {
                if (existing.Socket == target.Socket) continue;
                if (!AreSocketsCoincidentAndOpposed(
                        candidateSocket,
                        candidateRoom.transform,
                        existing.Socket,
                        existing.Module.Data.transform)) continue;

                count++;
                break;
            }
        }

        return count;
    }

    private List<SocketConnection> ConnectMatchingOpenSockets(
        DungeonPlacedModule placed,
        DoorPoint attachmentSocket)
    {
        List<SocketConnection> connections = new List<SocketConnection>();
        List<OpenSocket> openSockets = CollectOpenSockets();
        foreach (DoorPoint candidateSocket in placed.Data.GetDoorPoints())
        {
            if (candidateSocket == null || candidateSocket == attachmentSocket || candidateSocket.IsConnected)
                continue;

            OpenSocket match = openSockets.FirstOrDefault(existing =>
                existing.Module != placed &&
                AreSocketsCoincidentAndOpposed(
                    candidateSocket,
                    placed.Data.transform,
                    existing.Socket,
                    existing.Module.Data.transform));
            if (match.Socket == null) continue;

            candidateSocket.Connect(match.Socket);
            match.Socket.Connect(candidateSocket);
            connections.Add(new SocketConnection(candidateSocket, match.Socket));
        }

        return connections;
    }

    private static void DisconnectSockets(List<SocketConnection> connections)
    {
        for (int index = connections.Count - 1; index >= 0; index--)
        {
            SocketConnection connection = connections[index];
            connection.First.Disconnect(connection.Second);
            connection.Second.Disconnect(connection.First);
        }
    }

    private bool AreSocketsCoincidentAndOpposed(
        DoorPoint first,
        Transform firstRoom,
        DoorPoint second,
        Transform secondRoom)
    {
        if (first == null || second == null || first.IsConnected || second.IsConnected) return false;
        if (Vector3.Distance(first.transform.position, second.transform.position) >
            settings.SocketPositionTolerance)
            return false;

        Vector3 firstDirection = first.GetWorldDirection(firstRoom);
        Vector3 secondDirection = second.GetWorldDirection(secondRoom);
        return Vector3.Dot(firstDirection, secondDirection) <= -0.999f;
    }

    private List<GameObject> BuildCandidatePrefabs(int roomsLeft, OpenSocket target)
    {
        IEnumerable<GameObject> eligiblePrefabs;
        if (roomsLeft > 0 && CountRemainingMandatoryRooms() >= roomsLeft)
        {
            IEnumerable<GameObject> requiredRooms = remainingMandatoryRooms
                .Where(pair => pair.Value > 0 &&
                               pair.Key.GetComponent<RoomData>()?.ModuleKind == RoomModuleKind.Room)
                .Select(pair => pair.Key);
            IEnumerable<GameObject> corridors = GetPlacementPrefabs()
                .Where(prefab => prefab.GetComponent<RoomData>()?.ModuleKind == RoomModuleKind.Corridor);
            eligiblePrefabs = requiredRooms.Concat(corridors);
        }
        else
        {
            eligiblePrefabs = GetPlacementPrefabs();
        }

        int corridorCount = CorridorCount;
        List<GameObject> weightedPrefabs = new List<GameObject>();
        foreach (GameObject prefab in eligiblePrefabs.Where(prefab => prefab != null).Distinct())
        {
            RoomData data = prefab.GetComponent<RoomData>();
            if (data == null) continue;
            if (data.ModuleKind == RoomModuleKind.Room && roomsLeft <= 0) continue;
            if (data.ModuleKind == RoomModuleKind.Corridor && corridorCount >= settings.MaxCorridorCount) continue;
            if (data.ModuleKind == RoomModuleKind.Corridor &&
                target.Module.ConsecutiveCorridors >= settings.MaxConsecutiveCorridors) continue;

            int weight = Mathf.Clamp(Mathf.RoundToInt(data.SelectionWeight), 1, 10);
            for (int i = 0; i < weight; i++) weightedPrefabs.Add(prefab);
        }

        Shuffle(weightedPrefabs);
        bool preferGameplayRoom = roomsLeft > 0 &&
                                  (target.Module.ConsecutiveCorridors > 0 || corridorCount >= RoomCount);
        return weightedPrefabs
            .Distinct()
            .OrderByDescending(prefab =>
                preferGameplayRoom && prefab.GetComponent<RoomData>()?.ModuleKind == RoomModuleKind.Room)
            .ToList();
    }

    private void ResetMandatoryRoomCounts()
    {
        remainingMandatoryRooms = new Dictionary<GameObject, int>();
        foreach (GameObject prefab in settings.MandatoryRoomPrefabs)
        {
            if (prefab == null) continue;
            remainingMandatoryRooms.TryGetValue(prefab, out int count);
            remainingMandatoryRooms[prefab] = count + 1;
        }
    }

    private bool TryConsumeMandatory(GameObject prefab)
    {
        if (!remainingMandatoryRooms.TryGetValue(prefab, out int count) || count <= 0)
            return false;

        remainingMandatoryRooms[prefab] = count - 1;
        return true;
    }

    private void RestoreMandatory(GameObject prefab)
    {
        remainingMandatoryRooms.TryGetValue(prefab, out int count);
        remainingMandatoryRooms[prefab] = count + 1;
    }

    private int CountRemainingMandatoryModules()
    {
        return remainingMandatoryRooms.Values.Sum();
    }

    private int CountRemainingMandatoryRooms()
    {
        return remainingMandatoryRooms
            .Where(pair => pair.Key != null &&
                           pair.Key.GetComponent<RoomData>()?.ModuleKind == RoomModuleKind.Room)
            .Sum(pair => pair.Value);
    }

    private int ConnectRemainingMatchingSockets()
    {
        int connectionsCreated = 0;
        List<OpenSocket> openSockets = CollectOpenSockets();
        for (int firstIndex = 0; firstIndex < openSockets.Count; firstIndex++)
        {
            OpenSocket first = openSockets[firstIndex];
            if (first.Socket.IsConnected) continue;

            for (int secondIndex = firstIndex + 1; secondIndex < openSockets.Count; secondIndex++)
            {
                OpenSocket second = openSockets[secondIndex];
                if (second.Socket.IsConnected || first.Module == second.Module) continue;
                if (!AreSocketsCoincidentAndOpposed(
                        first.Socket,
                        first.Module.Data.transform,
                        second.Socket,
                        second.Module.Data.transform)) continue;

                first.Socket.Connect(second.Socket);
                second.Socket.Connect(first.Socket);
                connectionsCreated++;
                break;
            }
        }

        return connectionsCreated;
    }

    private List<GameObject> GetPlacementPrefabs()
    {
        if (placementPrefabs == null)
        {
            placementPrefabs = settings.RoomPrefabs
                .Concat(settings.MandatoryRoomPrefabs)
                .Where(prefab => prefab != null)
                .Distinct()
                .ToList();
        }

        return placementPrefabs;
    }

    private GameObject RentInstance(GameObject prefab)
    {
        if (instancePool.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Pop();
                if (pooled != null)
                {
                    pooled.SetActive(true);
                    return pooled;
                }
            }
        }

        GameObject instance = UnityEngine.Object.Instantiate(
            prefab,
            Vector3.zero,
            Quaternion.identity,
            generatedRoot);
        instance.name = prefab.name;
        return instance;
    }

    private void ReturnInstance(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;

        RoomData data = instance.GetComponent<RoomData>();
        if (data != null) data.ResetRuntimeState();

        instance.SetActive(false);
        if (!instancePool.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            instancePool[prefab] = pool;
        }

        pool.Push(instance);
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private readonly struct ScoredOpenSocket
    {
        public OpenSocket Socket { get; }
        public float Score { get; }

        public ScoredOpenSocket(OpenSocket socket, float score)
        {
            Socket = socket;
            Score = score;
        }
    }

    private readonly struct ScoredDoorPoint
    {
        public DoorPoint Socket { get; }
        public float Score { get; }

        public ScoredDoorPoint(DoorPoint socket, float score)
        {
            Socket = socket;
            Score = score;
        }
    }

    private readonly struct SocketConnection
    {
        public DoorPoint First { get; }
        public DoorPoint Second { get; }

        public SocketConnection(DoorPoint first, DoorPoint second)
        {
            First = first;
            Second = second;
        }
    }

    private readonly struct OpenSocket
    {
        public DungeonPlacedModule Module { get; }
        public DoorPoint Socket { get; }

        public OpenSocket(DungeonPlacedModule module, DoorPoint socket)
        {
            Module = module;
            Socket = socket;
        }
    }
}
