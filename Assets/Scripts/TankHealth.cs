
using UnityEngine;

public class TankHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Health Bar")]
    public TankHealthBar healthBar;

    [Header("Destruction Settings")]
    public GameObject destructionExplosionPrefab;
    public GameObject destroyedFireAndSmoke;
    public GameObject miniMarker;
    public float destroyDelay = 0f;

    [Header("Tank Type")]
    public bool isPlayerTank = false;

    private bool isDestroyed = false;

    public float CurrentHealth => currentHealth;

    public bool IsDestroyed => isDestroyed;
    void Awake()
    {
        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.Setup(transform, maxHealth);
            healthBar.UpdateHealth(currentHealth);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDestroyed || damage <= 0f)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        // Update the health bar.
        if (healthBar != null)
        {
            healthBar.UpdateHealth(currentHealth);
        }

        // Destroy the tank when its health reaches zero.
        if (currentHealth <= 0f)
        {
            DestroyTank();
        }
    }
    
private void DisableTank()
{
    // Disable all valid scripts on this tank and its children.
    MonoBehaviour[] scripts =
        GetComponentsInChildren<MonoBehaviour>(true);

    foreach (MonoBehaviour script in scripts)
    {
        // Missing scripts can appear as null entries.
        if (script == null || script == this)
            continue;

        script.enabled = false;
    }

    // Stop all valid Rigidbody components.
    Rigidbody[] rigidbodies =
        GetComponentsInChildren<Rigidbody>(true);

    foreach (Rigidbody rb in rigidbodies)
    {
        if (rb == null)
            continue;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }
}


    void DestroyTank()
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        Destroy(healthBar.gameObject);

        MinimapMarker marker = GetComponent<MinimapMarker>();

        if (marker != null)
        {
            Destroy(marker);
        }
        if (GameDirector.Instance != null)
        {
            GameDirector.Instance.NotifyDestroyed(this);
        }
        // Play the destruction effect, if assigned.
        if (destructionExplosionPrefab != null && destroyedFireAndSmoke != null)
        {
            GameObject Explodeffect = Instantiate(
                destructionExplosionPrefab,
                transform.position,
                Quaternion.identity
            );

            Destroy(Explodeffect, 5f);

            GameObject FireandSmoke = Instantiate(destroyedFireAndSmoke, transform.position + Vector3.up * 0.7f, Quaternion.identity);
            Destroy(FireandSmoke, destroyDelay);
        }

        // Destroy the tank.
        DisableTank();
        Destroy(gameObject, destroyDelay);
    }
}
