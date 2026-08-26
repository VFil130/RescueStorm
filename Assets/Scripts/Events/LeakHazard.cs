using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class LeakHazard : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float repairDuration = 4f;
    [SerializeField, Min(0.5f)] private float repairRange = 2.5f;
    [SerializeField] private ParticleSystem leakParticles;
    [SerializeField] private GameObject worldIndicator;
    [SerializeField] private AudioSource leakAudioSource;

    [SyncVar(hook = nameof(OnRepairProgressChanged))]
    private float repairProgress;
    [SyncVar(hook = nameof(OnRepairedChanged))]
    private bool repaired;

    private readonly HashSet<Player> repairers = new HashSet<Player>();
    private LeakEventManager eventManager;

    public event Action<float> ProgressChanged;
    public float RepairProgress => repairProgress;
    public bool IsRepaired => repaired;
    public float RepairDuration => repairDuration;

    private bool HasStateAuthority =>
        isServer || (!NetworkServer.active && !NetworkClient.active);

    public void Initialize(LeakEventManager manager)
    {
        if (!HasStateAuthority) return;

        eventManager = manager;
        repairProgress = 0f;
        repaired = false;
        ApplyVisualState();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyVisualState();
    }

    private void Update()
    {
        if (!HasStateAuthority || repaired) return;

        repairers.RemoveWhere(player => !CanRepair(player));
        if (repairers.Count == 0) return;

        repairProgress = Mathf.Clamp01(
            repairProgress +
            CalculateRepairProgressDelta(
                repairers.Count,
                Time.deltaTime,
                repairDuration));

        if (!NetworkServer.active)
            ProgressChanged?.Invoke(repairProgress);

        if (repairProgress >= 1f)
            CompleteRepair();
    }

    public bool TryBeginRepair(Player player)
    {
        if (!HasStateAuthority || repaired || !CanRepair(player)) return false;

        repairers.Add(player);
        return true;
    }

    public void EndRepair(Player player)
    {
        if (!HasStateAuthority || player == null) return;
        repairers.Remove(player);
    }

    private bool CanRepair(Player player)
    {
        if (player == null || repaired || !player.isActiveAndEnabled) return false;
        if (player.GetSelectedIPickable() is not RepairToolItem) return false;

        Vector3 targetPosition = transform.position;
        Vector3 playerPosition = player.transform.position + Vector3.up * 1.4f;
        if ((targetPosition - playerPosition).sqrMagnitude > repairRange * repairRange)
            return false;

        return HasLineOfSight(player, playerPosition, targetPosition);
    }

    private bool HasLineOfSight(Player player, Vector3 origin, Vector3 target)
    {
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction / distance,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        foreach (RaycastHit hit in hits.OrderBy(hit => hit.distance))
        {
            if (hit.transform == null) continue;
            if (hit.transform == player.transform ||
                hit.transform.IsChildOf(player.transform)) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                return true;
            if (hit.distance >= distance - 0.35f) return true;
            return false;
        }

        return true;
    }

    private void CompleteRepair()
    {
        if (repaired) return;

        repaired = true;
        repairProgress = 1f;
        repairers.Clear();
        ApplyVisualState();
        eventManager?.NotifyLeakRepaired(this);
    }

    private void OnRepairProgressChanged(float _, float newValue)
    {
        ProgressChanged?.Invoke(newValue);
    }

    private void OnRepairedChanged(bool _, bool __)
    {
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        bool active = !repaired;
        if (worldIndicator != null) worldIndicator.SetActive(active);

        if (leakParticles != null)
        {
            if (active && !leakParticles.isPlaying)
                leakParticles.Play();
            else if (!active && leakParticles.isPlaying)
                leakParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (leakAudioSource != null)
        {
            if (active && !leakAudioSource.isPlaying)
                leakAudioSource.Play();
            else if (!active)
                leakAudioSource.Stop();
        }
    }

    public static float CalculateRepairProgressDelta(
        int repairerCount,
        float deltaTime,
        float duration)
    {
        if (repairerCount <= 0 || deltaTime <= 0f) return 0f;
        return repairerCount * deltaTime / Mathf.Max(0.1f, duration);
    }

    private void OnDestroy()
    {
        if (HasStateAuthority)
            eventManager?.NotifyLeakDestroyed(this);
    }
}
