using System.Collections;
using Mirror;
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

        if (NetworkClient.isConnected && !NetworkServer.active)
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

        if (healthBar != null)
        {
            healthBar.UpdateHealth(currentHealth);
        }

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
        MonoBehaviour[] scripts =
            GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this ||
                script is NetworkBehaviour)
                continue;

            script.enabled = false;
        }

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

        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        bool isNetworked = networkIdentity != null && networkIdentity.netId != 0;

        if (isNetworked && networkTankPlayer != null &&
            NetworkServer.active)
        {
            networkTankPlayer.NotifyMultiplayerTankDestroyed();
        }

        DisableTank();

        if (isNetworked)
        {
            if (NetworkServer.active)
            {
                StartCoroutine(DespawnAfterDelay(networkIdentity));
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

    private IEnumerator DespawnAfterDelay(NetworkIdentity networkIdentity)
    {
        yield return new WaitForSeconds(destroyDelay);

        if (networkIdentity != null &&
            NetworkServer.active)
        {
            NetworkServer.Destroy(networkIdentity.gameObject);
        }
    }
}
