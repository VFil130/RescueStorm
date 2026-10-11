using System.Collections;
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

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 3f;
    [SerializeField] private int damage = 1;

    [Header("Patrol")]
    [SerializeField] private float patrolWaitTime = 5f;
    [SerializeField] private float patrolRadius = 15f;
    [SerializeField] private float stuckTime = 5f;

    [Header("Chase")]
    [SerializeField] private float chaseGiveUpTime = 8f;
    [SerializeField] private float chaseProgressThreshold = 1.5f;
    [SerializeField] private float chaseGiveUpCooldown = 5f;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float fleeThreshold = 0.3f;
    [SerializeField] private float regenRate = 8f;

    [Header("Vent")]
    [SerializeField] private float ventCooldown = 12f;
    [SerializeField] private float ventUseRange = 30f;
    [SerializeField] private float ventWalkTimeout = 10f;
    [SerializeField] private float teleportDelay = 1f;
    [SerializeField] private string enterVentAnimation = "VentEnter";
    [SerializeField] private string exitVentAnimation = "VentExit";

    [Header("Trap")]
    [SerializeField] private GameObject trapPrefab;
    [SerializeField, Min(0)] private int maxTraps = 3;
    [SerializeField] private float trapCooldown = 8f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    private NavMeshAgent agent;
    [SerializeField] private Transform target;
    private Vector3 patrolTarget;
    private Vector3 hintPoint;
    private bool hasHintPoint;
    private float attackTimer = 0f;
    private float patrolTimer = 0f;
    private int currentHealth;
    private float regenTimer;
    private float ventTimer;
    private float trapTimer;
    private float stuckTimer;
    private float chaseTimer;
    private float chaseCooldownTimer;
    private float lastChaseDistance;
    private Vent enterVent;
    private Vent exitVent;
    private bool usingVent;
    private bool ventTeleporting;
    private float ventWalkTimer;
    private bool isFleeing;
    private BTNode behaviorTree;

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
        if (!isServer) return;

        attackTimer -= Time.deltaTime;
        trapTimer -= Time.deltaTime;
        chaseCooldownTimer = Mathf.Max(0f, chaseCooldownTimer - Time.deltaTime);
        UpdateAnimations();
        if (usingVent)
        {
            UpdateVentTravel();
            return;
        }
        if (HandleVentAndHeal()) return;
        if (behaviorTree != null)
            behaviorTree.Execute();
        CheckStuck();
        TryPlaceTrap();
    }

    [Server]
    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        regenTimer = 2f;
    }

    private void CheckStuck()
    {
        if (isFleeing || target != null) return;
        if (!agent.hasPath || agent.pathPending) return;
        if (Vector3.Distance(transform.position, agent.destination) < 1.5f)
        {
            stuckTimer = 0f;
            return;
        }

        if (agent.velocity.magnitude < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTime)
            {
                stuckTimer = 0f;
                agent.ResetPath();
                hasHintPoint = false;
                SetNewPatrolTarget();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void UpdateRegen()
    {
        if (currentHealth >= maxHealth) return;
        regenTimer -= Time.deltaTime;
        if (regenTimer <= 0f)
            currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(regenRate * Time.deltaTime));
    }

    private bool HandleVentAndHeal()
    {
        ventTimer -= Time.deltaTime;
        UpdateRegen();

        VentManager vm = VentManager.Instance;
        if (vm == null || vm.VentCount == 0) return isFleeing;

        if (isFleeing)
        {
            if (currentHealth >= maxHealth * fleeThreshold * 2f)
            {
                isFleeing = false;
                agent.speed = walkSpeed;
                return false;
            }
            FleeToClosestVent(vm);
            return true;
        }

        if (currentHealth < maxHealth * fleeThreshold)
        {
            FleeToClosestVent(vm);
            return true;
        }

        if (ventTimer <= 0f && !hasHintPoint && FindClosestPlayer() == null)
        {
            StartVentTravel(vm.GetRandomVent());
            return false;
        }

        return false;
    }

    private void FleeToClosestVent(VentManager vm)
    {
        Vent vent = vm.GetClosestVent(transform.position);
        if (vent == null) return;
        isFleeing = true;
        agent.speed = fleeSpeed;
        agent.SetDestination(vent.SpawnPosition);
    }

    private void TryVentTravelTowards(Vector3 destination)
    {
        VentManager vm = VentManager.Instance;
        if (vm == null || vm.VentCount == 0) return;
        if (ventTimer > 0f) return;

        float currentDist = Vector3.Distance(transform.position, destination);
        if (currentDist < ventUseRange) return;

        Vent exit = vm.GetClosestVent(destination);
        if (exit == null) return;

        float newDist = Vector3.Distance(exit.SpawnPosition, destination);
        if (newDist + 5f >= currentDist) return;

        StartVentTravel(exit);
    }

    private void StartVentTravel(Vent exit)
    {
        VentManager vm = VentManager.Instance;
        if (vm == null || exit == null || usingVent) return;

        Vent enter = vm.GetClosestVent(transform.position);
        if (enter == null) return;

        enterVent = enter;
        exitVent = exit;
        usingVent = true;
        ventWalkTimer = 0f;
        agent.speed = walkSpeed;
        agent.SetDestination(enter.SpawnPosition);
    }

    private void UpdateVentTravel()
    {
        if (ventTeleporting) return;

        ventWalkTimer += Time.deltaTime;
        if (ventWalkTimer >= ventWalkTimeout)
        {
            usingVent = false;
            ventTeleporting = false;
            ventTimer = ventCooldown;
            agent.isStopped = false;
            SetNewPatrolTarget();
            return;
        }

        if (Vector3.Distance(transform.position, enterVent.SpawnPosition) > 1.5f)
            return;

        ventTeleporting = true;
        StartCoroutine(EnterAndTeleport());
    }

    private IEnumerator EnterAndTeleport()
    {
        agent.isStopped = true;
        agent.ResetPath();
        PlayAnimation(enterVentAnimation);
        yield return new WaitForSeconds(teleportDelay);

        transform.position = exitVent.SpawnPosition;
        transform.rotation = exitVent.SpawnRotation;
        PlayAnimation(exitVentAnimation);
        yield return new WaitForSeconds(teleportDelay);

        usingVent = false;
        ventTeleporting = false;
        ventTimer = ventCooldown;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        SetNewPatrolTarget();
    }

    private void TryPlaceTrap()
    {
        if (trapPrefab == null || trapTimer > 0f) return;
        if (target == null) return;
        if (FindObjectsByType<Trap>(FindObjectsSortMode.None).Length >= maxTraps) return;

        Vector3 position = target.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            position = hit.position;

        GameObject trap = Instantiate(trapPrefab, position, Quaternion.identity);
        NetworkServer.Spawn(trap);
        trapTimer = trapCooldown;
    }

    private void BuildBehaviorTree()
    {
        behaviorTree = new BTSelector(new System.Collections.Generic.List<BTNode>
    {
        new BTSequence(new System.Collections.Generic.List<BTNode>
        {
            new BTCondition(IsTargetInAttackRange),
            new BTAction(() => AttackUpdate())
        }),
        new BTSequence(new System.Collections.Generic.List<BTNode>
        {
            new BTCondition(TryStartHunt),
            new BTAction(() => HuntUpdate())
        }),
        new BTAction(() => PatrolUpdate())
        });
    }

    private BTNode.Status AttackUpdate()
    {
        if (target == null) return BTNode.Status.Failure;

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }

        return IsTargetInAttackRange() ? BTNode.Status.Running : BTNode.Status.Failure;
    }

    private bool TryStartHunt()
    {
        if (chaseCooldownTimer > 0f) return false;

        Transform found = FindClosestPlayer();
        if (found == null)
        {
            ResetChaseProgress();
            return false;
        }

        if (found != target)
        {
            target = found;
            ResetChaseProgress();
        }

        return IsTargetInSight();
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
            GiveUpChase();
            return BTNode.Status.Failure;
        }

        if (distance <= lastChaseDistance - chaseProgressThreshold)
        {
            lastChaseDistance = distance;
            chaseTimer = 0f;
        }
        else
        {
            chaseTimer += Time.deltaTime;
        }

        bool cannotReach = !agent.pathPending &&
                           (agent.pathStatus == NavMeshPathStatus.PathPartial ||
                            agent.pathStatus == NavMeshPathStatus.PathInvalid);
        bool notMoving = agent.velocity.magnitude < 0.75f;
        bool stuck = (cannotReach || notMoving) && chaseTimer >= chaseGiveUpTime;
        bool tooLong = chaseTimer >= chaseGiveUpTime * 2f;
        if (stuck || tooLong)
        {
            GiveUpChase();
            return BTNode.Status.Failure;
        }

        agent.speed = chaseSpeed;
        agent.SetDestination(target.position);

        return BTNode.Status.Running;
    }

    private void ResetChaseProgress()
    {
        chaseTimer = 0f;
        lastChaseDistance = float.MaxValue;
    }

    private void GiveUpChase()
    {
        target = null;
        chaseCooldownTimer = chaseGiveUpCooldown;
        ResetChaseProgress();
        agent.ResetPath();
        agent.speed = walkSpeed;
    }

    private BTNode.Status PatrolUpdate()
    {
        agent.speed = walkSpeed;

        MonitorHint();

        if (hasHintPoint &&
            Vector3.Distance(transform.position, hintPoint) > 1f)
        {
            TryVentTravelTowards(hintPoint);
            agent.SetDestination(hintPoint);
            return BTNode.Status.Running;
        }

        hasHintPoint = false;

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
        return BTNode.Status.Running;
    }

    private void MonitorHint()
    {
        AiCoordinator ai = AiCoordinator.Instance;
        if (ai == null || !ai.HasTarget) return;

        if (!hasHintPoint)
        {
            hintPoint = ai.TargetPosition;
            hasHintPoint = true;
            return;
        }

        if (Vector3.Distance(ai.TargetPosition, hintPoint) > 10f)
            hintPoint = ai.TargetPosition;
    }

    private void PerformAttack()
    {
        if (target == null) return;

        Player player = target.GetComponent<Player>();
        if (player != null)
        {
            PlayAnimation("NeckBite");
            player.TakeDamage(damage);
        }
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection.y = 0;
        if (NavMesh.SamplePosition(transform.position + randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
        }
        else
        {
            patrolTarget = transform.position;
        }
    }
    private Transform FindClosestPlayer()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, sightRange);
        Transform closest = null;
        float minDistance = sightRange;

        foreach (Collider col in colliders)
        {
            Player player = col.GetComponent<Player>();
            if (player != null && player.Health > 0)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = col.transform;
                }
            }
        }
        return closest;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    private bool IsTargetInSight()
    {
        bool result = target != null && Vector3.Distance(transform.position, target.position) <= sightRange;
        return result;
    }
    private bool IsTargetInAttackRange()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= attackRange;
    }
    #region Animation Methods
    private void UpdateAnimations()
    {
        if (animator == null) return;
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("IsRun", isMoving);
        animator.SetBool("IsIdle", !isMoving);
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null) return;
        animator.SetBool("IsRun", false);
        animator.SetBool("IsIdle", false);
        animator.Play(stateName, 0, 0f);
    }
    #endregion
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
