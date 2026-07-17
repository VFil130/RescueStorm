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
    private Transform target;
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

        if (behaviorTree != null)
            behaviorTree.Execute();
    }

    private void BuildBehaviorTree()
    {
        behaviorTree = new BTSelector(new System.Collections.Generic.List<BTNode>
        {
            new BTSequence(new System.Collections.Generic.List<BTNode>
            {
                new BTCondition(() => target != null && Vector3.Distance(transform.position, target.position) <= attackRange),
                new BTAction(() => { AttackUpdate(); return BTNode.Status.Running; })
            }),

            new BTSequence(new System.Collections.Generic.List<BTNode>
            {
                new BTCondition(() => target != null && Vector3.Distance(transform.position, target.position) <= sightRange),
                new BTAction(() => { HuntUpdate(); return BTNode.Status.Running; })
            }),

            new BTAction(() => { PatrolUpdate(); return BTNode.Status.Running; })
        });
    }

    private void AttackUpdate()
    {
        if (target == null) return;

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    private void HuntUpdate()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance > sightRange)
        {
            target = null;
            return;
        }

        agent.speed = chaseSpeed;
        agent.SetDestination(target.position);
    }

    private void PatrolUpdate()
    {
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

    private void PlayAttack()
    {
        if (animator != null) animator.SetTrigger("Attack");
        if (audioSource != null) audioSource.Play();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}