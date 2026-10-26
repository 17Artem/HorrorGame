using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Health playerHealth;

    [Header("Обнаружение и атака")]
    [SerializeField, Min(0.1f)] private float detectionRange = 12f;
    [SerializeField, Min(0.1f)] private float attackRange = 1.8f;
    [SerializeField, Min(0.1f)] private float pursuitSpeed = 2f;
    [SerializeField, Min(1)] private int damage = 10;
    [SerializeField, Min(0.01f)] private float attackCooldown = 1f;

    [Header("Проверка стен")]
    [Tooltip("Только стены и другие препятствия. Не Player и не Enemy.")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float sightHeight = 0.8f;

    private NavMeshAgent agent;
    private Health ownHealth;
    private float nextAttackTime;
    private bool hasDetectedPlayer;
    private Quaternion initialRotation;
    private Vector3 initialModelForward;

    private void Awake()
    {
        initialRotation = transform.rotation;
        Transform visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        initialModelForward = Vector3.ProjectOnPlane(visual.forward, Vector3.up).normalized;
        agent = GetComponent<NavMeshAgent>();
        ownHealth = GetComponent<Health>();
        if (ownHealth == null)
            ownHealth = gameObject.AddComponent<Health>();

        if (GetComponentInChildren<HealthBar>() == null)
        {
            GameObject barObject = new GameObject("Enemy Health Bar");
            barObject.transform.SetParent(transform, false);
            barObject.AddComponent<HealthBar>();
        }

        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponentInParent<Health>();
        }

        if (agent != null)
        {
            agent.stoppingDistance = attackRange * 0.8f;
            agent.speed = pursuitSpeed;
            agent.updateRotation = false;
            agent.updateUpAxis = false;

            // Join a nearby NavMesh without snapping or repositioning the enemy.
            if (!agent.enabled && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.35f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                if (!agent.isOnNavMesh)
                    agent.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (ownHealth.IsDead || playerHealth == null || playerHealth.IsDead)
        {
            StopAgent();
            return;
        }

        Vector3 playerPosition = playerHealth.transform.position;
        float distance = Vector3.Distance(transform.position, playerPosition);
        Vector3 sightStart = transform.position + Vector3.up * sightHeight;
        Vector3 sightEnd = playerPosition + Vector3.up * sightHeight;
        bool canSeePlayer = distance <= detectionRange && !Physics.Linecast(
            sightStart, sightEnd, obstacleMask, QueryTriggerInteraction.Ignore);

        if (canSeePlayer)
            hasDetectedPlayer = true;

        if (!hasDetectedPlayer)
        {
            StopAgent();
            return;
        }

        if (distance <= attackRange)
        {
            StopAgent();
            FacePlayer(playerPosition);
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                playerHealth.TakeDamage(damage);
            }
        }
        else if (CanNavigate())
        {
            agent.isStopped = false;
            agent.SetDestination(playerPosition);
        }
        else
        {
            Vector3 direction = playerPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                FacePlayer(playerPosition);
                transform.position += direction.normalized * pursuitSpeed * Time.deltaTime;
            }
        }
    }

    private bool CanNavigate()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    private void StopAgent()
    {
        if (CanNavigate())
            agent.isStopped = true;
    }

    private void FacePlayer(Vector3 playerPosition)
    {
        Vector3 direction = playerPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 startingForward = initialModelForward;
        if (startingForward.sqrMagnitude < 0.001f)
            startingForward = Vector3.forward;

        // Imported monster is authored with its face turned 90 degrees from the forward axis.
        Vector3 correctedForward = Quaternion.AngleAxis(-90f, Vector3.up) * startingForward.normalized;
        float yawDelta = Vector3.SignedAngle(correctedForward, direction.normalized, Vector3.up);
        Quaternion targetRotation = Quaternion.AngleAxis(yawDelta, Vector3.up) * initialRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
    }
}
