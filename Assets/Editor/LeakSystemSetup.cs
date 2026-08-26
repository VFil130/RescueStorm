using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LeakSystemSetup
{
    public const string LeakPrefabPath = "Assets/Prefabs/Gameplay/LeakHazard.prefab";
    public const string RepairToolPrefabPath = "Assets/Prefabs/Gameplay/RepairTool.prefab";

    private const string GameplayFolder = "Assets/Prefabs/Gameplay";
    private const string LeakMaterialPath = GameplayFolder + "/LeakIndicator.mat";
    private const string SteamMaterialPath = GameplayFolder + "/LeakSteam.mat";
    private const string ToolMaterialPath = GameplayFolder + "/RepairTool.mat";
    private const string PlayerPrefabPath = "Assets/TESTS/Player.prefab";
    private const string EvacuationPrefabPath = "Assets/TESTS/Evac.prefab";
    private const string EnemyPrefabPath = "Assets/TESTS/Zombie Idle.prefab";
    private const string CupPrefabPath = "Assets/TESTS/Cup.prefab";

    private static readonly string[] CorridorPrefabPaths =
    {
        "Assets/Prefabs/ROOMx2_1.prefab",
        "Assets/Prefabs/ROOMx2L_1.prefab",
        "Assets/Prefabs/ROOMx3T_1.prefab",
        "Assets/Prefabs/ROOMx3T_2.prefab"
    };

    [MenuItem("Tools/Rescue Storm/Build Leak System Assets")]
    public static void Build()
    {
        EnsureFolder(GameplayFolder);

        Material indicatorMaterial = GetOrCreateMaterial(
            LeakMaterialPath,
            new Color(1f, 0.12f, 0.02f),
            emission: true,
            particle: false);
        Material steamMaterial = GetOrCreateMaterial(
            SteamMaterialPath,
            new Color(0.75f, 0.9f, 1f, 0.65f),
            emission: false,
            particle: true);
        Material toolMaterial = GetOrCreateMaterial(
            ToolMaterialPath,
            new Color(1f, 0.7f, 0.05f),
            emission: false,
            particle: false);

        GameObject leakPrefab = CreateLeakPrefab(indicatorMaterial, steamMaterial);
        GameObject repairToolPrefab = CreateRepairToolPrefab(toolMaterial);

        int markerCount = ConfigureCorridorLeakPoints();
        if (markerCount == 0)
            throw new InvalidOperationException("No pipe transforms were found in corridor prefabs.");

        ConfigurePlayerPrefab(repairToolPrefab);
        ConfigureScene("Assets/Scenes/MainMenu.unity", leakPrefab, repairToolPrefab, false);
        ConfigureScene("Assets/Scenes/SampleScene.unity", leakPrefab, repairToolPrefab, true);
        ConfigureScene("Assets/Scenes/DGTESTER.unity", leakPrefab, repairToolPrefab, true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Leak system assets built with {markerCount} corridor spawn markers.");
    }

    private static GameObject CreateLeakPrefab(
        Material indicatorMaterial,
        Material steamMaterial)
    {
        GameObject root = new GameObject("LeakHazard");
        root.AddComponent<NetworkIdentity>();

        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.radius = 0.5f;
        collider.isTrigger = true;

        LeakHazard hazard = root.AddComponent<LeakHazard>();
        AudioSource audioSource = root.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 18f;

        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = "WorldIndicator";
        indicator.transform.SetParent(root.transform, false);
        indicator.transform.localPosition = Vector3.up * 0.8f;
        indicator.transform.localScale = Vector3.one * 0.24f;
        UnityEngine.Object.DestroyImmediate(indicator.GetComponent<Collider>());
        indicator.GetComponent<Renderer>().sharedMaterial = indicatorMaterial;

        GameObject particleObject = new GameObject("Steam");
        particleObject.transform.SetParent(root.transform, false);
        particleObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 1.4f;
        main.startSize = 0.24f;
        main.startColor = new Color(0.8f, 0.92f, 1f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 24f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.08f;

        ParticleSystemRenderer particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = steamMaterial;

        SerializedObject serializedHazard = new SerializedObject(hazard);
        serializedHazard.FindProperty("repairDuration").floatValue = 4f;
        serializedHazard.FindProperty("repairRange").floatValue = 2.5f;
        serializedHazard.FindProperty("leakParticles").objectReferenceValue = particles;
        serializedHazard.FindProperty("worldIndicator").objectReferenceValue = indicator;
        serializedHazard.FindProperty("leakAudioSource").objectReferenceValue = audioSource;
        SerializedProperty syncInterval = serializedHazard.FindProperty("syncInterval");
        if (syncInterval != null) syncInterval.floatValue = 0.1f;
        serializedHazard.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LeakPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateRepairToolPrefab(Material material)
    {
        GameObject root = new GameObject("RepairTool");
        root.AddComponent<NetworkIdentity>();
        NetworkTransformUnreliable networkTransform =
            root.AddComponent<NetworkTransformUnreliable>();
        RepairToolItem item = root.AddComponent<RepairToolItem>();

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.28f, 0.18f, 0.65f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.22f, 0.12f, 0.58f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = material;

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle";
        handle.transform.SetParent(root.transform, false);
        handle.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        handle.transform.localScale = new Vector3(0.07f, 0.22f, 0.07f);
        UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());
        handle.GetComponent<Renderer>().sharedMaterial = material;

        SerializedObject serializedItem = new SerializedObject(item);
        serializedItem.FindProperty("charges").intValue = 1;
        serializedItem.FindProperty("itemName").stringValue = "RepairTool";
        serializedItem.FindProperty("isHeavy").boolValue = false;
        serializedItem.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedTransform = new SerializedObject(networkTransform);
        SerializedProperty target = serializedTransform.FindProperty("target");
        if (target != null) target.objectReferenceValue = root.transform;
        serializedTransform.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RepairToolPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static int ConfigureCorridorLeakPoints()
    {
        int totalMarkers = 0;
        foreach (string path in CorridorPrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RoomData room = root.GetComponent<RoomData>();
                if (room == null || room.ModuleKind != RoomModuleKind.Corridor)
                    continue;

                foreach (LeakSpawnPoint existing in
                         root.GetComponentsInChildren<LeakSpawnPoint>(true))
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                List<Transform> pipeTransforms = root
                    .GetComponentsInChildren<Transform>(true)
                    .Where(transform =>
                        transform != root.transform &&
                        transform.name.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Where(transform =>
                        transform.parent == null ||
                        transform.parent.name.IndexOf(
                            "pipe",
                            StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();

                foreach (Transform pipe in pipeTransforms)
                {
                    GameObject marker = new GameObject("__LeakSpawnPoint");
                    marker.transform.SetParent(pipe, false);
                    marker.transform.localPosition = Vector3.up * 0.15f;
                    marker.transform.localRotation = Quaternion.identity;
                    marker.AddComponent<LeakSpawnPoint>();
                    totalMarkers++;
                }

                room.RefreshLeakSpawnPoints();
                EditorUtility.SetDirty(room);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return totalMarkers;
    }

    private static void ConfigurePlayerPrefab(GameObject repairToolPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            if (player == null)
                throw new InvalidOperationException("Player prefab has no Player component.");

            if (root.GetComponent<LeakRepairController>() == null)
                root.AddComponent<LeakRepairController>();

            SerializedObject serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("startingRepairToolPrefab").objectReferenceValue =
                repairToolPrefab;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureScene(
        string scenePath,
        GameObject leakPrefab,
        GameObject repairToolPrefab,
        bool configureEventManager)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject evacuationPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(EvacuationPrefabPath);
        GameObject enemyPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        GameObject cupPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(CupPrefabPath);

        foreach (NetworkManager networkManager in
                 UnityEngine.Object.FindObjectsByType<NetworkManager>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            AddSpawnPrefab(networkManager, leakPrefab);
            AddSpawnPrefab(networkManager, repairToolPrefab);
            EditorUtility.SetDirty(networkManager);
            AddSpawnPrefab(networkManager, evacuationPrefab);
            AddSpawnPrefab(networkManager, enemyPrefab);
            AddSpawnPrefab(networkManager, cupPrefab);
        }

        if (configureEventManager)
        {
            DungeonGenerator generator =
                UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>(
                    FindObjectsInactive.Include);
            if (generator == null)
                throw new InvalidOperationException($"{scenePath} has no DungeonGenerator.");

            LeakEventManager eventManager =
                generator.GetComponent<LeakEventManager>() ??
                generator.gameObject.AddComponent<LeakEventManager>();

            SerializedObject serializedManager = new SerializedObject(eventManager);
            serializedManager.FindProperty("gameManager").objectReferenceValue =
                generator.GetComponent<GameManager>();
            serializedManager.FindProperty("leakPrefab").objectReferenceValue = leakPrefab;
            serializedManager.FindProperty("minimumInterval").floatValue = 90f;
            serializedManager.FindProperty("maximumInterval").floatValue = 180f;
            serializedManager.FindProperty("maximumActiveLeaks").intValue = 3;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            DungeonNetworkSessionCoordinator networkCoordinator =
                generator.GetComponent<DungeonNetworkSessionCoordinator>();
            if (networkCoordinator != null)
            {
                SerializedObject serializedCoordinator =
                    new SerializedObject(networkCoordinator);
                serializedCoordinator.FindProperty("leakEventManager").objectReferenceValue =
                    eventManager;
                serializedCoordinator.FindProperty("gameManager").objectReferenceValue =
                    generator.GetComponent<GameManager>();
                serializedCoordinator.FindProperty("evacuationZonePrefab").objectReferenceValue =
                    evacuationPrefab;
                serializedCoordinator.FindProperty("enemyPrefab").objectReferenceValue =
                    enemyPrefab;
                serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
            }

            DungeonLocalSessionCoordinator localCoordinator =
                generator.GetComponent<DungeonLocalSessionCoordinator>();
            if (localCoordinator != null)
            {
                SerializedObject serializedCoordinator =
                    new SerializedObject(localCoordinator);
                serializedCoordinator.FindProperty("leakEventManager").objectReferenceValue =
                    eventManager;
                serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
            }

            GameManager gameManager = generator.GetComponent<GameManager>();
            if (gameManager != null)
            {
                SerializedObject serializedGameManager =
                    new SerializedObject(gameManager);
                serializedGameManager.FindProperty("matchDuration").floatValue = 300f;
                serializedGameManager.FindProperty("leakTimeDrainPerLeak").floatValue = 0.5f;
                serializedGameManager.FindProperty("maximumTimeDrainMultiplier").floatValue = 2.5f;
                serializedGameManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (string.Equals(
                scenePath,
                "Assets/Scenes/SampleScene.unity",
                StringComparison.Ordinal))
        {
            foreach (EvacuateZone zone in UnityEngine.Object.FindObjectsByType<EvacuateZone>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                zone.gameObject.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Could not save {scenePath}.");
    }

    private static void AddSpawnPrefab(NetworkManager manager, GameObject prefab)
    {
        if (manager == null || prefab == null || manager.spawnPrefabs.Contains(prefab)) return;
        manager.spawnPrefabs.Add(prefab);
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color color,
        bool emission,
        bool particle)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(
            particle
                ? "Universal Render Pipeline/Particles/Unlit"
                : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.color = color;
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
