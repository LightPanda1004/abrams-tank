
using UnityEngine;

public class AntiDroneLauncher : MonoBehaviour
{
    [Header("Launcher")]
    public Transform firePoint;
    public GameObject homingMissilePrefab;

    [Header("Target Detection")]
    public float detectionRange = 100f;

    [Header("Firing")]
    public KeyCode fireKey = KeyCode.R;
    public float reloadTime = 4f;
    public int ammunition = 8;

    [Header("Optional Effects")]
    public GameObject launchEffectPrefab;

    private AIDroneController currentTarget;
    private float reloadTimer;

    public bool IsReloading => reloadTimer > 0f;
    public float RemainingReload => Mathf.Max(0f, reloadTimer);
    public float ReloadProgress =>
        reloadTimer > 0f
            ? 1f - Mathf.Clamp01(reloadTimer / Mathf.Max(0.1f, reloadTime))
            : 1f;
    public int AmmoRemaining => ammunition;

    private void Update()
    {
        reloadTimer -= Time.deltaTime;

        if (Input.GetKeyDown(fireKey))
        {
            FireMissile();
        }
    }

    private void FindNearestDrone()
    {
        currentTarget = null;

        AIDroneController[] drones =
            FindObjectsOfType<AIDroneController>();

        float nearestDistance = detectionRange;

        foreach (AIDroneController drone in drones)
        {
            if (drone == null || !drone.gameObject.activeInHierarchy)
                continue;

            TankHealth droneHealth =
                drone.GetComponentInParent<TankHealth>();

            // Skip drones already marked as destroyed.
            if (droneHealth != null && droneHealth.IsDestroyed)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                drone.transform.position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                currentTarget = drone;
            }
        }
    }

    private void FireMissile()
    {
        if (reloadTimer > 0f)
        {
            Debug.Log("Anti-drone launcher is reloading.");
            return;
        }

        if (ammunition <= 0)
        {
            Debug.Log("Anti-drone missile ammunition is empty.");
            return;
        }

        if (firePoint == null || homingMissilePrefab == null)
        {
            Debug.LogError(
                "Assign Fire Point and Homing Missile Prefab!"
            );
            return;
        }

        // Refresh target before firing.
        FindNearestDrone();

        if (currentTarget == null)
        {
            Debug.Log("No AI drone in missile range.");
            return;
        }

        GameObject missileObject = Instantiate(
    homingMissilePrefab,
    firePoint.position,
    firePoint.rotation);

        HomingMissile missile =
            missileObject.GetComponent<HomingMissile>();

        if (missile == null)
        {
            Debug.LogError(
                "The missile prefab needs a HomingMissile component."
            );

            Destroy(missileObject);
            return;
        }

        missile.SetOwner(transform.root);
        missile.SetTarget(currentTarget.transform);

        if (launchEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                launchEffectPrefab,
                firePoint.position,
                firePoint.rotation
            );

            Destroy(effect, 3f);
        }

        ammunition--;
        reloadTimer = reloadTime;

        Debug.Log(
            "Anti-drone missile launched. Remaining ammunition: "
            + ammunition
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
