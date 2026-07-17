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

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    private NavMeshAgent agent;
    [SerializeField]private Transform target;
    private Vector3 patrolTarget;
    private float attackTimer = 0f;
    private float patrolTimer = 0f;
    private BTNode behaviorTree;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        SetNewPatrolTarget();
        BuildBehaviorTree();
    }

    void Update()
    {
        if (!isServer) return;

        attackTimer -= Time.deltaTime;
        if (target == null)
        {
            target = FindClosestPlayer();
        }

        if (behaviorTree != null)
            behaviorTree.Execute();
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
            new BTCondition(IsTargetInSight),
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
        agent.speed = walkSpeed;

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

    private void PerformAttack()
    {
        if (target == null) return;

        Player player = target.GetComponent<Player>();
        if (player != null)
        {
            player.TakeDamage(damage);
            PlayAttack();
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
    private void PlayAttack()
    {
        if (animator != null) animator.SetTrigger("Attack");
        if (audioSource != null) audioSource.Play();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    private bool IsTargetInSight()
    {
        bool result = target != null && Vector3.Distance(transform.position, target.position) <= sightRange;
        Debug.Log($"IsTargetInSight: {result}");
        return result;
    }
    private bool IsTargetInAttackRange()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= attackRange;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}