using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

[CustomEditor(typeof(DungeonGenerator))]
public sealed class DungeonGeneratorEditor : Editor
{
    private bool showAdvancedSettings;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptReference();
        DrawContentSettings();
        DrawLevelSizeSettings();
        DrawMapShapeSettings();
        DrawNavigationSettings();
        DrawAdvancedSettings();

        serializedObject.ApplyModifiedProperties();

        DungeonGenerator generator = (DungeonGenerator)target;
        IReadOnlyList<GameObject> legacyChildren = generator.FindLegacyGeneratedChildren();

        DrawLegacyMigration(generator, legacyChildren);
        DrawGenerationButtons(generator, legacyChildren.Count > 0);
        DrawValidationButton(generator);
        DrawGenerationResult(generator.LastResult);
    }

    private void DrawScriptReference()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }
    }

    private void DrawContentSettings()
    {
        DrawSectionTitle("Content");
        DrawProperty(
            "roomPrefabs",
            "Random Module Pool",
            "Rooms and corridors that may be picked during normal generation.",
            includeChildren: true);
        DrawProperty(
            "startRoomPrefab",
            "Starting Room",
            "Optional fixed starting room. If empty, the first suitable room from the random pool is used.");
        DrawProperty(
            "mandatoryRoomPrefabs",
            "Always Required Rooms",
            "Rooms that must appear in every generated level, regardless of active quests.",
            includeChildren: true);
        DrawProperty(
            "unusedDoorBlockerPrefab",
            "Unused Door Blocker",
            "Prefab placed over a doorway that was not connected to another module.");
        DrawProperty(
            "closeUnusedSockets",
            "Block Unused Doorways",
            "Close every unconnected doorway with the blocker prefab above.");

        SerializedProperty randomModules = serializedObject.FindProperty("roomPrefabs");
        if (randomModules.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "Add at least one room or corridor to the Random Module Pool.",
                MessageType.Error);
        }

        SerializedProperty closeDoors = serializedObject.FindProperty("closeUnusedSockets");
        SerializedProperty blocker = serializedObject.FindProperty("unusedDoorBlockerPrefab");
        if (closeDoors.boolValue && blocker.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Block Unused Doorways is enabled, but no blocker prefab is assigned.",
                MessageType.Warning);
        }
    }

    private void DrawLevelSizeSettings()
    {
        DrawSectionTitle("Level Size");
        DrawProperty(
            "targetRoomCount",
            "Gameplay Rooms",
            "Exact number of gameplay rooms. Corridor modules are not included in this count.");
        DrawProperty(
            "maxCorridorCount",
            "Corridor Limit",
            "Maximum number of corridor modules the generator may add between rooms.");
        DrawProperty(
            "seed",
            "Generation Seed",
            "Use 0 for a new random layout on every run. Use a fixed value to reproduce a layout.");
        DrawProperty(
            "generateOnStart",
            "Generate On Play",
            "Generate the level automatically when this scene starts.");

        EditorGUILayout.HelpBox(
            "Gameplay Rooms controls playable spaces; Corridor Limit only controls connecting modules.",
            MessageType.Info);
    }

    private void DrawMapShapeSettings()
    {
        DrawSectionTitle("Map Shape");
        DrawProperty(
            "desiredLoopCount",
            "Target Loops",
            "Preferred number of alternate routes. More loops give players more ways to escape or flank enemies.");
        DrawProperty(
            "maxConsecutiveCorridors",
            "Corridors In A Row",
            "Maximum corridor chain length before the generator must place a room.");
        DrawProperty(
            "preferredLayoutRadius",
            "Preferred Map Radius",
            "Soft target for how far the level spreads from its center. This is not a hard boundary.");
        DrawProperty(
            "branchDepthWeight",
            "Branching",
            "Higher values encourage the generator to extend deeper branches.");
        DrawProperty(
            "compactnessWeight",
            "Compactness",
            "Higher values keep modules closer to the current center of the map.");
        DrawProperty(
            "loopClosureBonus",
            "Loop Priority",
            "Higher values prefer placements that connect back into the existing layout.");
    }

    private void DrawNavigationSettings()
    {
        DrawSectionTitle("Navigation");
        DrawProperty(
            "buildNavMeshAfterGeneration",
            "Build NavMesh",
            "Bake navigation after generation so enemies can move through the level.");
        DrawProperty(
            "showNavMeshBreakMarkers",
            "Show Broken Connections",
            "Draw Scene view markers at doorways where the baked NavMesh is disconnected.");
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.Space();
        showAdvancedSettings = EditorGUILayout.Foldout(
            showAdvancedSettings,
            "Advanced Generation Limits",
            toggleOnLabelClick: true);
        if (!showAdvancedSettings) return;

        using (new EditorGUI.IndentLevelScope())
        {
            DrawProperty(
                "questRoomPrefabs",
                "Legacy Quest Room Lookup",
                "Compatibility list for old quests that store a room name. New quests should reference their Required Room prefab directly.",
                includeChildren: true);
            DrawProperty(
                "generationAttempts",
                "Retry Count",
                "How many complete layouts may be attempted before generation fails.");
            DrawProperty(
                "maxSearchSteps",
                "Search Step Limit",
                "Maximum placement decisions allowed in one attempt. Increase only for large or restrictive room sets.");
            DrawProperty(
                "overlapTolerance",
                "Overlap Tolerance",
                "Small amount removed from module bounds when checking intersections.");
            DrawProperty(
                "socketPositionTolerance",
                "Door Position Tolerance",
                "Maximum distance between two doorway markers for an automatic connection.");
            DrawProperty(
                "socketAngleTolerance",
                "Door Angle Tolerance",
                "Maximum angle error, in degrees, between two facing doorway markers.");
            DrawProperty(
                "floorHeightTolerance",
                "Floor Height Tolerance",
                "Maximum vertical difference allowed between connected modules.");
        }
    }

    private void DrawProperty(
        string propertyName,
        string label,
        string tooltip,
        bool includeChildren = false)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
    }

    private static void DrawSectionTitle(string title)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void DrawLegacyMigration(
        DungeonGenerator generator,
        IReadOnlyList<GameObject> legacyChildren)
    {
        if (legacyChildren.Count == 0) return;

        EditorGUILayout.HelpBox(
            $"Found {legacyChildren.Count} legacy generated children. Adopt them explicitly before generating a new layout.",
            MessageType.Warning);

        if (!GUILayout.Button($"Adopt {legacyChildren.Count} Legacy Generated Objects")) return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Adopt legacy generated objects?",
            "Matching direct children will be moved under the explicit generated-level root. They can then be cleared safely without relying on object names. This operation supports Undo.",
            "Adopt",
            "Cancel");
        if (!confirmed) return;

        AdoptLegacyChildrenWithUndo(generator, legacyChildren);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
    }

    private static void DrawGenerationButtons(DungeonGenerator generator, bool hasLegacyChildren)
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(hasLegacyChildren))
            {
                if (GUILayout.Button("Generate"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate dungeon");
                    generator.GenerateDungeon();
                    EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                }
            }

            if (GUILayout.Button("Clear"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear dungeon");
                generator.ClearDungeon();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
    }

    private static void DrawValidationButton(DungeonGenerator generator)
    {
        if (!GUILayout.Button("Validate Room Set")) return;

        List<string> errors = generator.ValidateConfiguration();
        if (errors.Count == 0)
            Debug.Log("Dungeon room set is valid.", generator);
        else
            Debug.LogError(string.Join(Environment.NewLine, errors), generator);
    }

    private static void DrawGenerationResult(DungeonGenerationResult result)
    {
        if (result == null) return;

        string evacuationRoomName = result.EvacuationRoom != null
            ? result.EvacuationRoom.name
            : "not assigned";
        int corridorCount = result.Modules.Count - result.Rooms.Count;
        EditorGUILayout.HelpBox(
            $"Generated {result.Rooms.Count} rooms and {corridorCount} corridors " +
            $"with {result.LoopCount} loops with seed {result.Seed}.\n" +
            $"Evacuation: {evacuationRoomName}",
            MessageType.Info);
    }

    internal static void AdoptLegacyChildrenWithUndo(
        DungeonGenerator generator,
        IReadOnlyList<GameObject> legacyChildren)
    {
        Undo.SetCurrentGroupName("Adopt legacy generated dungeon objects");
        int undoGroup = Undo.GetCurrentGroup();
        Transform generatedRoot = generator.transform.Find(DungeonGenerator.GeneratedRootName);
        if (generatedRoot == null)
        {
            GameObject root = new GameObject(DungeonGenerator.GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create generated dungeon root");
            Undo.SetTransformParent(root.transform, generator.transform, "Parent generated dungeon root");
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            generatedRoot = root.transform;
        }

        foreach (GameObject child in legacyChildren.Where(child => child != null))
        {
            Undo.SetTransformParent(child.transform, generatedRoot, "Adopt legacy generated object");
        }

        Undo.CollapseUndoOperations(undoGroup);
    }
}

[CustomEditor(typeof(RoomData))]
public sealed class RoomDataEditor : Editor
{
    private bool showAdvancedSettings;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
        SerializedProperty moduleKind = serializedObject.FindProperty("moduleKind");
        EditorGUILayout.PropertyField(
            moduleKind,
            new GUIContent(
                "Module Type",
                "Room counts toward Gameplay Rooms. Corridor only connects rooms and counts toward Corridor Limit."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("selectionWeight"),
            new GUIContent(
                "Selection Weight",
                "Relative chance that this prefab is selected from the random module pool."));

        using (new EditorGUI.DisabledScope(moduleKind.enumValueIndex == (int)RoomModuleKind.Corridor))
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("canBeEvacuationRoom"),
                new GUIContent(
                    "Can Be Evacuation Room",
                    "Allow this room to become the extraction destination. Corridors cannot be evacuation rooms."));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gameplay Spawn Points", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("survivorSpawnPoint"),
            new GUIContent(
                "Survivor Spawn",
                "Preferred position for a survivor assigned to this room. The room center is used when empty."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("miniGameSpawnPoint"),
            new GUIContent(
                "Mini Game Spawn",
                "Position for a quest mini-game in this room."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("itemSpawnPoints"),
            new GUIContent("Item Spawns", "Possible positions for items in this room."),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("playerSpawnPoints"),
            new GUIContent("Player Spawns", "Possible player starting positions in this room."),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("ventSpawnPoints"),
            new GUIContent("Vent Spawns", "Possible ventilation points the monster travels and spawns from."),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("leakSpawnPoints"),
            new GUIContent("Leak Spawns", "Pipe markers eligible for timed leak events."),
            includeChildren: true);

        EditorGUILayout.Space();
        showAdvancedSettings = EditorGUILayout.Foldout(
            showAdvancedSettings,
            "Advanced Module Setup",
            toggleOnLabelClick: true);
        if (showAdvancedSettings)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("occupancyVolume"),
                    new GUIContent(
                        "Placement Bounds",
                        "Optional BoxCollider used for overlap checks. Renderer bounds are used when empty."));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("doors"),
                    new GUIContent(
                        "Doorway Cache",
                        "Cached DoorPoint components. Use Refresh Doorways after changing the prefab hierarchy."),
                    includeChildren: true);
            }
        }

        serializedObject.ApplyModifiedProperties();

        RoomData room = (RoomData)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Doorways"))
        {
            Undo.RecordObject(room, "Refresh room doorways");
            room.RefreshDoorPoints();
            EditorUtility.SetDirty(room);
            serializedObject.Update();
        }
        if (GUILayout.Button("Refresh Leak Spawns"))
        {
            Undo.RecordObject(room, "Refresh leak spawn points");
            room.RefreshLeakSpawnPoints();
            EditorUtility.SetDirty(room);
            serializedObject.Update();
        }


        IReadOnlyList<DoorPoint> sockets = room.GetDoorPoints();
        if (sockets.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No DoorPoint components found. This module cannot connect to the generated level.",
                MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Doorways: {sockets.Count} · Placement size: {room.GetWorldBounds().size}",
                MessageType.None);
        }

        if (room.GetComponentsInChildren<Collider>(true).Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No colliders found. The module can be placed, but it cannot block the player or contribute to NavMesh generation.",
                MessageType.Warning);
        }
    }
}

