using System;
using System.Collections;
using Mirror;
using UnityEngine;

public sealed class RepairToolItem : ItemObject
{
    public static RepairToolItem Local { get; private set; }

    public LeakHazard CurrentTarget { get; private set; }
    public event Action<LeakHazard> TargetChanged;

    [Tooltip("Charges drained per second while the repair action is held.")]
    [SerializeField, Min(0f)] private float chargeDrainPerSecond = 1f;

    [Tooltip("Looping effect prefab name inside Resources/Particles.")]
    [SerializeField] private string repairEffectName = "RepairParticle";

    [Tooltip("Muzzle transform the effect is attached to.")]
    [SerializeField] private Transform effectAnchor;

    [Tooltip("Raycast mask used to aim at leaks.")]
    [SerializeField] private LayerMask leakMask;

    private Coroutine repairCoroutine;
    private LeakHazard aimedTarget;
    private Player owner;
    private LeakHazard serverTarget;
    private float chargeTimer;

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

    private int LeakMask
    {
        get
        {
            int layer = LayerMask.NameToLayer("Leak");
            return layer < 0 ? leakMask.value : 1 << layer;
        }
    }

    public override void UseHeld(GameObject user)
    {
        owner = user.GetComponent<Player>();
        Debug.Log("F4");
        if (repairCoroutine != null) return;
        repairCoroutine = StartCoroutine(RepairRoutine(user));
    }

    public override void ReleaseHeld(GameObject user)
    {
        if (repairCoroutine == null) return;
        StopCoroutine(repairCoroutine);
        repairCoroutine = null;
        chargeTimer = 0f;
        StopRepair();
        ParticleManager.Instance.SetLoopingParticleVisible(repairEffectName, false);
    }

    private IEnumerator RepairRoutine(GameObject user)
    {
        Debug.Log("F5");
        while (true)
        {
            Debug.Log("F6");
            LeakHazard target = FindAimedLeak(user);
            if (target != null && !target.IsRepaired)
                TryBeginRepair(target);
            else
                StopRepair();

            if (charges <= 0)
            {
                StopRepair();
                ParticleManager.Instance.SetLoopingParticleVisible(repairEffectName, false);
                repairCoroutine = null;
                yield break;
            }

            ParticleManager.Instance.ShowLoopingParticle(repairEffectName, effectAnchor);

            if (charges > 0 && chargeDrainPerSecond > 0f)
            {
                chargeTimer += Time.deltaTime;
                while (chargeTimer >= 1f)
                {
                    chargeTimer -= 1f;
                    charges--;
                    if (charges <= 0)
                    {
                        StopRepair();
                        ParticleManager.Instance.SetLoopingParticleVisible(repairEffectName, false);
                        repairCoroutine = null;
                        yield break;
                    }
                }
            }

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
            Debug.Log("F1");
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

    [Command(requiresAuthority = false)]
    private void CmdBeginRepair(
        NetworkIdentity leakIdentity,
        NetworkConnectionToClient sender = null)
    {
        Player player = ResolvePlayer(sender);
        LeakHazard leak = leakIdentity != null ? leakIdentity.GetComponent<LeakHazard>() : null;
        Debug.Log("F2");
        if (serverTarget != null && serverTarget != leak)
            serverTarget.EndRepair(player);
        if (leak == null || !leak.TryBeginRepair(player))
            serverTarget = null;
        else
            serverTarget = leak;
    }

    [Command(requiresAuthority = false)]
    private void CmdStopRepair(NetworkConnectionToClient sender = null)
    {
        Player player = ResolvePlayer(sender);
        if (serverTarget != null)
            serverTarget.EndRepair(player);
        serverTarget = null;
    }

    private static Player ResolvePlayer(NetworkConnectionToClient sender)
    {
        if (sender == null || sender.identity == null) return null;
        return sender.identity.GetComponent<Player>();
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
        if (!Physics.Raycast(ray, out RaycastHit hit, 2.5f, LeakMask, QueryTriggerInteraction.Collide))
            return null;
        return hit.collider.GetComponentInParent<LeakHazard>();
    }
}