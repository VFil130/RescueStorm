using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(DungeonGenerator), typeof(SurvivorManager))]
[RequireComponent(typeof(GameManager))]
public sealed class DungeonNetworkSessionCoordinator : NetworkBehaviour
{
    private const char QuestIdSeparator = '\n';

    [SerializeField] private DungeonGenerator generator;
    [SerializeField] private SurvivorManager survivorManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject evacuationZonePrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField, Min(0)] private int enemyCount = 1;
    [SerializeField] private bool spawnGameplayContent = true;
    [SerializeField, Min(1f)] private float clientGenerationTimeout = 20f;

    [SyncVar] private int sessionSeed;
    [SyncVar] private string assignedQuestIds = string.Empty;
    [SyncVar] private uint serverLayoutHash;
    [SyncVar(hook = nameof(OnSessionRevisionChanged))]
    private uint sessionRevision;

    private readonly HashSet<int> waitingForClients = new HashSet<int>();
    private Coroutine clientGenerationCoroutine;
    private Coroutine clientTimeoutCoroutine;
    private uint queuedClientRevision;
    private uint appliedClientRevision;
    private bool contentSpawned;

    private void Awake()
    {
        ResolveReferences();
        generator?.SuppressAutomaticGeneration();
    }

    private void ResolveReferences()
    {
        if (generator == null) generator = GetComponent<DungeonGenerator>();
        if (survivorManager == null) survivorManager = GetComponent<SurvivorManager>();
        if (gameManager == null) gameManager = GetComponent<GameManager>();
    }

    public override void OnStartServer()
    {
        ResolveReferences();
        if (!TryGenerateServerLayout()) return;

        RegisterPlayerSpawnPoints(generator.LastResult);
        RememberConnectedClients();
        if (waitingForClients.Count == 0)
        {
            SpawnGameplayContent();
            return;
        }

        clientTimeoutCoroutine = StartCoroutine(WaitForClientGeneration());
    }

    public override void OnStartClient()
    {
        if (!isServer && sessionRevision != 0)
            QueueClientGeneration(sessionRevision);
    }

    public override void OnStopServer()
    {
        if (clientTimeoutCoroutine != null)
            StopCoroutine(clientTimeoutCoroutine);

        clientTimeoutCoroutine = null;
        waitingForClients.Clear();
    }

    private bool TryGenerateServerLayout()
    {
        if (generator == null || survivorManager == null)
        {
            Debug.LogError("Network dungeon session is missing its generator or survivor manager.", this);
            return false;
        }

        survivorManager.InitializeSurvivors();
        List<string> questIds = survivorManager.GetAssignedQuestIds();
        List<string> invalidQuestIds = questIds
            .Where(string.IsNullOrWhiteSpace)
            .ToList();
        if (invalidQuestIds.Count > 0)
        {
            Debug.LogError("Every networked quest needs a non-empty Quest ID.", this);
            return false;
        }

        List<GameObject> mandatoryRooms = survivorManager.ResolveRequiredRoomPrefabs(
            generator,
            out List<string> missingRoomNames);
        if (missingRoomNames.Count > 0)
        {
            Debug.LogError($"Quest rooms are not configured: {string.Join(", ", missingRoomNames)}", this);
            return false;
        }

        generator.SetMandatoryRooms(mandatoryRooms);
        int generatedSeed = generator.CreateSessionSeed();
        if (!generator.GenerateDungeon(generatedSeed, buildNavigation: true))
        {
            Debug.LogError($"Server dungeon generation failed for seed {generatedSeed}.", this);
            return false;
        }

        sessionSeed = generatedSeed;
        assignedQuestIds = string.Join(QuestIdSeparator.ToString(), questIds);
        serverLayoutHash = CalculateLayoutHash(generator.LastResult);
        sessionRevision = sessionRevision == uint.MaxValue ? 1u : sessionRevision + 1u;

        Debug.Log(
            $"Server prepared dungeon revision {sessionRevision} with seed {sessionSeed} " +
            $"and layout hash {serverLayoutHash:X8}.",
            this);
        return true;
    }

    private void OnSessionRevisionChanged(uint _, uint newRevision)
    {
        if (!isClient || isServer || newRevision == 0) return;
        QueueClientGeneration(newRevision);
    }

    private void QueueClientGeneration(uint revision)
    {
        if (revision <= appliedClientRevision || revision == queuedClientRevision) return;

        queuedClientRevision = revision;
        if (clientGenerationCoroutine != null)
            StopCoroutine(clientGenerationCoroutine);
        clientGenerationCoroutine = StartCoroutine(GenerateClientLayoutNextFrame(revision));
    }

    private IEnumerator GenerateClientLayoutNextFrame(uint revision)
    {
        yield return null;
        clientGenerationCoroutine = null;

        if (revision != sessionRevision || revision <= appliedClientRevision) yield break;
        appliedClientRevision = revision;

        string[] questIds = string.IsNullOrEmpty(assignedQuestIds)
            ? Array.Empty<string>()
            : assignedQuestIds.Split(QuestIdSeparator);
        if (!survivorManager.AssignQuestsById(questIds, out List<string> missingQuestIds))
        {
            ReportClientGeneration(
                revision,
                false,
                0,
                $"Missing quests: {string.Join(", ", missingQuestIds)}");
            yield break;
        }

        List<GameObject> mandatoryRooms = survivorManager.ResolveRequiredRoomPrefabs(
            generator,
            out List<string> missingRoomNames);
        if (missingRoomNames.Count > 0)
        {
            ReportClientGeneration(
                revision,
                false,
                0,
                $"Missing quest rooms: {string.Join(", ", missingRoomNames)}");
            yield break;
        }

        generator.SetMandatoryRooms(mandatoryRooms);
        if (!generator.GenerateDungeon(sessionSeed, buildNavigation: false))
        {
            ReportClientGeneration(revision, false, 0, "Client generation failed.");
            yield break;
        }

        uint clientLayoutHash = CalculateLayoutHash(generator.LastResult);
        bool matchesServer = clientLayoutHash == serverLayoutHash;
        string error = matchesServer
            ? string.Empty
            : $"Layout hash {clientLayoutHash:X8} does not match server {serverLayoutHash:X8}.";
        ReportClientGeneration(revision, matchesServer, clientLayoutHash, error);
    }

    private void ReportClientGeneration(uint revision, bool success, uint layoutHash, string error)
    {
        if (!success)
            Debug.LogError($"Network dungeon generation failed: {error}", this);

        CmdReportGeneration(revision, success, layoutHash, error);
    }

    [Command(requiresAuthority = false)]
    private void CmdReportGeneration(
        uint revision,
        bool success,
        uint layoutHash,
        string error,
        NetworkConnectionToClient sender = null)
    {
        if (sender == null || revision != sessionRevision) return;

        if (!success || layoutHash != serverLayoutHash)
        {
            string reason = string.IsNullOrWhiteSpace(error)
                ? $"Layout hash {layoutHash:X8} does not match server {serverLayoutHash:X8}."
                : error;
            Debug.LogError(
                $"Client {sender.connectionId} could not reproduce the dungeon: {reason}",
                this);
            waitingForClients.Remove(sender.connectionId);
            sender.Disconnect();
            TryFinishWaitingForClients();
            return;
        }

        waitingForClients.Remove(sender.connectionId);
        Debug.Log($"Client {sender.connectionId} confirmed dungeon {layoutHash:X8}.", this);
        TryFinishWaitingForClients();
    }

    [Server]
    private void RememberConnectedClients()
    {
        waitingForClients.Clear();
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null || connection == NetworkServer.localConnection) continue;
            waitingForClients.Add(connection.connectionId);
        }
    }

    [Server]
    private IEnumerator WaitForClientGeneration()
    {
        float deadline = Time.unscaledTime + clientGenerationTimeout;
        while (waitingForClients.Count > 0 && Time.unscaledTime < deadline)
        {
            waitingForClients.RemoveWhere(id => !NetworkServer.connections.ContainsKey(id));
            yield return null;
        }

        if (waitingForClients.Count > 0)
        {
            foreach (int connectionId in waitingForClients.ToList())
            {
                if (!NetworkServer.connections.TryGetValue(
                        connectionId,
                        out NetworkConnectionToClient connection))
                    continue;

                Debug.LogError(
                    $"Client {connectionId} did not finish dungeon generation in time and was disconnected.",
                    this);
                connection.Disconnect();
            }

            waitingForClients.Clear();
        }

        clientTimeoutCoroutine = null;
        SpawnGameplayContent();
    }

    [Server]
    private void TryFinishWaitingForClients()
    {
        if (waitingForClients.Count > 0 || contentSpawned) return;

        if (clientTimeoutCoroutine != null)
        {
            StopCoroutine(clientTimeoutCoroutine);
            clientTimeoutCoroutine = null;
        }

        SpawnGameplayContent();
    }

    [Server]
    private void SpawnGameplayContent()
    {
        if (contentSpawned) return;
        contentSpawned = true;
        if (!spawnGameplayContent) return;

        DungeonGenerationResult result = generator.LastResult;
        int survivorCount = survivorManager.GetAssignedQuests().Count;
        if (result == null) return;

        SpawnEvacuationZone(result);
        if (survivorCount == 0)
        {
            Debug.LogError("No quests were selected, so no survivors can be spawned.", this);
            return;
        }

        List<Vector3> positions = survivorManager.ResolveSurvivorPositions(result);
        if (positions.Count < survivorCount)
        {
            Debug.LogError(
                $"Only {positions.Count} survivor positions are available for {survivorCount} quests.",
                this);
            return;
        }

        survivorManager.SpawnSurvivorsAtPositions(positions);
        SpawnEnemies(result);
        gameManager.StartMatch(survivorCount);
    }

    [Server]
    private void SpawnEvacuationZone(DungeonGenerationResult result)
    {
        if (evacuationZonePrefab == null || result.EvacuationRoom == null)
        {
            Debug.LogError("Evacuation zone prefab or evacuation room is missing.", this);
            return;
        }

        Transform room = result.EvacuationRoom.transform;
        GameObject zone = Instantiate(
            evacuationZonePrefab,
            room.position + Vector3.up,
            room.rotation);
        NetworkServer.Spawn(zone);
    }

    [Server]
    private void SpawnEnemies(DungeonGenerationResult result)
    {
        if (enemyCount <= 0) return;
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab is not assigned.", this);
            return;
        }

        List<Transform> spawnPoints = result.EnemySpawnPoints
            .Where(point => point != null)
            .ToList();
        if (spawnPoints.Count == 0)
        {
            spawnPoints = result.Rooms
                .Where(room => room != null && room != result.StartRoom)
                .Select(room => room.transform)
                .ToList();
        }

        if (spawnPoints.Count == 0) return;

        int count = Mathf.Min(enemyCount, spawnPoints.Count);
        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            GameObject enemy = Instantiate(
                enemyPrefab,
                spawnPoint.position + Vector3.up,
                spawnPoint.rotation);
            NetworkServer.Spawn(enemy);
        }
    }

    [Server]
    private static void RegisterPlayerSpawnPoints(DungeonGenerationResult result)
    {
        if (result == null) return;

        IEnumerable<Transform> spawnPoints = result.PlayerSpawnPoints.Count > 0
            ? result.PlayerSpawnPoints
            : new[] { result.StartRoom != null ? result.StartRoom.transform : null };
        foreach (Transform spawnPoint in spawnPoints.Where(point => point != null))
        {
            if (spawnPoint.GetComponent<NetworkStartPosition>() == null)
                spawnPoint.gameObject.AddComponent<NetworkStartPosition>();
        }
    }

    private static uint CalculateLayoutHash(DungeonGenerationResult result)
    {
        const uint offset = 2166136261;
        uint hash = offset;
        if (result == null) return hash;

        AddInt(ref hash, result.Seed);
        foreach (RoomData module in result.Modules)
        {
            if (module == null) continue;

            AddString(ref hash, module.name);
            AddInt(ref hash, (int)module.ModuleKind);
            AddInt(ref hash, (int)module.RuntimeRole);
            AddVector(ref hash, module.transform.position);
            AddVector(ref hash, module.transform.eulerAngles);
        }

        return hash;
    }

    private static void AddString(ref uint hash, string value)
    {
        foreach (char character in value ?? string.Empty)
        {
            hash ^= character;
            hash *= 16777619;
        }

        hash ^= 0xFF;
        hash *= 16777619;
    }

    private static void AddVector(ref uint hash, Vector3 value)
    {
        AddInt(ref hash, Mathf.RoundToInt(value.x * 1000f));
        AddInt(ref hash, Mathf.RoundToInt(value.y * 1000f));
        AddInt(ref hash, Mathf.RoundToInt(value.z * 1000f));
    }

    private static void AddInt(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (byte)value;
            hash *= 16777619;
            hash ^= (byte)(value >> 8);
            hash *= 16777619;
            hash ^= (byte)(value >> 16);
            hash *= 16777619;
            hash ^= (byte)(value >> 24);
            hash *= 16777619;
        }
    }
}
