using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(DungeonGenerator), typeof(SurvivorManager), typeof(GameManager))]
public sealed class DungeonLocalSessionCoordinator : MonoBehaviour
{
    private const string TestSceneName = "DGTESTER";

    [SerializeField] private DungeonGenerator generator;
    [SerializeField] private SurvivorManager survivorManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LeakEventManager leakEventManager;
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject evacuationZonePrefab;
    [SerializeField] private bool spawnGameplayContent = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapTestScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != TestSceneName) return;

        DungeonGenerator sceneGenerator = FindGeneratorInScene(activeScene);
        if (sceneGenerator == null)
        {
            Debug.LogError("DGTESTER local bootstrap could not find DungeonGenerator.");
            return;
        }

        if (!sceneGenerator.gameObject.activeSelf)
            sceneGenerator.gameObject.SetActive(true);
        sceneGenerator.enabled = true;

        DungeonLocalSessionCoordinator coordinator =
            sceneGenerator.GetComponent<DungeonLocalSessionCoordinator>();
        if (coordinator == null)
            coordinator = sceneGenerator.gameObject.AddComponent<DungeonLocalSessionCoordinator>();

        coordinator.enabled = true;
        coordinator.ResolveReferences();
        Debug.Log("DGTESTER local bootstrap is ready.", coordinator);
    }

    private static DungeonGenerator FindGeneratorInScene(Scene scene)
    {
        // FindFirstObjectByType skips inactive objects while a scene is loading.
        return Resources.FindObjectsOfTypeAll<DungeonGenerator>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.gameObject.scene == scene);
    }

    private void Awake()
    {
        ResolveReferences();
        if (generator == null)
        {
            Debug.LogError("Local dungeon session has no DungeonGenerator.", this);
            enabled = false;
            return;
        }

        generator.SuppressAutomaticGeneration();
    }

    private void ResolveReferences()
    {
        if (generator == null) generator = GetComponent<DungeonGenerator>();
        if (survivorManager == null) survivorManager = GetComponent<SurvivorManager>();
        if (gameManager == null) gameManager = GetComponent<GameManager>();

        if (leakEventManager == null) leakEventManager = GetComponent<LeakEventManager>();
        // The inactive NetworkManager is used only as a prefab reference in local mode.
        // It is never started and no networking code runs.
        if (localPlayerPrefab == null)
        {
            NetworkManager networkManager =
                FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (networkManager != null) localPlayerPrefab = networkManager.playerPrefab;
        }
    }

    private void Start()
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.Log("Mirror is active; the network dungeon coordinator owns this session.", this);
            return;
        }

        Debug.Log("Starting local dungeon session.", this);
        gameManager?.EnableLocalSession();

        if (survivorManager == null)
        {
            Debug.LogError("Local dungeon session has no SurvivorManager.", this);
            return;
        }

        survivorManager.InitializeSurvivors();
        List<GameObject> mandatoryRooms = survivorManager.ResolveRequiredRoomPrefabs(
            generator,
            out List<string> missingRoomNames);

        if (missingRoomNames.Count > 0)
        {
            Debug.LogError($"Quest rooms are not configured: {string.Join(", ", missingRoomNames)}", this);
            return;
        }

        generator.SetMandatoryRooms(mandatoryRooms);
        int sessionSeed = generator.CreateSessionSeed();
        if (!generator.GenerateDungeon(sessionSeed, buildNavigation: true))
        {
            Debug.LogError($"Local dungeon generation failed for seed {sessionSeed}.", this);
            return;
        }

        DungeonGenerationResult result = generator.LastResult;
        SpawnLocalPlayer(result);
        SpawnEvacuationZone(result);
        if (spawnGameplayContent) SpawnGameplayContent(result);
    }

    private void SpawnLocalPlayer(DungeonGenerationResult result)
    {
        if (localPlayerPrefab == null)
        {
            Debug.LogError("Local player prefab is not assigned.", this);
            return;
        }

        Transform configuredSpawn = result.PlayerSpawnPoints.FirstOrDefault(point => point != null);
        Vector3 position = configuredSpawn != null
            ? configuredSpawn.position
            : result.StartRoom.transform.position + Vector3.up;
        Quaternion rotation = configuredSpawn != null
            ? configuredSpawn.rotation
            : result.StartRoom.transform.rotation;

        GameObject player = Instantiate(localPlayerPrefab, position, rotation);
        player.name = "LocalPlayer";
    }

    private void SpawnGameplayContent(DungeonGenerationResult result)
    {
        int survivorCount = survivorManager.GetAssignedQuests().Count;
        if (survivorCount == 0) return;

        List<Vector3> positions = survivorManager.ResolveSurvivorPositions(result);

        if (positions.Count < survivorCount)
        {
            Debug.LogError($"Only {positions.Count} survivor positions are available for {survivorCount} quests.", this);
            return;
        }

        SpawnAiCoordinator();
        survivorManager.SpawnSurvivorsLocallyAtPositions(positions);
        gameManager?.StartMatch(survivorCount);
        leakEventManager?.BeginSession(result);
    }

    private void SpawnAiCoordinator()
    {
        GameObject intel = new GameObject("AiCoordinator");
        intel.AddComponent<AiCoordinator>();
    }

    private void SpawnEvacuationZone(DungeonGenerationResult result)
    {
        if (evacuationZonePrefab == null || result?.EvacuationRoom == null) return;

        Transform room = result.EvacuationRoom.transform;
        Instantiate(evacuationZonePrefab, room.position + Vector3.up, room.rotation);
    }
}
