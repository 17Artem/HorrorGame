using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private bool destroyOnDeath;

    [Tooltip("Компоненты, которые отключатся при смерти")]
    [SerializeField] private Behaviour[] disableOnDeath;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0;

    public event Action HealthChanged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        HealthChanged?.Invoke();

        if (IsDead)
            Die();
    }

    private void Die()
    {
        foreach (Behaviour component in disableOnDeath)
        {
            if (component != null)
                component.enabled = false;
        }

        if (destroyOnDeath)
            Destroy(gameObject);
    }
}
