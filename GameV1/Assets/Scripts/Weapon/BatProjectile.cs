using UnityEngine;

public class BatProjectile : MonoBehaviour
{
    private int damage;
    private Health owner;
    private int remainingBounces;
    private float constantSpeed;
    private bool initialized;

    public void Initialize(int projectileDamage, Health projectileOwner, int bounces, float speed)
    {
        damage = projectileDamage;
        owner = projectileOwner;
        remainingBounces = Mathf.Max(0, bounces);
        constantSpeed = Mathf.Max(0f, speed);
        initialized = true;
        Destroy(gameObject, 5f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!initialized) return;
        Health target = collision.collider.GetComponentInParent<Health>();
        if (target != null && target != owner && collision.collider.GetComponentInParent<EnemyAI>() != null)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (remainingBounces <= 0 || collision.contactCount == 0)
        {
            Destroy(gameObject);
            return;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            Destroy(gameObject);
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        Vector3 reflectedDirection = Vector3.Reflect(body.linearVelocity.normalized, contact.normal);
        body.linearVelocity = reflectedDirection * constantSpeed;
        SphereCollider sphere = GetComponent<SphereCollider>();
        float separation = sphere != null ? sphere.radius * transform.lossyScale.x + 0.01f : 0.05f;
        body.position = contact.point + contact.normal * separation;
        remainingBounces--;
    }
}