[CustomEditor(typeof(QuestSO), true)]
public sealed class QuestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quest", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("questId"),
            new GUIContent("Quest ID", "Stable identifier used by quest logic and save data."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("questName"),
            new GUIContent("Display Name", "Name shown to players and designers."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generated Content", EditorStyles.boldLabel);
        SerializedProperty requiredRoom = serializedObject.FindProperty("requiredRoomPrefab");
        EditorGUILayout.PropertyField(
            requiredRoom,
            new GUIContent(
                "Required Room",
                "Room prefab that must be included whenever this quest is selected."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("itemPrefab"),
            new GUIContent("Quest Item", "Item prefab spawned in the required room for this quest."));

        if (requiredRoom.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "No Required Room is assigned. The survivor may be placed in any available room.",
                MessageType.Info);
        }

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "questId",
            "questName",
            "requiredRoomPrefab",
            "requiredRoom",
            "requiredItem",
            "itemPrefab");

        serializedObject.ApplyModifiedProperties();
    }
}

public static class DungeonGeneratorBatchValidation
{
    private const string TestScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Run()
    {
        RescueStormProjectValidator.Validate();
        EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        DungeonGenerator generator = UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>();
        if (generator == null)
            throw new InvalidOperationException($"No DungeonGenerator found in {TestScenePath}.");
        ValidateSessionCoordinator(generator);

        IReadOnlyList<GameObject> legacyChildren = generator.FindLegacyGeneratedChildren();
        if (legacyChildren.Count > 0)
        {
            DungeonGeneratorEditor.AdoptLegacyChildrenWithUndo(generator, legacyChildren);
        }

        List<string> configurationErrors = generator.ValidateConfiguration();
        if (configurationErrors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, configurationErrors));

