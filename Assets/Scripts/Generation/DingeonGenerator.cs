using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonGenerator : MonoBehaviour
{
    public const string GeneratedRootName = "__GeneratedLevel";

    private const float NavMeshDoorInset = 0.65f;
    private const float NavMeshDoorLinkWidth = 0f;
    private const float NavMeshSampleRadius = 0.35f;
    private const float NavMeshDoorLaneWidth = 1.5f;
    private const float NavMeshMinimumRegionArea = 2f;
    private const int NavMeshTileSize = 256;
    private const float NavMeshBreakMarkerSize = 0.35f;

    [Header("Room set")]
    [SerializeField] private GameObject[] roomPrefabs = Array.Empty<GameObject>();
    [SerializeField] private GameObject startRoomPrefab;
    [Tooltip("Rooms available only when an assigned quest requests them by name.")]
    [SerializeField] private GameObject[] questRoomPrefabs = Array.Empty<GameObject>();
    [SerializeField] private List<GameObject> mandatoryRoomPrefabs = new List<GameObject>();
    [FormerlySerializedAs("wallPrefab")]
    [SerializeField] private GameObject unusedDoorBlockerPrefab;

    [Header("Generation")]
    [FormerlySerializedAs("maxRooms")]
    [Tooltip("Number of gameplay rooms. Corridor modules are counted separately.")]
    [SerializeField, Min(2)] private int targetRoomCount = 20;
    [SerializeField, Min(0)] private int maxCorridorCount = 60;
    [SerializeField] private int seed;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField, Min(1)] private int generationAttempts = 8;
    [SerializeField, Min(100)] private int maxSearchSteps = 25000;
    [SerializeField, Min(0f)] private float overlapTolerance = 0.08f;
    [SerializeField, Min(0.001f)] private float socketPositionTolerance = 0.05f;
    [SerializeField, Range(0f, 15f)] private float socketAngleTolerance = 2f;
    [SerializeField, Min(0f)] private float floorHeightTolerance = 0.05f;

    [Header("Layout topology")]
    [FormerlySerializedAs("minimumLoopCount")]
    [SerializeField, Min(0)] private int desiredLoopCount = 3;
    [SerializeField, Min(1)] private int maxConsecutiveCorridors = 2;
    [SerializeField, Min(0f)] private float preferredLayoutRadius = 45f;
    [SerializeField, Min(0f)] private float branchDepthWeight = 3f;
    [SerializeField, Min(0f)] private float compactnessWeight = 1f;
    [SerializeField, Min(0f)] private float loopClosureBonus = 100f;

    [Header("Finishing")]
    [InspectorName("Close Empty Doors")]
    [SerializeField] private bool closeUnusedSockets = true;
    [InspectorName("Build NavMesh")]
    [SerializeField] private bool buildNavMeshAfterGeneration = true;
    [SerializeField, HideInInspector] private NavMeshSurface navMeshSurface;
    [InspectorName("Show NavMesh Issues")]
    [SerializeField] private bool showNavMeshBreakMarkers = true;

    private readonly List<GameObject> runtimeMandatoryRooms = new List<GameObject>();
    private Transform generatedRoot;
    private DungeonLayoutBuilder layoutBuilder;
    private DungeonLayout currentLayout;
    private DungeonNavigationBuilder navigationBuilder;
    private Coroutine navMeshValidationCoroutine;
    private bool automaticGenerationSuppressed;

    public int TargetRoomCount
    {
        get => targetRoomCount;
        set => targetRoomCount = Mathf.Max(2, value);
    }

    public int Seed
    {
        get => seed;
        set => seed = value;
    }

    public DungeonGenerationResult LastResult { get; private set; }
    public event Action<DungeonGenerationResult> GenerationCompleted;
    public event Action<string> GenerationFailed;

    private void Start()
    {
        if (generateOnStart && !automaticGenerationSuppressed)
            GenerateDungeon();
    }

    public void SuppressAutomaticGeneration()
    {
        automaticGenerationSuppressed = true;
    }

    public int CreateSessionSeed()
    {
        return seed == 0 ? CreateRandomSeed() : seed;
    }

    [ContextMenu("Generate Dungeon")]
    public bool GenerateDungeon()
    {
        return GenerateDungeon(CreateSessionSeed(), buildNavMeshAfterGeneration);
    }

    public bool GenerateDungeon(int generationSeed, bool buildNavigation)
    {
        List<string> errors = ValidateConfiguration();
        if (errors.Count > 0)
            return Fail(string.Join(Environment.NewLine, errors));

        ClearDungeon();
        int baseSeed = generationSeed == 0 ? CreateRandomSeed() : generationSeed;

        for (int attempt = 0; attempt < generationAttempts; attempt++)
        {
            int attemptSeed = unchecked(baseSeed + attempt * 7919);
            CreateGeneratedRoot();
            layoutBuilder = new DungeonLayoutBuilder(
                transform,
                generatedRoot,
                CreateLayoutSettings(),
                attemptSeed);

            if (!layoutBuilder.TryBuild())
            {
                DiscardCurrentLayout();
                continue;
            }

            layoutBuilder.ConnectRemainingSockets();
            int desiredLoops = layoutBuilder.DesiredLoopCount;
            if (layoutBuilder.LoopCount < desiredLoops && attempt < generationAttempts - 1)
            {
                DiscardCurrentLayout();
                continue;
            }

            if (!layoutBuilder.TryComplete(out currentLayout))
            {
                DiscardCurrentLayout();
                continue;
            }

            if (buildNavigation && buildNavMeshAfterGeneration)
                BuildRuntimeNavMesh();

            LastResult = currentLayout.CreateResult();
            GenerationCompleted?.Invoke(LastResult);
            LogGenerationSuccess(desiredLoops);
            return true;
        }

        return Fail(
            $"Could not generate exactly {targetRoomCount} gameplay rooms with at most " +
            $"{maxCorridorCount} corridors after {generationAttempts} attempts. " +
            "Check room sockets, bounds, mandatory rooms, or increase the search limits.");
    }

    public void SetMandatoryRooms(List<GameObject> rooms)
    {
        runtimeMandatoryRooms.Clear();
        if (rooms != null)
            runtimeMandatoryRooms.AddRange(rooms.Where(room => room != null));
    }

    public List<GameObject> ResolveRoomPrefabs(
        IEnumerable<string> roomNames,
        out List<string> missingRoomNames)
    {
        missingRoomNames = new List<string>();
        List<GameObject> resolved = new List<GameObject>();
        if (roomNames == null) return resolved;

        Dictionary<string, GameObject> prefabsByName = GetConfiguredRoomPrefabs()
            .GroupBy(prefab => NormalizePrefabName(prefab.name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (string roomName in roomNames
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string normalizedName = NormalizePrefabName(roomName);
            if (prefabsByName.TryGetValue(normalizedName, out GameObject prefab))
                resolved.Add(prefab);
            else
                missingRoomNames.Add(roomName);
        }

        return resolved;
    }

    public List<Vector3> GetRandomRoomPositions(int count)
    {
        return layoutBuilder != null
            ? layoutBuilder.GetRandomRoomPositions(count)
            : new List<Vector3>();
    }

    [ContextMenu("Clear Dungeon")]
    public void ClearDungeon()
    {
        CancelPendingNavMeshValidation();
        LastResult = null;
        navigationBuilder?.RemoveDoorLinks();
        navigationBuilder = null;
        currentLayout = null;
        layoutBuilder = null;

        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot != null)
            DungeonObjectLifecycle.Destroy(existingRoot.gameObject);

        generatedRoot = null;
    }

    public IReadOnlyList<GameObject> FindLegacyGeneratedChildren()
    {
        List<GameObject> legacyChildren = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name == GeneratedRootName) continue;
            if (IsLegacyGeneratedChild(child.gameObject))
                legacyChildren.Add(child.gameObject);
        }

        return legacyChildren;
    }

    public List<string> ValidateConfiguration()
    {
        List<string> errors = new List<string>();
        List<GameObject> allPrefabs = GetConfiguredRoomPrefabs();
        List<GameObject> mandatoryRooms = GetMandatoryRoomPrefabs();

        if (targetRoomCount < 2)
            errors.Add("Target room count must be at least 2 rooms.");
        if (roomPrefabs == null || roomPrefabs.Length == 0)
            errors.Add("At least one regular room prefab is required.");
        if (allPrefabs.Count == 0)
            errors.Add("No valid room prefabs are configured.");
        if (!GetPlacementPrefabs().Any(prefab =>
                prefab.GetComponent<RoomData>() is RoomData data &&
                data.ModuleKind == RoomModuleKind.Room &&
                data.CanBeEvacuationRoom))
            errors.Add("At least one room prefab must allow the evacuation role.");

        int mandatoryCount = mandatoryRooms
            .Count(room => room != null &&
                           room.GetComponent<RoomData>()?.ModuleKind == RoomModuleKind.Room);
        GameObject configuredStart = startRoomPrefab != null
            ? startRoomPrefab
            : roomPrefabs?.FirstOrDefault(room => room != null);
        if (configuredStart != null &&
            configuredStart.GetComponent<RoomData>()?.ModuleKind != RoomModuleKind.Room)
            errors.Add("The start room prefab must be classified as Room, not Corridor.");
        if (configuredStart != null &&
            configuredStart.GetComponent<RoomData>()?.ModuleKind == RoomModuleKind.Room &&
            mandatoryRooms.Contains(configuredStart))
            mandatoryCount--;
        if (targetRoomCount < mandatoryCount + 1)
        {
            errors.Add(
                $"Target room count {targetRoomCount} is too small for the start room and " +
                $"{mandatoryCount} mandatory rooms.");
        }

        foreach (GameObject prefab in allPrefabs)
            ValidateRoomPrefab(prefab, errors, buildNavMeshAfterGeneration);

        return errors.Distinct().ToList();
    }

    private DungeonLayoutSettings CreateLayoutSettings()
    {
        return new DungeonLayoutSettings
        {
            RoomPrefabs = roomPrefabs ?? Array.Empty<GameObject>(),
            StartRoomPrefab = startRoomPrefab,
            MandatoryRoomPrefabs = GetMandatoryRoomPrefabs(),
            UnusedDoorBlockerPrefab = unusedDoorBlockerPrefab,
            TargetRoomCount = targetRoomCount,
            MaxCorridorCount = maxCorridorCount,
            MaxSearchSteps = maxSearchSteps,
            OverlapTolerance = overlapTolerance,
            SocketPositionTolerance = socketPositionTolerance,
            SocketAngleTolerance = socketAngleTolerance,
            FloorHeightTolerance = floorHeightTolerance,
            DesiredLoopCount = desiredLoopCount,
            MaxConsecutiveCorridors = maxConsecutiveCorridors,
            PreferredLayoutRadius = preferredLayoutRadius,
            BranchDepthWeight = branchDepthWeight,
            CompactnessWeight = compactnessWeight,
            LoopClosureBonus = loopClosureBonus,
            CloseUnusedSockets = closeUnusedSockets
        };
    }

    private DungeonNavigationSettings CreateNavigationSettings()
    {
        return new DungeonNavigationSettings
        {
            DoorInset = NavMeshDoorInset,
            DoorLinkWidth = NavMeshDoorLinkWidth,
            SampleRadius = NavMeshSampleRadius,
            DoorLaneWidth = NavMeshDoorLaneWidth,
            MinimumRegionArea = NavMeshMinimumRegionArea,
            TileSize = NavMeshTileSize
        };
    }

    private void BuildRuntimeNavMesh()
    {
        navigationBuilder = new DungeonNavigationBuilder(
            this,
            generatedRoot,
            currentLayout,
            CreateNavigationSettings(),
            navMeshSurface);

        if (!navigationBuilder.TryBuild())
            return;

        navMeshSurface = navigationBuilder.Surface;
        CancelPendingNavMeshValidation();
        if (Application.isPlaying && isActiveAndEnabled)
        {
            navMeshValidationCoroutine = StartCoroutine(
                ValidateRuntimeNavMeshNextFrame());
        }
        else
        {
            navigationBuilder.LogConnectivity();
        }
    }

    private IEnumerator ValidateRuntimeNavMeshNextFrame()
    {
        yield return null;
        navMeshValidationCoroutine = null;
        navigationBuilder?.LogConnectivity();
    }

    private void OnDrawGizmos()
    {
        navigationBuilder?.DrawGizmos(showNavMeshBreakMarkers, NavMeshBreakMarkerSize);
    }

    private void OnDestroy()
    {
        CancelPendingNavMeshValidation();
        navigationBuilder?.RemoveDoorLinks();
    }

    private void CancelPendingNavMeshValidation()
    {
        if (navMeshValidationCoroutine == null) return;

        StopCoroutine(navMeshValidationCoroutine);
        navMeshValidationCoroutine = null;
    }

    private void LogGenerationSuccess(int desiredLoops)
    {
        Debug.Log(
            $"Dungeon generated: {currentLayout.RoomCount} rooms + " +
            $"{currentLayout.CorridorCount} corridors, {currentLayout.LoopCount} loops, " +
            $"seed {currentLayout.Seed}, search steps {currentLayout.SearchSteps}.",
            this);

        if (currentLayout.LoopCount < desiredLoops)
        {
            Debug.LogWarning(
                $"Generated layout has {currentLayout.LoopCount}/{desiredLoops} desired loops. " +
                "The bounded retry budget was exhausted; the usable layout was kept instead of " +
                "stalling generation.",
                this);
        }
    }

    private void CreateGeneratedRoot()
    {
        GameObject root = new GameObject(GeneratedRootName);
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private void DiscardCurrentLayout()
    {
        navigationBuilder?.RemoveDoorLinks();
        navigationBuilder = null;
        currentLayout = null;
        layoutBuilder = null;

        if (generatedRoot != null)
        {
            DungeonObjectLifecycle.Destroy(generatedRoot.gameObject);
            generatedRoot = null;
        }
    }

    private List<GameObject> GetConfiguredRoomPrefabs()
    {
        return EnumeratePlacementPrefabs()
            .Concat(questRoomPrefabs ?? Array.Empty<GameObject>())
            .Append(startRoomPrefab)
            .Where(prefab => prefab != null)
            .Distinct()
            .ToList();
    }

    private bool IsLegacyGeneratedChild(GameObject child)
    {
        string childName = NormalizePrefabName(child.name);
        if (childName.StartsWith("SocketCap_", StringComparison.Ordinal)) return true;
        if (unusedDoorBlockerPrefab != null && childName == unusedDoorBlockerPrefab.name) return true;

        return GetConfiguredRoomPrefabs().Any(prefab => childName == prefab.name);
    }

    private static string NormalizePrefabName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("(Clone)", string.Empty).Trim();
    }

    private List<GameObject> GetPlacementPrefabs()
    {
        return EnumeratePlacementPrefabs()
            .Where(prefab => prefab != null)
            .Distinct()
            .ToList();
    }

    private IEnumerable<GameObject> EnumeratePlacementPrefabs()
    {
        IEnumerable<GameObject> regularRooms = roomPrefabs ?? Array.Empty<GameObject>();
        return regularRooms.Concat(GetMandatoryRoomPrefabs());
    }

    private List<GameObject> GetMandatoryRoomPrefabs()
    {
        IEnumerable<GameObject> configuredRooms = mandatoryRoomPrefabs ?? new List<GameObject>();
        return configuredRooms.Concat(runtimeMandatoryRooms).ToList();
    }

    private static void ValidateRoomPrefab(
        GameObject prefab,
        ICollection<string> errors,
        bool requireNavigationGeometry)
    {
        RoomData data = prefab.GetComponent<RoomData>();
        if (data == null)
        {
            errors.Add($"Room prefab '{prefab.name}' needs RoomData on its root.");
            return;
        }

        IReadOnlyList<DoorPoint> sockets = data.GetDoorPoints();
        if (sockets.Count == 0)
            errors.Add($"Room prefab '{prefab.name}' has no DoorPoint sockets.");
        if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            errors.Add($"Room prefab '{prefab.name}' has no renderers and no usable automatic bounds.");
        if (requireNavigationGeometry && prefab.GetComponentsInChildren<Collider>(true).Length == 0)
            errors.Add($"Room prefab '{prefab.name}' has no colliders for runtime NavMesh generation.");
        if (prefab.GetComponentsInChildren<NetworkBehaviour>(true).Length > 0 ||
            prefab.GetComponentsInChildren<NetworkIdentity>(true).Length > 0)
        {
            errors.Add(
                $"Room prefab '{prefab.name}' contains a NetworkBehaviour. " +
                "Generated room modules must contain only local geometry and spawn markers; " +
                "the server must spawn networked gameplay objects separately.");
        }

        foreach (DoorPoint socket in sockets)
        {
            if (socket == null) continue;
            if (!socket.UsesTransformForward && socket.direction == Vector2Int.zero)
                errors.Add($"Room prefab '{prefab.name}' has a DoorPoint with a zero direction.");
        }
    }

    private bool Fail(string message)
    {
        Debug.LogError(message, this);
        GenerationFailed?.Invoke(message);
        return false;
    }

    private static int CreateRandomSeed()
    {
        int generatedSeed = unchecked(Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode());
        return generatedSeed == 0 ? 1 : generatedSeed;
    }
}
