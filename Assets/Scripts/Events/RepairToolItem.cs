using System;
using System.Collections;
using Mirror;
using UnityEngine;

public sealed class RepairToolItem : ItemObject
{
    public static RepairToolItem Local { get; private set; }

    public LeakHazard CurrentTarget { get; private set; }
    public event Action<LeakHazard> TargetChanged;

    private Coroutine repairCoroutine;
    private LeakHazard aimedTarget;
    private Player owner;
    private LeakHazard serverTarget;

    private void Awake()
    {
        if (IsStandalone) Local = this;
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        Local = this;
    }

    public override void OnStopAuthority()
    {
        base.OnStopAuthority();
        if (Local == this) Local = null;
        StopRepair();
    }

    private bool IsStandalone => !NetworkClient.active && !NetworkServer.active;

    public override void UseHeld(GameObject user)
    {
        owner = user.GetComponent<Player>();
        if (repairCoroutine != null) return;
        repairCoroutine = StartCoroutine(RepairRoutine(user));
    }

    public override void ReleaseHeld(GameObject user)
    {
        if (repairCoroutine == null) return;
        StopCoroutine(repairCoroutine);
        repairCoroutine = null;
        StopRepair();
    }

    private IEnumerator RepairRoutine(GameObject user)
    {
        while (true)
        {
            LeakHazard target = FindAimedLeak(user);
            if (target != null && !target.IsRepaired)
                TryBeginRepair(target);
            else
                StopRepair();
            yield return null;
        }
    }

    private void TryBeginRepair(LeakHazard target)
    {
        if (IsStandalone)
        {
            if (target != null && !target.TryBeginRepair(owner))
                return;
            SetTarget(target);
        }
        else
        {
            CmdBeginRepair(target.netIdentity);
            SetTarget(target);
        }
    }

    private void StopRepair()
    {
        if (IsStandalone)
            EndServerTarget(aimedTarget);
        else if (NetworkClient.active && isOwned)
            CmdStopRepair();

        SetTarget(null);
    }

    private void SetTarget(LeakHazard target)
    {
        if (CurrentTarget == target) return;

        if (IsStandalone && aimedTarget != null && aimedTarget != target)
            EndServerTarget(aimedTarget);
        aimedTarget = target;
        CurrentTarget = target;
        TargetChanged?.Invoke(target);
    }

    private void EndServerTarget(LeakHazard target)
    {
        target?.EndRepair(owner);
    }

    [Command]
    private void CmdBeginRepair(NetworkIdentity leakIdentity)
    {
        LeakHazard leak = leakIdentity != null ? leakIdentity.GetComponent<LeakHazard>() : null;
        if (serverTarget != null)
            serverTarget.EndRepair(owner);
        if (leak == null || !leak.TryBeginRepair(owner))
            serverTarget = null;
        else
            serverTarget = leak;
    }

    [Command]
    private void CmdStopRepair()
    {
        if (serverTarget != null)
            serverTarget.EndRepair(owner);
        serverTarget = null;
    }

    private void Update()
    {
        if (Local != this || repairCoroutine != null) return;

        Player player = GetComponentInParent<Player>();
        if (player == null) return;

        LeakHazard aimed = FindAimedLeak(player.gameObject);
        SetTarget(aimed);
    }

    private LeakHazard FindAimedLeak(GameObject user)
    {
        Player player = user.GetComponent<Player>();
        if (player == null) return null;

        Camera playerCamera = player.GetComponentInChildren<Camera>();
        if (playerCamera == null) return null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!Physics.Raycast(ray, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Collide))
            return null;

        return hit.collider.GetComponentInParent<LeakHazard>();
    }
}