        int originalSeed = generator.Seed;
        int originalTargetCount = generator.TargetRoomCount;
        int[] roomCounts = { 2, 5, 10, 20, 30 };
        int[] smokeSeeds = { 101, 202, 303 };
        int generatedLayouts = 0;

        foreach (int targetCount in roomCounts)
        {
            generator.TargetRoomCount = targetCount;
            foreach (int smokeSeed in smokeSeeds)
            {
                generator.Seed = smokeSeed;
                if (!generator.GenerateDungeon(smokeSeed, buildNavigation: false))
                    throw new InvalidOperationException($"Generation failed for {targetCount} rooms and seed {smokeSeed}.");
                if (generator.LastResult == null || generator.LastResult.Rooms.Count != targetCount)
                    throw new InvalidOperationException($"Seed {smokeSeed} produced an incorrect room count.");
                if (generator.LastResult.StartRoom == null || generator.LastResult.EvacuationRoom == null)
                    throw new InvalidOperationException($"Seed {smokeSeed} has no start or evacuation room.");
                if ((generator.LastResult.EvacuationRoom.RuntimeRole & RoomRuntimeRole.Evacuation) == 0)
                    throw new InvalidOperationException($"Seed {smokeSeed} did not retain the evacuation role flag.");

                ValidateGeneratedConnections(generator.LastResult, smokeSeed);
                generatedLayouts++;
            }
        }

