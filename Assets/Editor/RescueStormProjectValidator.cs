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

    private static readonly string[] CorridorPrefabPaths =
    {
        "Assets/Prefabs/ROOMx2_1.prefab",
        "Assets/Prefabs/ROOMx2L_1.prefab",
        "Assets/Prefabs/ROOMx3T_1.prefab",
        "Assets/Prefabs/ROOMx3T_2.prefab"
    };

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

        if (enabledScenes.Length < 2 ||
            !string.Equals(enabledScenes[0], MainMenuScenePath, StringComparison.Ordinal) ||
            !enabledScenes.Contains(GameplayScenePath, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Build scenes must start with '{MainMenuScenePath}' and include '{GameplayScenePath}'.");
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
            playerPrefab.GetComponent<LeakRepairController>() == null ||
            player.StartingRepairToolPrefab != repairToolPrefab)
        {
            throw new InvalidOperationException(
                "Player prefab must contain LeakRepairController and reference the repair tool.");
        }

        int markerCount = 0;
        foreach (string path in CorridorPrefabPaths)
        {
            GameObject corridor = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RoomData room = corridor != null ? corridor.GetComponent<RoomData>() : null;
            if (room == null || room.ModuleKind != RoomModuleKind.Corridor)
                throw new InvalidOperationException($"'{path}' is not a configured corridor prefab.");

            markerCount += room.GetLeakSpawnPoints().Count;
        }

        if (markerCount == 0)
            throw new InvalidOperationException("No corridor leak spawn points are configured.");
    }

    private static void ValidateMainMenu()
    {
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        NetworkManager networkManager =
            UnityEngine.Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
        if (networkManager == null)
            throw new InvalidOperationException("MainMenu has no NetworkManager.");

        if (!string.Equals(networkManager.offlineScene, MainMenuScenePath, StringComparison.Ordinal) ||
            !string.Equals(networkManager.onlineScene, GameplayScenePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MainMenu NetworkManager must route offline to MainMenu and online to SampleScene.");
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
