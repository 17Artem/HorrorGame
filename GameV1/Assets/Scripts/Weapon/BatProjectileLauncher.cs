using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Fires a glowing projectile from the bat's Empty child (the muzzle socket).</summary>
public class BatProjectileLauncher : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Health ownerHealth;
    [SerializeField, Min(1)] private int projectileDamage = 25;
    [SerializeField, Min(1f)] private float projectileSpeed = 16f;
    [SerializeField, Min(0.1f)] private float projectileCooldown = 0.6f;
    [SerializeField, Min(0.01f)] private float projectileRadius = 0.16f;
    [SerializeField, Min(0)] private int wallBounces = 4;
    [SerializeField] private Color projectileColor = new Color(0.2f, 1f, 0.25f, 1f);
    private float nextFireTime;

    private void Awake()
    {
        if (muzzle == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "Empty")
                {
                    muzzle = child;
                    break;
                }
            }
        }

        if (aimCamera == null) aimCamera = Camera.main;
        if (ownerHealth == null) ownerHealth = GetComponentInParent<Health>();

        if (muzzle == null)
            Debug.LogError("BatProjectileLauncher: socket 'Empty' was not found under the player/weapon.", this);
        if (aimCamera == null)
            Debug.LogError("BatProjectileLauncher: no aiming camera found. Assign the player's camera or tag it MainCamera.", this);
    }

    private void Update()
    {
        if (aimCamera == null || (ownerHealth != null && ownerHealth.IsDead)) return;
        if (FirePressed() && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + projectileCooldown;
            Fire();
        }
    }

    private bool FirePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    private void Fire()
    {
        if (muzzle == null || aimCamera == null) return;

        Vector3 origin = muzzle.position;
        Vector3 direction = aimCamera.transform.forward;
        RaycastHit[] aimHits = Physics.RaycastAll(aimCamera.transform.position, direction, 100f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(aimHits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit aimHit in aimHits)
        {
            Health hitHealth = aimHit.collider.GetComponentInParent<Health>();
            if (ownerHealth != null && hitHealth == ownerHealth)
                continue;
            direction = (aimHit.point - origin).normalized;
            break;
        }

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Green Bat Projectile";
        orb.transform.position = origin + direction * (projectileRadius + 0.05f);
        orb.transform.localScale = Vector3.one * projectileRadius * 2f;
        Renderer renderer = orb.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", projectileColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", projectileColor);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", projectileColor * 2.5f);
        }
        renderer.material = material;

        SphereCollider sphere = orb.GetComponent<SphereCollider>();
        Rigidbody body = orb.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction * projectileSpeed;
        foreach (Collider collider in GetComponentsInParent<Collider>())
            Physics.IgnoreCollision(sphere, collider);
        if (ownerHealth != null)
            foreach (Collider collider in ownerHealth.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(sphere, collider);

        GameObject glow = new GameObject("Projectile Glow");
        glow.transform.SetParent(orb.transform, false);
        Light light = glow.AddComponent<Light>();
        light.color = projectileColor;
        light.intensity = 1.4f;
        light.range = 2.2f;
        light.shadows = LightShadows.None;
        orb.AddComponent<BatProjectile>().Initialize(projectileDamage, ownerHealth, wallBounces, projectileSpeed);
    }
}
