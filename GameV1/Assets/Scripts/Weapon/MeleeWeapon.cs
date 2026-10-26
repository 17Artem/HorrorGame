using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MeleeWeapon : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Health ownerHealth;

    [Header("Удар")]
    [SerializeField, Min(1)] private int damage = 25;
    [SerializeField, Min(0.1f)] private float attackDistance = 2f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.2f;
    [SerializeField, Min(0.01f)] private float attackCooldown = 0.5f;

    [Tooltip("Включить врагов и окружение, исключить игрока и оружие")]
    [SerializeField] private LayerMask hitMask = ~0;

    private float nextAttackTime;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (ownerHealth == null)
            ownerHealth = GetComponentInParent<Health>();
    }

    private void Update()
    {
        if (playerCamera == null)
            return;

        if (ownerHealth != null && ownerHealth.IsDead)
            return;

        if (AttackPressed() && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            Attack();
        }
    }

    private bool AttackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null &&
               Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private void Attack()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.SphereCast(
            ray,
            hitRadius,
            out RaycastHit hit,
            attackDistance,
            hitMask,
            QueryTriggerInteraction.Ignore))
        {
            Health target = hit.collider.GetComponentInParent<Health>();

            if (target != null && target != ownerHealth)
                target.TakeDamage(damage);
        }
    }
}
