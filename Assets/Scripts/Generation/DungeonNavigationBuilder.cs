using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

internal sealed class DungeonNavigationSettings
{
    public float DoorInset { get; set; }
    public float DoorLinkWidth { get; set; }
    public float SampleRadius { get; set; }
    public float DoorLaneWidth { get; set; }
    public float MinimumRegionArea { get; set; }
    public int TileSize { get; set; }
}

internal sealed class DungeonNavigationBuilder
{
    private const float NavigationFloorOffset = 0.02f;
    private const float NavigationLaneHeight = 0.1f;
    private const float MinimumDoorSampleRadius = 0.75f;
    private const float MinimumDoorProbeSpacing = 0.5f;
    private const float DoorSampleSideTolerance = 0.05f;
    private const float MaximumWalkableElevation = 0.8f;
    private const float ElevatedSurfaceVolumePadding = 0.5f;
    private const int DoorProbeCount = 5;

    private readonly DungeonGenerator owner;
    private readonly Transform generatedRoot;
    private readonly DungeonLayout layout;
    private readonly DungeonNavigationSettings settings;
    private readonly List<NavMeshBreakMarker> breakMarkers = new List<NavMeshBreakMarker>();
    private readonly List<NavMeshLinkInstance> doorLinks =
        new List<NavMeshLinkInstance>();

    public NavMeshSurface Surface { get; private set; }
    public int BridgeCount { get; private set; }
    public int LinkCount => doorLinks.Count;

    public DungeonNavigationBuilder(
        DungeonGenerator owner,
        Transform generatedRoot,
        DungeonLayout layout,
        DungeonNavigationSettings settings,
        NavMeshSurface surface)
    {
        this.owner = owner;
        this.generatedRoot = generatedRoot;
        this.layout = layout;
        this.settings = settings;
        Surface = surface;
    }

    public bool TryBuild()
    {
        BridgeCount = 0;
        RemoveDoorLinks();
        EnsureSurface();
        if (Surface == null)
        {
            Debug.LogWarning(
                "NavMeshSurface is not assigned. Add one to bake navigation from the editor generator.",
                owner);
            return false;
        }

        ReplaceInvalidNavMeshColliders();
        CreateElevatedSurfaceExclusionVolumes();
        BridgeCount = CreateDoorwayNavigationGeometry();

        Surface.collectObjects = CollectObjects.Children;
        Surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        Surface.minRegionArea = settings.MinimumRegionArea;
        Surface.overrideTileSize = true;
        Surface.tileSize = settings.TileSize;
        Surface.BuildNavMesh();
        CreateDoorNavMeshLinks();

        return true;
    }

    public void LogConnectivity()
    {
        List<string> unreachableModules = FindUnreachableModules();
        RefreshBreakMarkers();
        if (unreachableModules.Count > 0)
        {
            Debug.LogError(
                $"NavMesh is disconnected in {unreachableModules.Count} generated modules " +
                $"after baking {BridgeCount} doorway bridges and registering " +
                $"{LinkCount} runtime connections: {string.Join(", ", unreachableModules)}" +
                $". Scene view markers: {breakMarkers.Count} broken doorway connections " +
                "(red = no complete path, magenta = no NavMesh sample).",
                owner);
            return;
        }

        Debug.Log(
            $"NavMesh connectivity verified for {layout.Modules.Count} modules using " +
            $"{BridgeCount} baked doorway bridges and {LinkCount} runtime connections " +
            "without generated transition GameObjects.",
            owner);
    }

    public void DrawGizmos(bool showMarkers, float markerSize)
    {
        if (!showMarkers || breakMarkers.Count == 0) return;

        foreach (NavMeshBreakMarker marker in breakMarkers)
        {
            Gizmos.color = marker.MissingSample ? Color.magenta : Color.red;
            Gizmos.DrawSphere(marker.Start, markerSize);
            Gizmos.DrawSphere(marker.End, markerSize);
            Gizmos.DrawLine(marker.Start, marker.End);

            Vector3 midpoint = (marker.Start + marker.End) * 0.5f;
            Gizmos.DrawWireCube(midpoint, Vector3.one * markerSize * 1.5f);
            Gizmos.DrawLine(midpoint, midpoint + Vector3.up * markerSize * 4f);
        }
    }

    private void EnsureSurface()
    {
        if (Surface == null)
            Surface = owner.GetComponent<NavMeshSurface>();
        if (Surface == null && Application.isPlaying)
            Surface = owner.gameObject.AddComponent<NavMeshSurface>();
    }