        ValidateCombinedMandatoryAndEvacuationRoles();
        ValidateDeterministicLayout(generator);
        ValidateGeneratedLeakPoints(generator);
        ValidateNavigationWithoutDoorLinks(generator);
        ValidateQuestRoomAndSurvivor(generator);

        generator.Seed = originalSeed;
        generator.TargetRoomCount = originalTargetCount;
        generator.ClearDungeon();
        Debug.Log($"Dungeon smoke test passed for {generatedLayouts} layouts from 2 to 30 rooms.");
    }

    private static void ValidateSessionCoordinator(DungeonGenerator generator)
    {
        bool hasLocalCoordinator =
            generator.GetComponent<DungeonLocalSessionCoordinator>() is { enabled: true };
        bool hasNetworkCoordinator =
            generator.GetComponent<DungeonNetworkSessionCoordinator>() != null;
        if (!hasLocalCoordinator && !hasNetworkCoordinator)
        {
            throw new InvalidOperationException(
                "The dungeon test scene requires a local or network session coordinator.");
        }
    }

    private static void ValidateCombinedMandatoryAndEvacuationRoles()
    {
        RoomRuntimeRole combined = RoomRuntimeRole.Mandatory | RoomRuntimeRole.Evacuation;
        if ((combined & RoomRuntimeRole.Mandatory) == 0 ||
            (combined & RoomRuntimeRole.Evacuation) == 0)
            throw new InvalidOperationException("Room runtime roles cannot retain combined flags.");
    }

    private static void ValidateDeterministicLayout(DungeonGenerator generator)
    {
        generator.TargetRoomCount = 20;
        const int deterministicSeed = 505;
        if (!generator.GenerateDungeon(deterministicSeed, buildNavigation: false))
            throw new InvalidOperationException("Could not generate the first deterministic layout.");
        string firstSignature = CreateLayoutSignature(generator.LastResult);

        if (!generator.GenerateDungeon(deterministicSeed, buildNavigation: false))
            throw new InvalidOperationException("Could not generate the second deterministic layout.");
        string secondSignature = CreateLayoutSignature(generator.LastResult);

        if (!string.Equals(firstSignature, secondSignature, StringComparison.Ordinal))
            throw new InvalidOperationException("The same synchronized seed produced different layouts.");
    }

    private static void ValidateGeneratedLeakPoints(DungeonGenerator generator)
    {
        generator.TargetRoomCount = 20;
        int[] seeds = { 909, 1010, 1111 };

        foreach (int seed in seeds)
        {
            if (!generator.GenerateDungeon(seed, buildNavigation: false))
                throw new InvalidOperationException(
                    $"Could not generate leak marker validation layout for seed {seed}.");

            List<Transform> points = generator.LastResult.Modules
                .Where(module => module != null && module.ModuleKind == RoomModuleKind.Corridor)
                .SelectMany(GetLeakSourceTransforms)
                .Where(point => point != null)
                .ToList();
            if (points.Count == 0) continue;

            if (points.Any(point =>
                    point.GetComponentInParent<RoomData>()?.ModuleKind !=
                    RoomModuleKind.Corridor))
            {
                throw new InvalidOperationException(
                    "A generated leak spawn point is not owned by a corridor.");
            }

            return;
        }

        throw new InvalidOperationException(
            "Generated layouts did not preserve any corridor leak or vent points.");
    }

    private static IEnumerable<Transform> GetLeakSourceTransforms(RoomData module)
    {
        IReadOnlyList<LeakSpawnPoint> dedicatedPoints = module.GetLeakSpawnPoints();
        return dedicatedPoints.Count > 0
            ? dedicatedPoints.Where(point => point != null).Select(point => point.transform)
            : module.VentSpawnPoints.Where(point => point != null);
    }

    private static void ValidateNavigationWithoutDoorLinks(DungeonGenerator generator)
    {
        NavMeshSurface surface = generator.GetComponent<NavMeshSurface>();
        bool addedSurface = surface == null;
        if (addedSurface) surface = generator.gameObject.AddComponent<NavMeshSurface>();

        SerializedObject serializedGenerator = new SerializedObject(generator);
        SerializedProperty surfaceProperty =
            serializedGenerator.FindProperty("navMeshSurface");
        surfaceProperty.objectReferenceValue = surface;
        serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            generator.TargetRoomCount = 10;
            const int navigationSeed = 909;
            if (!generator.GenerateDungeon(navigationSeed, buildNavigation: true))
                throw new InvalidOperationException(
                    "Could not generate the navigation validation layout.");

            Transform generatedRoot =
                generator.transform.Find(DungeonGenerator.GeneratedRootName);
            if (generatedRoot == null)
                throw new InvalidOperationException("Generated navigation root is missing.");

            NavMeshLink[] links =
                generatedRoot.GetComponentsInChildren<NavMeshLink>(true);
            if (links.Length > 0)
                throw new InvalidOperationException(
                    $"Generated dungeon still contains {links.Length} NavMeshLink transitions.");

            DungeonGenerationResult result = generator.LastResult;
            if (!TrySampleModule(result.StartRoom, out Vector3 start))
                throw new InvalidOperationException(
                    "Could not sample NavMesh in the generated start room.");

            foreach (RoomData module in result.Modules)
            {
                if (!TrySampleModule(module, out Vector3 target))
                    throw new InvalidOperationException(
                        $"Generated module '{module.name}' has no NavMesh sample.");

                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(start, target, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    throw new InvalidOperationException(
                        $"AI has no complete NavMesh path to generated module '{module.name}' without door links.");
                }
            }
        }
        finally
        {
            generator.ClearDungeon();
            surface.RemoveData();
            if (addedSurface) UnityEngine.Object.DestroyImmediate(surface);

            serializedGenerator = new SerializedObject(generator);
            surfaceProperty = serializedGenerator.FindProperty("navMeshSurface");
            surfaceProperty.objectReferenceValue = null;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool TrySampleModule(RoomData module, out Vector3 position)
    {
        if (module != null)
        {
            foreach (DoorPoint socket in module.GetDoorPoints())
            {
                if (socket == null || !socket.IsConnected) continue;

                Vector3 probe = socket.transform.position -
                                socket.GetWorldDirection(module.transform) * 0.65f;
                probe.y = module.GetWorldBounds().min.y + 0.1f;
                if (NavMesh.SamplePosition(
                        probe,
                        out NavMeshHit doorHit,
                        1.5f,
                        NavMesh.AllAreas))
                {
                    position = doorHit.position;
                    return true;
                }
            }

            Bounds bounds = module.GetWorldBounds();
            Vector3 center = new Vector3(
                bounds.center.x,
                bounds.min.y + 0.1f,
                bounds.center.z);
            if (NavMesh.SamplePosition(
                    center,
                    out NavMeshHit centerHit,
                    2f,
                    NavMesh.AllAreas))
            {
                position = centerHit.position;
                return true;
            }
        }

        position = default;
        return false;
    }

    private static void ValidateQuestRoomAndSurvivor(DungeonGenerator generator)
    {
        SurvivorManager survivorManager = generator.GetComponent<SurvivorManager>();
        if (survivorManager == null)
            throw new InvalidOperationException("DGTESTER has no SurvivorManager for quest-room validation.");

        survivorManager.InitializeSurvivors();
        List<QuestSO> quests = survivorManager.GetAssignedQuests();
        if (quests.Count == 0)
            throw new InvalidOperationException("DGTESTER has no survivor quests configured.");

        List<GameObject> mandatoryRooms = survivorManager.ResolveRequiredRoomPrefabs(
            generator,
            out List<string> missingRoomNames);
        if (missingRoomNames.Count > 0)
            throw new InvalidOperationException($"Missing quest rooms: {string.Join(", ", missingRoomNames)}");

        generator.SetMandatoryRooms(mandatoryRooms);
        generator.TargetRoomCount = 10;
        const int questSeed = 808;
        if (!generator.GenerateDungeon(questSeed, buildNavigation: false))
            throw new InvalidOperationException("Could not generate the survivor quest test layout.");

        foreach (QuestSO quest in quests.Where(quest =>
                     !string.IsNullOrWhiteSpace(quest.RequiredRoomName)))
        {
            string requiredRoomName = quest.RequiredRoomName;
            RoomData questRoom = generator.LastResult.Rooms.FirstOrDefault(room =>
                string.Equals(room.name, requiredRoomName, StringComparison.OrdinalIgnoreCase) &&
                (room.RuntimeRole & RoomRuntimeRole.Mandatory) != 0);
            if (questRoom == null)
            {
                throw new InvalidOperationException(
                    $"Quest room '{requiredRoomName}' was not generated as mandatory.");
            }
        }

        List<Vector3> positions = survivorManager.ResolveSurvivorPositions(generator.LastResult);
        if (positions.Count != quests.Count)
            throw new InvalidOperationException("Could not resolve one survivor position per assigned quest.");

        int survivorCountBeforeSpawn = survivorManager.GetSurvivors().Count;
        HashSet<ItemObject> existingItems = Resources.FindObjectsOfTypeAll<ItemObject>()
            .Where(item => item != null && !EditorUtility.IsPersistent(item))
            .ToHashSet();

        survivorManager.SpawnSurvivorsLocallyAtPositions(positions);
        List<Survivor> spawnedSurvivors = survivorManager.GetSurvivors()
            .Skip(survivorCountBeforeSpawn)
            .Where(survivor => survivor != null)
            .ToList();
        if (spawnedSurvivors.Count != quests.Count)
            throw new InvalidOperationException("The configured survivor prefab was not spawned for every quest.");

        float spawnHeight = new SerializedObject(survivorManager)
            .FindProperty("survivorSpawnHeight").floatValue;
        Vector3 expectedPosition = positions[0] + Vector3.up * spawnHeight;
        if (Vector3.Distance(spawnedSurvivors[0].transform.position, expectedPosition) > 0.01f)
            throw new InvalidOperationException("The survivor was not placed at its quest-room spawn point.");

        List<ItemObject> spawnedItems = Resources.FindObjectsOfTypeAll<ItemObject>()
            .Where(item => item != null && !EditorUtility.IsPersistent(item) && !existingItems.Contains(item))
            .ToList();
        foreach (string requiredItemName in quests
                     .Select(quest => quest.RequiredItemName)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!spawnedItems.Any(item => string.Equals(
                    item.Name,
                    requiredItemName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Quest item '{requiredItemName}' was not spawned.");
            }
        }

        foreach (Survivor survivor in spawnedSurvivors)
            UnityEngine.Object.DestroyImmediate(survivor.gameObject);
        foreach (ItemObject item in spawnedItems)
            if (item != null) UnityEngine.Object.DestroyImmediate(item.gameObject);

        generator.SetMandatoryRooms(null);
    }

    private static string CreateLayoutSignature(DungeonGenerationResult result)
    {
        return string.Join(
            "|",
            result.Modules.Select(module =>
                $"{module.name}:{module.transform.position.x:R},{module.transform.position.y:R},{module.transform.position.z:R}:" +
                $"{module.transform.rotation.x:R},{module.transform.rotation.y:R},{module.transform.rotation.z:R},{module.transform.rotation.w:R}"));
    }

    private static void ValidateGeneratedConnections(DungeonGenerationResult result, int seed)
    {
        HashSet<RoomData> visited = new HashSet<RoomData>();
        Queue<RoomData> queue = new Queue<RoomData>();
        queue.Enqueue(result.StartRoom);
        visited.Add(result.StartRoom);

        while (queue.Count > 0)
        {
            RoomData module = queue.Dequeue();
            foreach (DoorPoint socket in module.GetDoorPoints())
            {
                if (!socket.IsConnected || socket.ConnectedTo == null) continue;
                RoomData neighbour = socket.ConnectedTo.GetComponentInParent<RoomData>();
                if (neighbour != null && visited.Add(neighbour)) queue.Enqueue(neighbour);
            }
        }

        if (visited.Count != result.Modules.Count)
            throw new InvalidOperationException($"Seed {seed} contains unreachable modules: {visited.Count}/{result.Modules.Count} reachable.");

        List<RoomData> modules = result.Modules.ToList();
        for (int i = 0; i < modules.Count; i++)
        {
            Bounds first = Shrink(modules[i].GetWorldBounds(), 0.08f);
            for (int j = i + 1; j < modules.Count; j++)
            {
                Bounds second = Shrink(modules[j].GetWorldBounds(), 0.08f);
                if (first.Intersects(second))
                    throw new InvalidOperationException($"Seed {seed} contains overlapping modules '{modules[i].name}' and '{modules[j].name}'.");
            }
        }
    }

    private static Bounds Shrink(Bounds bounds, float amount)
    {
        Vector3 size = bounds.size;
        size.x = Mathf.Max(0.01f, size.x - amount * 2f);
        size.y = Mathf.Max(0.01f, size.y - amount * 2f);
        size.z = Mathf.Max(0.01f, size.z - amount * 2f);
        bounds.size = size;
        return bounds;
    }
}

public static class DungeonGeneratorLegacyMigration
{
    private const string TestScenePath = "Assets/Scenes/DGTESTER.unity";

    public static void Run()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        DungeonGenerator generator = UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>();
        if (generator == null)
            throw new InvalidOperationException($"No DungeonGenerator found in {TestScenePath}.");

        IReadOnlyList<GameObject> legacyChildren = generator.FindLegacyGeneratedChildren();
        if (legacyChildren.Count == 0)
        {
            Debug.Log("No legacy generated dungeon objects require migration.");
            return;
        }

        DungeonGeneratorEditor.AdoptLegacyChildrenWithUndo(generator, legacyChildren);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Could not save migrated scene {TestScenePath}.");

        Debug.Log($"Adopted {legacyChildren.Count} legacy generated objects under {DungeonGenerator.GeneratedRootName}.");
    }
}
