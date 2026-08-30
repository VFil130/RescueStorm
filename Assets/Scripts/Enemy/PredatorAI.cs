using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class PredatorAI : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float chaseSpeed = 6f;
    [SerializeField] private float fleeSpeed = 7f;

    [Header("Senses")]
    [SerializeField] private float sightRange = 15f;
    [SerializeField] private float attackRange = 2f;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float fleeThreshold = 0.25f;
    [SerializeField] private float cautiousThreshold = 0.5f;
    [SerializeField] private float regenRate = 5f;
    [SerializeField] private float regenDelay = 5f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 3f;
    [SerializeField] private int damage = 1;

    [Header("Patrol")]
    [SerializeField] private float patrolWaitTime = 5f;
    [SerializeField] private float patrolRadius = 15f;

    [Header("Traps")]
    [SerializeField] private GameObject trapPrefab;
    [SerializeField, Min(0)] private int maxTraps = 5;
    [SerializeField] private float trapCooldown = 8f;

    [Header("Ambush")]
    [SerializeField] private float ambushWaitTime = 15f;
    [SerializeField] private float ambushSearchRange = 30f;

    [Header("Vent")]
    [SerializeField] private float ventCooldown = 10f;
    [SerializeField] private float ventTeleportRange = 40f;
    [SerializeField] private float respawnDelay = 7f;

    [Header("Intel")]
    [SerializeField] private float intelQueryInterval = 2f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    private NavMeshAgent agent;
    [SerializeField] private Transform target;
    private Vector3 patrolTarget;
    private float attackTimer;
    private float patrolTimer;
    private float trapTimer;
    private float ventTimer;
    private float intelTimer;
    private float ambushTimer;
    private float regenTimer;
    private int currentHealth;
    private int currentTrapCount;
    private bool isFleeing;
    private bool isRespawning;
    private Vector3 ambushPosition;
    private bool hasAmbushPosition;
    private BTNode behaviorTree;
    private readonly Collider[] overlapBuffer = new Collider[32];

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        agent.enabled = true;
        agent.speed = walkSpeed;
        currentHealth = maxHealth;
        SetNewPatrolTarget();
        BuildBehaviorTree();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer) agent.enabled = false;
    }

    void Update()
    {
        if (!isServer || isRespawning) return;

        attackTimer -= Time.deltaTime;
        trapTimer -= Time.deltaTime;
        ventTimer -= Time.deltaTime;
        intelTimer -= Time.deltaTime;
        ambushTimer -= Time.deltaTime;

        UpdateRegen();
        UpdateAnimations();

        if (behaviorTree != null)
            behaviorTree.Execute();
    }

    [Server]
    public void TakeDamage(int amount)
    {
        if (isRespawning) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        regenTimer = regenDelay;

        if (currentHealth <= 0)
            StartCoroutine(RespawnThroughVent());
    }

    private void UpdateRegen()
    {
        if (currentHealth >= maxHealth) return;

        regenTimer -= Time.deltaTime;
        if (regenTimer <= 0f)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(regenRate * Time.deltaTime));
        }
    }

    private IEnumerator RespawnThroughVent()
    {
        isRespawning = true;
        agent.enabled = false;
        RpcHideMonster();

        yield return new WaitForSeconds(respawnDelay);

        Vent vent = VentManager.Instance != null
            ? VentManager.Instance.GetRandomVent()
            : null;

        if (vent != null)
        {
            transform.position = vent.SpawnPosition;
            transform.rotation = vent.SpawnRotation;
        }

        currentHealth = maxHealth;
        isFleeing = false;
        agent.enabled = true;
        agent.speed = walkSpeed;
        SetNewPatrolTarget();
        RpcShowMonster();
        isRespawning = false;
    }

    [ClientRpc]
    private void RpcHideMonster()
    {
        gameObject.SetActive(false);
    }

    [ClientRpc]
    private void RpcShowMonster()
    {
        gameObject.SetActive(true);
    }

    private void BuildBehaviorTree()
    {
        var fleeSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(IsHealthLow) { nodeName = "IsHealthLow" },
            new BTAction(() => FleeToVentUpdate()) { nodeName = "FleeToVent" }
        }) { nodeName = "FleeSequence" };

        var ventTeleportSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(CanTeleportThroughtVent) { nodeName = "CanVentTeleport" },
            new BTAction(() => VentTeleportUpdate()) { nodeName = "VentTeleport" }
        }) { nodeName = "VentTeleportSequence" };

        var ambushSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(HasAmbushPoint) { nodeName = "HasAmbushPoint" },
            new BTAction(() => AmbushUpdate()) { nodeName = "AmbushUpdate" }
        }) { nodeName = "AmbushSequence" };

        var trapSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(CanPlaceTrap) { nodeName = "CanPlaceTrap" },
            new BTAction(() => PlaceTrapUpdate()) { nodeName = "PlaceTrap" }
        }) { nodeName = "TrapSequence" };

        var attackSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(IsTargetInAttackRange) { nodeName = "IsTargetInAttackRange" },
            new BTAction(() => AttackUpdate()) { nodeName = "AttackUpdate" }
        }) { nodeName = "AttackSequence" };

        var intelHuntSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(HasIntelTarget) { nodeName = "HasIntelTarget" },
            new BTAction(() => IntelHuntUpdate()) { nodeName = "IntelHuntUpdate" }
        }) { nodeName = "IntelHuntSequence" };

        var huntSequence = new BTSequence(new List<BTNode>
        {
            new BTCondition(() => { target = FindClosestPlayer(); return IsTargetInSight(); }) { nodeName = "FindTarget" },
            new BTAction(() => HuntUpdate()) { nodeName = "HuntUpdate" }
        }) { nodeName = "HuntSequence" };

        var patrolAction = new BTAction(() => PatrolUpdate()) { nodeName = "PatrolUpdate" };

        behaviorTree = new BTSelector(new List<BTNode>
        {
            fleeSequence,
            ventTeleportSequence,
            ambushSequence,
            trapSequence,
            attackSequence,
            intelHuntSequence,
            huntSequence,
            patrolAction
        }) { nodeName = "Root" };
    }

    private bool IsHealthLow() => (float)currentHealth / maxHealth < fleeThreshold;

    private bool IsHealthCautious() => (float)currentHealth / maxHealth < cautiousThreshold;

    private BTNode.Status FleeToVentUpdate()
    {
        if (isFleeing)
        {
            if (!agent.hasPath) return BTNode.Status.Running;
            return BTNode.Status.Running;
        }

        Vent vent = VentManager.Instance != null
            ? VentManager.Instance.GetClosestVent(transform.position)
            : null;

        if (vent == null)
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
            randomDir.y = 0;
            if (NavMesh.SamplePosition(transform.position + randomDir, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            {
                agent.speed = fleeSpeed;
                agent.SetDestination(hit.position);
            }
            return BTNode.Status.Running;
        }

        agent.speed = fleeSpeed;
        agent.SetDestination(vent.SpawnPosition);
        isFleeing = true;

        if (Vector3.Distance(transform.position, vent.SpawnPosition) < 2f)
        {
            StartCoroutine(RespawnThroughVent());
            return BTNode.Status.Running;
        }

        return BTNode.Status.Running;
    }

    private bool CanTeleportThroughtVent()
    {
        if (ventTimer > 0f) return false;
        if (target == null) return false;
        float distToTarget = Vector3.Distance(transform.position, target.position);
        return distToTarget > ventTeleportRange;
    }

    private BTNode.Status VentTeleportUpdate()
    {
        Vent vent = VentManager.Instance != null
            ? VentManager.Instance.GetRandomVent()
            : null;

        if (vent == null) return BTNode.Status.Failure;

        transform.position = vent.SpawnPosition;
        transform.rotation = vent.SpawnRotation;
        ventTimer = ventCooldown;

        if (target != null)
            agent.SetDestination(target.position);

        return BTNode.Status.Success;
    }

    private bool HasAmbushPoint()
    {
        if (!hasAmbushPosition) FindAmbushPoint();
        return hasAmbushPosition;
    }

    private void FindAmbushPoint()
    {
        List<Vector3> candidates = new List<Vector3>();

        Survivor[] survivors = FindObjectsByType<Survivor>(FindObjectsSortMode.None);
        foreach (Survivor s in survivors)
        {
            if (s.GetState() != Survivor.SurvivorState.Waiting) continue;
            float dist = Vector3.Distance(transform.position, s.transform.position);
            if (dist < ambushSearchRange)
                candidates.Add(s.transform.position);
        }

        EvacuateZone evac = FindFirstObjectByType<EvacuateZone>();
        if (evac != null)
        {
            float dist = Vector3.Distance(transform.position, evac.transform.position);
            if (dist < ambushSearchRange)
                candidates.Add(evac.transform.position);
        }

        if (candidates.Count == 0)
        {
            hasAmbushPosition = false;
            return;
        }

        Vector3 chosen = candidates[Random.Range(0, candidates.Count)];
        Vector3 offset = Random.insideUnitSphere * 3f;
        offset.y = 0;

        if (NavMesh.SamplePosition(chosen + offset, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            ambushPosition = hit.position;
            hasAmbushPosition = true;
            ambushTimer = ambushWaitTime;
        }
        else
        {
            hasAmbushPosition = false;
        }
    }

    private BTNode.Status AmbushUpdate()
    {
        if (ambushTimer <= 0f)
        {
            hasAmbushPosition = false;
            return BTNode.Status.Failure;
        }

        float distToAmbush = Vector3.Distance(transform.position, ambushPosition);
        if (distToAmbush > 2f)
        {
            agent.speed = walkSpeed;
            agent.SetDestination(ambushPosition);
            return BTNode.Status.Running;
        }

        agent.isStopped = true;
        return BTNode.Status.Running;
    }

    private bool CanPlaceTrap()
    {
        return trapPrefab != null
            && currentTrapCount < maxTraps
            && trapTimer <= 0f
            && IsHealthCautious() == false;
    }

    private BTNode.Status PlaceTrapUpdate()
    {
        Vector3 trapPosition = transform.position + transform.forward * 2f;
        if (NavMesh.SamplePosition(trapPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            GameObject trap = Instantiate(trapPrefab, hit.position, Quaternion.identity);
            NetworkServer.Spawn(trap);
            currentTrapCount++;
            trapTimer = trapCooldown;
        }

        return BTNode.Status.Success;
    }

    private bool IsTargetInAttackRange()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= attackRange;
    }

    private BTNode.Status AttackUpdate()
    {
        if (target == null) return BTNode.Status.Failure;

        agent.isStopped = true;

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }

        if (!IsTargetInAttackRange())
        {
            agent.isStopped = false;
            return BTNode.Status.Failure;
        }

        return BTNode.Status.Running;
    }

    private bool HasIntelTarget()
    {
        if (intelTimer > 0f) return false;
        intelTimer = intelQueryInterval;

        if (MonsterIntel.Instance == null || !MonsterIntel.Instance.HasTarget)
            return false;

        target = MonsterIntel.Instance.TargetPosition != Vector3.zero
            ? FindClosestPlayerInRange(MonsterIntel.Instance.TargetPosition, sightRange * 2f)
            : null;

        return target != null;
    }

    private BTNode.Status IntelHuntUpdate()
    {
        if (target == null) return BTNode.Status.Failure;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= sightRange)
        {
            if (IsTargetInAttackRange())
                return BTNode.Status.Failure;

            return HuntUpdate();
        }

        agent.speed = chaseSpeed;
        agent.SetDestination(target.position);
        return BTNode.Status.Running;
    }

    private bool IsTargetInSight()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= sightRange;
    }

    private BTNode.Status HuntUpdate()
    {
        if (target == null)
        {
            agent.speed = walkSpeed;
            return BTNode.Status.Failure;
        }

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance > sightRange)
        {
            target = null;
            agent.speed = walkSpeed;
            return BTNode.Status.Failure;
        }

        agent.speed = chaseSpeed;
        agent.SetDestination(target.position);

        return BTNode.Status.Running;
    }

    private BTNode.Status PatrolUpdate()
    {
        agent.isStopped = false;
        agent.speed = IsHealthCautious() ? walkSpeed * 0.7f : walkSpeed;

        if (Vector3.Distance(transform.position, patrolTarget) < 1f)
        {
            patrolTimer += Time.deltaTime;
            if (patrolTimer >= patrolWaitTime)
            {
                SetNewPatrolTarget();
                patrolTimer = 0f;
            }
        }
        else
        {
            agent.SetDestination(patrolTarget);
        }

        FindAmbushPoint();
        return BTNode.Status.Running;
    }

    private void PerformAttack()
    {
        if (target == null) return;

        Player player = target.GetComponent<Player>();
        if (player != null)
        {
            PlayAttackAnimation();
            player.TakeDamage(damage);
        }
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection.y = 0;
        if (NavMesh.SamplePosition(transform.position + randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            patrolTarget = hit.position;
        else
            patrolTarget = transform.position;
    }

    private Transform FindClosestPlayer()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, sightRange, overlapBuffer);
        Transform closest = null;
        float minDistance = sightRange;

        for (int i = 0; i < count; i++)
        {
            Player player = overlapBuffer[i].GetComponent<Player>();
            if (player != null && player.Health > 0)
            {
                float distance = Vector3.Distance(transform.position, overlapBuffer[i].transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = overlapBuffer[i].transform;
                }
            }
        }
        return closest;
    }

    private Transform FindClosestPlayerInRange(Vector3 center, float range)
    {
        int count = Physics.OverlapSphereNonAlloc(center, range, overlapBuffer);
        Transform closest = null;
        float minDistance = range;

        for (int i = 0; i < count; i++)
        {
            Player player = overlapBuffer[i].GetComponent<Player>();
            if (player != null && player.Health > 0)
            {
                float distance = Vector3.Distance(center, overlapBuffer[i].transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = overlapBuffer[i].transform;
                }
            }
        }
        return closest;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    #region Animation Methods
    private void UpdateAnimations()
    {
        if (animator == null) return;
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("IsRun", isMoving);
        animator.SetBool("IsIdle", !isMoving);
    }

    private void PlayAttackAnimation()
    {
        if (animator == null) return;
        animator.SetBool("IsRun", false);
        animator.SetBool("IsIdle", false);
        animator.Play("NeckBite", 0, 0f);
    }
    #endregion

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, ambushSearchRange);

        if (hasAmbushPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(ambushPosition, 0.5f);
        }
    }
}
