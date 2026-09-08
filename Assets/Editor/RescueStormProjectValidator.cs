using System;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class RescueStormProjectValidator
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
    private const string EvacuationPrefabPath = "Assets/TESTS/Evac.prefab";
    private const string EnemyPrefabPath = "Assets/TESTS/Zombie Idle.prefab";
    private const string PlayerPrefabPath = "Assets/TESTS/Player.prefab";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("Tools/Rescue Storm/Validate Project Configuration")]
    public static void Validate()
    {
        ValidateBuildScenes();
        ValidateLeakSystemAssets();
        ValidateMainMenu();
        ValidateGameplayScene();
        ValidateLeakMath();
        Debug.Log("Rescue Storm project configuration is valid.");
    }

    private static void ValidateBuildScenes()
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (!enabledScenes.Contains(GameplayScenePath, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Build scenes must include enabled gameplay scene '{GameplayScenePath}'.");
        }
    }

    private static void ValidateLeakSystemAssets()
    {
        GameObject leakPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(LeakSystemSetup.LeakPrefabPath);
        GameObject repairToolPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(LeakSystemSetup.RepairToolPrefabPath);
        GameObject playerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

        if (leakPrefab == null ||
            leakPrefab.GetComponent<NetworkIdentity>() == null ||
            leakPrefab.GetComponent<LeakHazard>() == null)
        {
            throw new InvalidOperationException(
                "Leak prefab must contain NetworkIdentity and LeakHazard.");
        }

        if (repairToolPrefab == null ||
            repairToolPrefab.GetComponent<NetworkIdentity>() == null ||
            repairToolPrefab.GetComponent<RepairToolItem>() == null)
        {
            throw new InvalidOperationException(
                "Repair tool prefab must contain NetworkIdentity and RepairToolItem.");
        }

        Player player = playerPrefab != null ? playerPrefab.GetComponent<Player>() : null;
        if (player == null ||
            player.StartingRepairToolPrefab != repairToolPrefab)
        {
            throw new InvalidOperationException(
                "Player prefab must reference the repair tool.");
        }

    }

    private static void ValidateMainMenu()
    {
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        NetworkManager networkManager =
            UnityEngine.Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
        if (networkManager == null)
            throw new InvalidOperationException("MainMenu has no NetworkManager.");

        if (!string.IsNullOrWhiteSpace(networkManager.offlineScene) ||
            !string.Equals(networkManager.onlineScene, GameplayScenePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MainMenu NetworkManager must keep offlineScene empty and route online to SampleScene.");
        }

        RequireSpawnPrefab(networkManager, EvacuationPrefabPath);
        RequireSpawnPrefab(networkManager, EnemyPrefabPath);
        RequireSpawnPrefab(networkManager, LeakSystemSetup.LeakPrefabPath);
        RequireSpawnPrefab(networkManager, LeakSystemSetup.RepairToolPrefabPath);

        SettingsManager settings =
            UnityEngine.Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (settings == null)
            throw new InvalidOperationException("MainMenu has no SettingsManager.");

        SerializedProperty inputActions =
            new SerializedObject(settings).FindProperty("inputActions");
        InputActionAsset expectedActions =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (expectedActions == null || inputActions?.objectReferenceValue != expectedActions)
            throw new InvalidOperationException("SettingsManager has no gameplay InputActionAsset.");
        if (expectedActions.FindAction("Player/Slot5") == null ||
            expectedActions.FindAction("Player/Attack") == null)
        {
            throw new InvalidOperationException(
                "Gameplay InputActionAsset needs Slot5 and Attack actions.");
        }
    }

    private static void ValidateGameplayScene()
    {
        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>(
                FindObjectsInactive.Include);
        if (generator == null)
            throw new InvalidOperationException("SampleScene has no DungeonGenerator.");

        ValidateConfiguredLeakSources(generator);

        DungeonNetworkSessionCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<DungeonNetworkSessionCoordinator>(
                FindObjectsInactive.Include);
        if (coordinator == null)
            throw new InvalidOperationException("SampleScene has no network dungeon coordinator.");

        SerializedObject serializedCoordinator = new SerializedObject(coordinator);
        RequireReference(serializedCoordinator, "generator");
        RequireReference(serializedCoordinator, "survivorManager");
        RequireReference(serializedCoordinator, "gameManager");
        RequireReference(serializedCoordinator, "leakEventManager");
        RequireReference(serializedCoordinator, "evacuationZonePrefab");
        RequireReference(serializedCoordinator, "enemyPrefab");

        LeakEventManager eventManager =
            UnityEngine.Object.FindFirstObjectByType<LeakEventManager>(
                FindObjectsInactive.Include);
        if (eventManager == null)
            throw new InvalidOperationException("SampleScene has no LeakEventManager.");

        SerializedObject serializedEventManager = new SerializedObject(eventManager);
        RequireReference(serializedEventManager, "gameManager");
        RequireReference(serializedEventManager, "leakPrefab");

        bool hasActiveLegacyEvacuation =
            UnityEngine.Object.FindObjectsByType<EvacuateZone>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Any(zone => zone.gameObject.activeInHierarchy);
        if (hasActiveLegacyEvacuation)
        {
            throw new InvalidOperationException(
                "SampleScene contains an active fixed evacuation zone; the session coordinator owns its spawn.");
        }
    }

    private static void ValidateConfiguredLeakSources(DungeonGenerator generator)
    {
        SerializedProperty roomPrefabs =
            new SerializedObject(generator).FindProperty("roomPrefabs");
        if (roomPrefabs == null || !roomPrefabs.isArray)
            throw new InvalidOperationException("DungeonGenerator has no configured room pool.");

        int corridorCount = 0;
        int spawnSourceCount = 0;
        for (int i = 0; i < roomPrefabs.arraySize; i++)
        {
            GameObject prefab = roomPrefabs.GetArrayElementAtIndex(i).objectReferenceValue
                as GameObject;
            RoomData room = prefab != null ? prefab.GetComponent<RoomData>() : null;
            if (room == null || room.ModuleKind != RoomModuleKind.Corridor) continue;

            corridorCount++;
            int dedicatedPointCount = room.GetLeakSpawnPoints().Count;
            spawnSourceCount += dedicatedPointCount > 0
                ? dedicatedPointCount
                : room.VentSpawnPoints.Count(point => point != null);
        }

        if (corridorCount == 0)
            throw new InvalidOperationException("The configured room pool has no corridor prefabs.");
        if (spawnSourceCount == 0)
        {
            throw new InvalidOperationException(
                "Configured corridor prefabs need a leak point or vent spawn point.");
        }
    }

    private static void ValidateLeakMath()
    {
        float[] expectedMultipliers = { 1f, 1.5f, 2f, 2.5f };
        for (int leakCount = 0; leakCount < expectedMultipliers.Length; leakCount++)
        {
            float actual = GameManager.CalculateTimeDrainMultiplier(leakCount);
            if (!Mathf.Approximately(actual, expectedMultipliers[leakCount]))
            {
                throw new InvalidOperationException(
                    $"Leak count {leakCount} produced timer multiplier {actual}.");
            }
        }

        if (!Mathf.Approximately(
                LeakHazard.CalculateRepairProgressDelta(1, 1f, 4f),
                0.25f) ||
            !Mathf.Approximately(
                LeakHazard.CalculateRepairProgressDelta(2, 1f, 4f),
                0.5f))
        {
            throw new InvalidOperationException(
                "Leak repair progress does not scale linearly with repairer count.");
        }

        System.Random random = new System.Random(12345);
        for (int i = 0; i < 100; i++)
        {
            float interval = LeakEventManager.NextIntervalSeconds(random, 90f, 180f);
            if (interval < 90f || interval > 180f)
                throw new InvalidOperationException(
                    $"Leak interval {interval} is outside 90-180 seconds.");
        }
        if (LeakEventManager.CanMutateSessionState(
                serverActive: false,
                localSession: false))
        {
            throw new InvalidOperationException(
                "A disconnected remote client must not receive leak-session authority.");
        }
        if (!LeakEventManager.CanMutateSessionState(true, false) ||
            !LeakEventManager.CanMutateSessionState(false, true))
        {
            throw new InvalidOperationException("Server and explicit local sessions need authority.");
        }
    }

    private static void RequireReference(SerializedObject owner, string propertyName)
    {
        SerializedProperty property = owner.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            throw new InvalidOperationException(
                $"{owner.targetObject.name} has no required '{propertyName}' reference.");
    }

    private static void RequireSpawnPrefab(NetworkManager manager, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null || !manager.spawnPrefabs.Contains(prefab))
            throw new InvalidOperationException(
                $"NetworkManager does not register required spawn prefab '{prefabPath}'.");
    }
}
