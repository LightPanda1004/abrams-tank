
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankFire : MonoBehaviour
{
    public enum FireMethod
    {
        Mouse,
        Keyboard
    }

    [Header("Firing Method")]
    public FireMethod fireMethod = FireMethod.Mouse;

    [Header("Mouse Settings")]
    public int mouseButton = 0; // 0 = Left Mouse Button

    [Header("Keyboard Settings")]
    public KeyCode fireKey = KeyCode.Space;

    [Header("Projectile")]
    public GameObject shellPrefab;
    public Transform firePoint;
    public float shellSpeed = 40f;

    [Header("Reload Settings")]
    public float fireCooldown = 3f;

    [Header("Muzzle Effect")]
    public GameObject muzzleExplosionPrefab;
    public float muzzleEffectLifetime = 3f;

    [Header("Optional Reload UI")]
    public Slider reloadSlider;
    public TMP_Text reloadText;

    private float nextFireTime;
    private bool isReloading;
    private bool canFire;
    private bool ignoreMouseFireUntilRelease;
    private bool wasPaused;
    private float networkReloadDuration;

    public GameObject CreateNetworkedProjectileVisual()
    {
        if (shellPrefab == null || firePoint == null)
            return null;

        GameObject shell = Instantiate(
            shellPrefab,
            firePoint.position,
            firePoint.rotation
        );
        foreach (TankShell shellBehaviour in
            shell.GetComponentsInChildren<TankShell>(true))
        {
            shellBehaviour.enabled = false;
        }
        foreach (Collider shellCollider in
            shell.GetComponentsInChildren<Collider>(true))
        {
            shellCollider.enabled = false;
        }
        foreach (Rigidbody shellBody in
            shell.GetComponentsInChildren<Rigidbody>(true))
        {
            shellBody.velocity = Vector3.zero;
            shellBody.angularVelocity = Vector3.zero;
            shellBody.isKinematic = true;
            shellBody.detectCollisions = false;
        }
        Destroy(shell, 5f);
        return shell;
    }

    public void PlayNetworkedShotEffect()
    {
        if (firePoint != null && muzzleExplosionPrefab != null)
        {
            GameObject effect = Instantiate(
                muzzleExplosionPrefab,
                firePoint.position,
                firePoint.rotation
            );
            Destroy(effect, muzzleEffectLifetime);
        }
    }

    public void PlayNetworkedImpactEffect(Vector3 position)
    {
        if (shellPrefab == null)
            return;

        TankShell shell = shellPrefab.GetComponentInChildren<TankShell>(true);
        if (shell == null || shell.explosionPrefab == null)
            return;

        GameObject effect = Instantiate(
            shell.explosionPrefab,
            position,
            shell.explosionPrefab.transform.rotation
        );
        Destroy(effect, shell.explosionDestroyDelay);
    }

    public void NotifyNetworkedShot(float cooldown)
    {
        networkReloadDuration = Mathf.Max(0.1f, cooldown);
        nextFireTime = Time.time + networkReloadDuration;
        isReloading = true;
        SetReloadUIVisible(true);
    }

    public void UpdateNetworkReloadState()
    {
        UpdateReloadState();
    }

    public bool IsReloading => isReloading;
    public float RemainingReload => Mathf.Max(0f, nextFireTime - Time.time);
    public float ReloadProgress =>
        isReloading
            ? 1f - Mathf.Clamp01((nextFireTime - Time.time) /
                Mathf.Max(0.1f, fireCooldown))
            : 1f;

    void Start()
    {
        nextFireTime = 0f;
        isReloading = false;
        canFire = true;
        GameDirector gameDirector = GameDirector.Instance;
        wasPaused = gameDirector != null && gameDirector.IsPaused;

        SetReloadUIVisible(false);

        if (reloadSlider != null)
        {
            reloadSlider.minValue = 0f;
            reloadSlider.maxValue = 1f;
            reloadSlider.value = 1f;
        }

        if (reloadText != null)
            reloadText.text = "READY";

        Debug.Log("TankFire ready. Waiting for player input.");
    }

    void Update()
    {
        UpdateReloadState();

        GameDirector gameDirector = GameDirector.Instance;
        bool isPaused = gameDirector != null && gameDirector.IsPaused;
        if (fireMethod == FireMethod.Mouse)
        {
            if (isPaused || wasPaused)
                ignoreMouseFireUntilRelease = true;

            if (!Input.GetMouseButton(mouseButton))
                ignoreMouseFireUntilRelease = false;
        }

        wasPaused = isPaused;

        if (isPaused)
            return;

        if (!canFire || isReloading)
            return;

        bool firePressed = false;

        if (fireMethod == FireMethod.Mouse)
        {
            firePressed = !ignoreMouseFireUntilRelease &&
                Input.GetMouseButtonDown(mouseButton);
        }
        else
        {
            firePressed = Input.GetKeyDown(fireKey);
        }

        if (firePressed)
            Fire();
    }

    void Fire()
    {
        if (!canFire || isReloading ||
            Time.time < nextFireTime)
        {
            return;
        }

        if (shellPrefab == null || firePoint == null)
        {
            Debug.LogWarning(
                "TankFire: Assign Shell Prefab and Fire Point!"
            );
            return;
        }

        float cooldown = Mathf.Max(0.1f, fireCooldown);

        // Lock firing immediately.
        isReloading = true;
        nextFireTime = Time.time + cooldown;

        SetReloadUIVisible(true);

        SpawnLocalShell();

        // Spawn muzzle effect only after firing.
        if (muzzleExplosionPrefab != null)
        {
            GameObject effect = Instantiate(
                muzzleExplosionPrefab,
                firePoint.position,
                firePoint.rotation
            );

            Destroy(effect, muzzleEffectLifetime);
        }

        Debug.Log("Player tank fired.");
    }

    private void SpawnLocalShell()
    {
        GameObject shell = Instantiate(
            shellPrefab,
            firePoint.position,
            firePoint.rotation
        );

        TankShell shellScript = shell.GetComponent<TankShell>();
        if (shellScript != null)
            shellScript.SetOwner(transform);

        Rigidbody shellRigidbody = shell.GetComponent<Rigidbody>();

        if (shellRigidbody == null)
            shellRigidbody = shell.GetComponentInChildren<Rigidbody>();

        if (shellRigidbody != null)
        {
            shellRigidbody.isKinematic = false;
            shellRigidbody.velocity = firePoint.forward * shellSpeed;
        }
        else
        {
            Debug.LogWarning("TankFire: Shell prefab has no Rigidbody!");
        }
    }

    void UpdateReloadState()
    {
        float remaining = Mathf.Max(
            0f,
            nextFireTime - Time.time
        );

        if (isReloading && remaining <= 0f)
        {
            isReloading = false;
            networkReloadDuration = 0f;
            SetReloadUIVisible(false);

            Debug.Log("Reload complete. Ready to fire.");
        }

        if (!isReloading)
            return;

        float duration = Mathf.Max(
            0.1f,
            networkReloadDuration > 0f
                ? networkReloadDuration
                : fireCooldown
        );

        float progress = 1f -
            Mathf.Clamp01(remaining / duration);

        if (reloadSlider != null)
            reloadSlider.value = progress;

        if (reloadText != null)
            reloadText.text = remaining.ToString("0.0") + "s";
    }

    void SetReloadUIVisible(bool visible)
    {
        if (reloadSlider != null)
            reloadSlider.gameObject.SetActive(visible);

        if (reloadText != null)
            reloadText.gameObject.SetActive(visible);

    }

    public void SetStatusBarVisible(bool visible)
    {
        SetReloadUIVisible(visible && isReloading);
    }
}