    private void RefreshBreakMarkers()
    {
        breakMarkers.Clear();
        foreach (DungeonDoorwayConnection connection in layout.EnumerateDoorways())
        {
            bool hasStart = TrySampleInsideDoor(connection.First, out Vector3 start);
            bool hasEnd = TrySampleInsideDoor(connection.Second, out Vector3 end);
            if (!hasStart || !hasEnd)
            {
                breakMarkers.Add(new NavMeshBreakMarker(
                    hasStart ? start : connection.First.transform.position,
                    hasEnd ? end : connection.Second.transform.position,
                    true));
                continue;
            }

            NavMeshPath path = new NavMeshPath();
            if (!CalculateNavMeshPath(start, end, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                breakMarkers.Add(new NavMeshBreakMarker(start, end, false));
            }
        }
    }

    private void CreateDoorNavMeshLinks()
    {
        foreach (DungeonDoorwayConnection connection in layout.EnumerateDoorways())
        {
            if (!TrySampleInsideDoor(connection.First, out Vector3 start) ||
                !TrySampleInsideDoor(connection.Second, out Vector3 end))
            {
                Debug.LogWarning(
                    "Could not anchor a runtime NavMesh connection at door '" +
                    connection.First.name + "'.",
                    connection.First);
                continue;
            }

            NavMeshLinkData linkData = new NavMeshLinkData
            {
                startPosition = start,
                endPosition = end,
                width = settings.DoorLinkWidth,
                costModifier = -1f,
                bidirectional = true,
                area = 0,
                agentTypeID = Surface.agentTypeID
            };

            NavMeshLinkInstance link = NavMesh.AddLink(linkData);
            if (NavMesh.IsLinkValid(link))
                doorLinks.Add(link);
            else
                Debug.LogWarning(
                    "Could not register a runtime NavMesh connection at door '" +
                    connection.First.name + "'.",
                    connection.First);
        }
    }

    public void RemoveDoorLinks()
    {
        foreach (NavMeshLinkInstance link in doorLinks)
        {
            if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }

        doorLinks.Clear();
    }

    private int CreateDoorwayNavigationGeometry()
    {
        int laneIndex = 0;
        foreach (DungeonPlacedModule module in layout.Modules)
        {
            List<DoorPoint> connectedSockets = module.Data.GetDoorPoints()
                .Where(socket => socket != null && socket.IsConnected)
                .ToList();
            if (connectedSockets.Count == 0) continue;

            // Door markers in imported rooms are not consistently placed at floor height.
            float floorY = GetNavigationFloorY(module);
            Vector3 hub = CalculateDoorLaneHub(module, connectedSockets, floorY);
            CreateNavigationLane(hub, hub, floorY, $"__NavHub_{laneIndex:00}");

            foreach (DoorPoint socket in connectedSockets)
            {
                Vector3 doorway = socket.transform.position;
                doorway.y = floorY;
                CreateNavigationLane(doorway, hub, floorY, $"__NavLane_{laneIndex:00}");
                laneIndex++;
            }
        }

        int bridgeCount = 0;
        foreach (DungeonDoorwayConnection connection in layout.EnumerateDoorways())
        {
            RoomData connectedRoom = connection.Second.GetComponentInParent<RoomData>();
            if (connectedRoom == null) continue;

            Vector3 start = connection.First.transform.position -
                            connection.First.GetWorldDirection(connection.Module.Data.transform) * settings.DoorInset;
            Vector3 end = connection.Second.transform.position -
                          connection.Second.GetWorldDirection(connectedRoom.transform) * settings.DoorInset;
            float floorY = (GetNavigationFloorY(connection.Module) + GetNavigationFloorY(connectedRoom)) * 0.5f;
            start.y = floorY;
            end.y = floorY;

            CreateNavigationLane(start, end, floorY, $"__NavDoorBridge_{bridgeCount:00}");
            bridgeCount++;
        }

        return bridgeCount;
    }

    private void CreateElevatedSurfaceExclusionVolumes()
    {
        int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkableArea < 0)
            notWalkableArea = 1;

        for (int moduleIndex = 0; moduleIndex < layout.Modules.Count; moduleIndex++)
        {
            DungeonPlacedModule module = layout.Modules[moduleIndex];
            Bounds bounds = module.Bounds;
            float bottom = GetNavigationFloorY(module) + MaximumWalkableElevation;
            float height = bounds.max.y - bottom + ElevatedSurfaceVolumePadding;
            if (height <= 0f) continue;

            GameObject volumeObject = new GameObject($"__NavCeilingMask_{moduleIndex:00}");
            volumeObject.transform.SetParent(generatedRoot, true);
            volumeObject.transform.SetPositionAndRotation(
                new Vector3(
                    bounds.center.x,
                    bottom + height * 0.5f,
                    bounds.center.z),
                Quaternion.identity);

            NavMeshModifierVolume volume = volumeObject.AddComponent<NavMeshModifierVolume>();
            volume.area = notWalkableArea;
            volume.center = Vector3.zero;
            volume.size = new Vector3(
                bounds.size.x + ElevatedSurfaceVolumePadding * 2f,
                height,
                bounds.size.z + ElevatedSurfaceVolumePadding * 2f);
        }
    }

