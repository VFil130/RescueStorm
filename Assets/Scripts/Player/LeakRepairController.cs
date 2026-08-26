using System;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class LeakRepairController : NetworkBehaviour
{
    [SerializeField, Min(0.5f)] private float repairRange = 2.5f;
    [SerializeField] private LayerMask repairMask = -1;

    private Camera playerCamera;
    private Player player;
    private bool primaryActionHeld;
    private LeakHazard currentTarget;
    private LeakHazard serverRepairTarget;

    public event Action<LeakHazard> TargetChanged;
    public LeakHazard CurrentTarget => currentTarget;

    private bool IsStandalone =>
        !NetworkClient.active && !NetworkServer.active;
    private bool HasLocalControl =>
        isLocalPlayer || IsStandalone;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerCamera = GetComponentInChildren<Camera>(true);
    }

    private void Update()
    {
        if (!HasLocalControl) return;

        LeakHazard target = primaryActionHeld &&
                            player.GetSelectedIPickable() is RepairToolItem
            ? FindAimedLeak()
            : null;
        SetTarget(target);
    }

    public void SetPrimaryAction(bool held)
    {
        if (!HasLocalControl) return;

        primaryActionHeld = held;
        if (!held) SetTarget(null);
    }

    private LeakHazard FindAimedLeak()
    {
        if (playerCamera == null) return null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                repairRange,
                repairMask,
                QueryTriggerInteraction.Collide))
            return null;

        return hit.collider.GetComponentInParent<LeakHazard>();
    }

    private void SetTarget(LeakHazard target)
    {
        if (currentTarget == target) return;

        LeakHazard previous = currentTarget;
        currentTarget = target;

        if (IsStandalone)
        {
            if (previous != null) previous.EndRepair(player);
            if (currentTarget != null && !currentTarget.TryBeginRepair(player))
                currentTarget = null;
        }
        else if (isLocalPlayer)
        {
            if (previous != null) CmdStopRepair(previous.netIdentity);
            if (currentTarget != null) CmdBeginRepair(currentTarget.netIdentity);
        }

        TargetChanged?.Invoke(currentTarget);
    }

    [Command]
    private void CmdBeginRepair(NetworkIdentity leakIdentity)
    {
        if (serverRepairTarget != null)
            serverRepairTarget.EndRepair(player);

        serverRepairTarget = leakIdentity != null
            ? leakIdentity.GetComponent<LeakHazard>()
            : null;
        if (serverRepairTarget != null &&
            !serverRepairTarget.TryBeginRepair(player))
        {
            serverRepairTarget = null;
        }
    }

    [Command]
    private void CmdStopRepair(NetworkIdentity leakIdentity)
    {
        LeakHazard leak = leakIdentity != null
            ? leakIdentity.GetComponent<LeakHazard>()
            : null;
        leak?.EndRepair(player);

        if (serverRepairTarget == leak)
            serverRepairTarget = null;
    }

    public override void OnStopServer()
    {
        serverRepairTarget?.EndRepair(player);
        serverRepairTarget = null;
        base.OnStopServer();
    }

    private void OnDisable()
    {
        primaryActionHeld = false;
        if (IsStandalone && currentTarget != null)
            currentTarget.EndRepair(player);
        else if (NetworkClient.active && isLocalPlayer && currentTarget != null)
            CmdStopRepair(currentTarget.netIdentity);

        currentTarget = null;
    }
}
