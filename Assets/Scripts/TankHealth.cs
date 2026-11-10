
using System.Collections;
using UnityEngine;
using Unity.Netcode;

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
    private NetworkTankPlayer networkTankPlayer;
    private TankScopeZoom scopeZoom;

    public float CurrentHealth => currentHealth;

    public bool IsDestroyed => isDestroyed;

    public void SetReplicatedHealth(float health)
    {
        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);

        if (currentHealth < previousHealth)
            NotifyPlayerHit();

        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0f && !isDestroyed)
            DestroyTank();
    }

    void Awake()
    {
        currentHealth = maxHealth;
        networkTankPlayer = GetComponent<NetworkTankPlayer>();
        scopeZoom = GetComponentInChildren<TankScopeZoom>(true);
        if (scopeZoom == null)
            scopeZoom = GetComponentInParent<TankScopeZoom>();

        if (healthBar != null)
        {
            healthBar.Setup(transform, maxHealth);
            healthBar.UpdateHealth(currentHealth);
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, Transform attacker)
    {
        if (isDestroyed || damage <= 0f)
            return;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        NotifyPlayerHit();

        if (!isPlayerTank && attacker != null)
        {
            AITankController aiTankController =
                GetComponent<AITankController>();
            if (aiTankController != null)
                aiTankController.NotifyAttackedBy(attacker);
        }

        if (networkTankPlayer != null)
            networkTankPlayer.SetServerHealth(currentHealth);

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

    private void NotifyPlayerHit()
    {
        if (!isPlayerTank)
            return;

        if (scopeZoom == null)
        {
            scopeZoom = GetComponentInChildren<TankScopeZoom>(true);
            if (scopeZoom == null)
                scopeZoom = GetComponentInParent<TankScopeZoom>();
        }

        if (scopeZoom != null)
            scopeZoom.NotifyTankHit();
    }
    
private void DisableTank()
{
    // Disable all valid scripts on this tank and its children.
    MonoBehaviour[] scripts =
        GetComponentsInChildren<MonoBehaviour>(true);

    foreach (MonoBehaviour script in scripts)
    {
        // Missing scripts can appear as null entries.
        if (script == null || script == this ||
            script is NetworkBehaviour)
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

        if (healthBar != null)
            Destroy(healthBar.gameObject);

        if (networkTankPlayer == null)
            networkTankPlayer = GetComponent<NetworkTankPlayer>();

        MinimapMarker marker = GetComponent<MinimapMarker>();

        if (marker != null)
        {
            Destroy(marker);
        }
        if (GameDirector.Instance != null)
        {
            GameDirector.Instance.NotifyDestroyed(this);
        }
        PlayDestructionEffects();

        NetworkObject networkObject = GetComponent<NetworkObject>();
        bool isNetworked = networkObject != null && networkObject.IsSpawned;

        if (isNetworked && networkTankPlayer != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer)
        {
            networkTankPlayer.NotifyMultiplayerTankDestroyed();
        }

        DisableTank();

        if (isNetworked)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                StartCoroutine(DespawnAfterDelay(networkObject));
            }
        }
        else
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void PlayDestructionEffects()
    {
        if (destructionExplosionPrefab == null ||
            destroyedFireAndSmoke == null)
            return;

        GameObject explosion = Instantiate(
            destructionExplosionPrefab,
            transform.position,
            Quaternion.identity
        );
        Destroy(explosion, 5f);

        GameObject fireAndSmoke = Instantiate(
            destroyedFireAndSmoke,
            transform.position + Vector3.up * 0.7f,
            Quaternion.identity
        );
        Destroy(fireAndSmoke, destroyDelay);
    }

    private IEnumerator DespawnAfterDelay(NetworkObject networkObject)
    {
        yield return new WaitForSeconds(destroyDelay);

        if (networkObject != null && networkObject.IsSpawned &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer)
        {
            networkObject.Despawn(true);
        }
    }
}