    private static float GetNavigationFloorY(DungeonPlacedModule module)
    {
        return GetNavigationFloorY(module.Bounds);
    }

    private float GetNavigationFloorY(RoomData room)
    {
        DungeonPlacedModule placed = layout.Modules.FirstOrDefault(candidate => candidate.Data == room);
        Bounds bounds = placed != null ? placed.Bounds : room.GetWorldBounds();
        return GetNavigationFloorY(bounds);
    }

    private static float GetNavigationFloorY(Bounds bounds)
    {
        return bounds.min.y + NavigationFloorOffset;
    }

    private static Vector3 CalculateDoorLaneHub(
        DungeonPlacedModule module,
        IEnumerable<DoorPoint> sockets,
        float floorY)
    {
        List<float> constrainedX = new List<float>();
        List<float> constrainedZ = new List<float>();

        foreach (DoorPoint socket in sockets)
        {
            Vector3 direction = socket.GetWorldDirection(module.Data.transform);
            if (Mathf.Abs(direction.z) >= Mathf.Abs(direction.x))
                constrainedX.Add(socket.transform.position.x);
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.z))
                constrainedZ.Add(socket.transform.position.z);
        }

        return new Vector3(
            constrainedX.Count > 0 ? constrainedX.Average() : module.Bounds.center.x,
            floorY,
            constrainedZ.Count > 0 ? constrainedZ.Average() : module.Bounds.center.z);
    }

    private void CreateNavigationLane(Vector3 start, Vector3 end, float floorY, string objectName)
    {
        Vector3 delta = end - start;
        delta.y = 0f;
        float length = delta.magnitude;

        GameObject laneObject = new GameObject(objectName);
        laneObject.transform.SetParent(generatedRoot, true);
        laneObject.transform.SetPositionAndRotation(
            new Vector3(
                (start.x + end.x) * 0.5f,
                floorY - NavigationLaneHeight * 0.5f,
                (start.z + end.z) * 0.5f),
            length > 0.001f ? Quaternion.LookRotation(delta, Vector3.up) : Quaternion.identity);

        BoxCollider lane = laneObject.AddComponent<BoxCollider>();
        lane.size = new Vector3(
            settings.DoorLaneWidth,
            NavigationLaneHeight,
            Mathf.Max(settings.DoorLaneWidth, length + settings.DoorLaneWidth));
    }

    private bool TrySampleInsideDoor(DoorPoint socket, out Vector3 position)
    {
        position = default;
        if (socket == null) return false;

        RoomData room = socket.GetComponentInParent<RoomData>();
        if (room == null) return false;

        Vector3 outward = socket.GetWorldDirection(room.transform);
        Vector3 doorway = socket.transform.position;
        doorway.y = GetNavigationFloorY(room) + 0.1f;

        float sampleRadius = Mathf.Max(
            settings.SampleRadius,
            MinimumDoorSampleRadius,
            settings.DoorLaneWidth * 0.75f);
        Vector3 firstProbe = doorway - outward * settings.DoorInset;
        if (TrySampleOnRoomSide(firstProbe, doorway, outward, sampleRadius, out position))
            return true;

        DungeonPlacedModule module = layout.Modules.FirstOrDefault(candidate => candidate.Data == room);
        if (module == null) return false;

        List<DoorPoint> connectedSockets = room.GetDoorPoints()
            .Where(candidate => candidate != null && candidate.IsConnected)
            .ToList();
        Vector3 hub = CalculateDoorLaneHub(module, connectedSockets, doorway.y);
        Vector3 towardHub = hub - doorway;
        towardHub.y = 0f;
        float distanceToHub = towardHub.magnitude;
        if (distanceToHub <= 0.001f) return false;

        Vector3 directionToHub = towardHub / distanceToHub;
        float probeSpacing = Mathf.Max(
            MinimumDoorProbeSpacing,
            settings.SampleRadius,
            settings.DoorLaneWidth * 0.5f);

        for (int probeIndex = 1; probeIndex <= DoorProbeCount; probeIndex++)
        {
            float distance = Mathf.Min(
                settings.DoorInset + probeSpacing * probeIndex,
                distanceToHub);
            Vector3 probe = doorway + directionToHub * distance;
            if (TrySampleOnRoomSide(probe, doorway, outward, sampleRadius, out position))
                return true;
            if (distance >= distanceToHub) break;
        }

        return false;
    }

    private static bool TrySampleOnRoomSide(
        Vector3 probe,
        Vector3 doorway,
        Vector3 outward,
        float radius,
        out Vector3 position)
    {
        position = default;
        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, radius, NavMesh.AllAreas))
            return false;

        float side = Vector3.Dot(hit.position - doorway, outward);
        if (side > -DoorSampleSideTolerance) return false;

        position = hit.position;
        return true;
    }

    private List<string> FindUnreachableModules()
    {
        List<string> unreachable = new List<string>();
        if (layout.Modules.Count == 0 ||
            !TrySampleModulePosition(layout.Modules[0], out Vector3 start))
        {
            unreachable.Add("<start room has no NavMesh>");
            return unreachable;
        }

        NavMeshPath path = new NavMeshPath();
        for (int moduleIndex = 0; moduleIndex < layout.Modules.Count; moduleIndex++)
        {
            DungeonPlacedModule module = layout.Modules[moduleIndex];
            if (!TrySampleModulePosition(module, out Vector3 target) ||
                !CalculateNavMeshPath(start, target, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                unreachable.Add($"{module.Data.name}[{moduleIndex}]");
            }
        }

        return unreachable;
    }

    private bool CalculateNavMeshPath(Vector3 start, Vector3 target, NavMeshPath path)
    {
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = Surface != null ? Surface.agentTypeID : 0,
            areaMask = NavMesh.AllAreas
        };
        return NavMesh.CalculatePath(start, target, filter, path);
    }

    private bool TrySampleModulePosition(DungeonPlacedModule module, out Vector3 position)
    {
        foreach (DoorPoint socket in module.Data.GetDoorPoints())
        {
            if (socket != null && socket.IsConnected && TrySampleInsideDoor(socket, out position))
                return true;
        }

        Bounds bounds = module.Bounds;
        Vector3 center = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z);
        if (NavMesh.SamplePosition(center, out NavMeshHit hit, settings.SampleRadius, NavMesh.AllAreas))
        {
            position = hit.position;
            return true;
        }

        position = default;
        return false;
    }

    private void ReplaceInvalidNavMeshColliders()
    {
        if (generatedRoot == null) return;

        int replacedCount = 0;
        int disabledCount = 0;
        foreach (MeshCollider meshCollider in generatedRoot.GetComponentsInChildren<MeshCollider>(true))
        {
            Mesh mesh = meshCollider.sharedMesh;
            if (!meshCollider.enabled || mesh == null || !IsInvalidNavMeshSource(mesh)) continue;

            Bounds bounds = mesh.bounds;
            bool hasReplacement = meshCollider.GetComponents<Collider>()
                .Any(collider => collider != meshCollider && collider.enabled && !collider.isTrigger);

            if (!hasReplacement && IsFinite(bounds.center) && IsFinite(bounds.size) &&
                bounds.size.sqrMagnitude > 0.000001f)
            {
                BoxCollider replacement = meshCollider.gameObject.AddComponent<BoxCollider>();
                replacement.center = bounds.center;
                replacement.size = bounds.size;
                replacement.isTrigger = meshCollider.isTrigger;
                replacedCount++;
            }
            else if (!hasReplacement)
            {
                disabledCount++;
            }

            meshCollider.enabled = false;
        }

        if (replacedCount > 0 || disabledCount > 0)
        {
            Debug.Log(
                $"NavMesh sanitized imported colliders: {replacedCount} replaced, " +
                $"{disabledCount} disabled.",
                owner);
        }
    }

    private static bool IsInvalidNavMeshSource(Mesh mesh)
    {
        if (mesh.name.StartsWith("JOINT.", StringComparison.OrdinalIgnoreCase)) return true;
        if (!mesh.isReadable) return false;

        try
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length < 3) return true;
            return vertices.Any(vertex => !IsFinite(vertex));
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private readonly struct NavMeshBreakMarker
    {
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public bool MissingSample { get; }

        public NavMeshBreakMarker(Vector3 start, Vector3 end, bool missingSample)
        {
            Start = start;
            End = end;
            MissingSample = missingSample;
        }
    }
}
