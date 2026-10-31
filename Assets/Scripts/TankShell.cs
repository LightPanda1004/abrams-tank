
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TankShell : MonoBehaviour
{
    [Header("Shell Settings")]
    public float damage = 25f;
    public float lifetime = 5f;

    [Header("Impact Effect")]
    public GameObject explosionPrefab;
    public float explosionDestroyDelay = 5f;
    public float blastRadius = 5f;
    public float blastForce = 250f;
    public float blastUpwardsModifier = 0.5f;

    [Header("Disappear Settings")]
    public float disappearDuration = 0.4f;
    public float trailCleanupTime = 5f;

    private Transform ownerRoot;
    private Rigidbody rb;
    private bool hasExploded;
    private Vector3 previousPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        previousPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (!hasExploded && rb != null)
            previousPosition = rb.position;
    }

    void Start()
    {
        StartCoroutine(LifetimeRoutine());
    }

    public void SetOwner(Transform owner)
    {
        if (owner != null)
            ownerRoot = owner.root;
    }

    IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasExploded)
            BeginDisappearance();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasExploded)
            return;

        // Ignore the tank that fired this shell.
        if (ownerRoot != null && other.transform.root == ownerRoot)
            return;

        hasExploded = true;

        Vector3 hitPosition = GetImpactPoint(other, out Vector3 hitNormal);

        ApplyBlastForce(hitPosition);

        // Apply damage to a tank.
        TankHealth health = other.GetComponentInParent<TankHealth>();

        NetworkObject networkObject = GetComponent<NetworkObject>();
        bool isNetworkedShell = networkObject != null && networkObject.IsSpawned &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (health != null &&
            (!isNetworkedShell || NetworkManager.Singleton.IsServer))
            health.TakeDamage(damage);

        // Create the impact explosion.
        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionPrefab,
                hitPosition,
                Quaternion.LookRotation(hitNormal) *
                    explosionPrefab.transform.rotation
            );

            Destroy(effect, explosionDestroyDelay);
        }

        BeginDisappearance();
    }

    private Vector3 GetImpactPoint(Collider other, out Vector3 hitNormal)
    {
        Vector3 currentPosition = rb != null ? rb.position : transform.position;
        Vector3 travel = currentPosition - previousPosition;
        Vector3 direction = rb != null ? rb.velocity.normalized : travel.normalized;

        if (direction.sqrMagnitude > 0.001f)
        {
            Collider shellCollider = GetComponent<Collider>();
            float shellRadius = shellCollider != null
                ? shellCollider.bounds.extents.magnitude
                : 0f;
            float castDistance = travel.magnitude + shellRadius + 0.05f;

            if (other.Raycast(
                new Ray(previousPosition, direction),
                out RaycastHit hit,
                castDistance))
            {
                hitNormal = hit.normal;
                return hit.point;
            }
        }

        Vector3 closestPoint = other.ClosestPoint(currentPosition);
        hitNormal = (closestPoint - other.bounds.center).normalized;

        if (hitNormal.sqrMagnitude < 0.001f)
            hitNormal = direction.sqrMagnitude > 0.001f ? -direction : Vector3.up;

        return closestPoint;
    }

    private void ApplyBlastForce(Vector3 hitPosition)
    {
        if (blastRadius <= 0f || blastForce <= 0f)
            return;

        Collider[] nearbyColliders = Physics.OverlapSphere(
            hitPosition,
            blastRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            Rigidbody body = nearbyCollider.attachedRigidbody;

            if (body == null || body == rb || body.isKinematic ||
                !affectedBodies.Add(body))
                continue;

            if (ownerRoot != null && body.transform.root == ownerRoot)
                continue;

            body.AddExplosionForce(
                blastForce,
                hitPosition,
                blastRadius,
                blastUpwardsModifier,
                ForceMode.Impulse
            );
        }
    }

    void BeginDisappearance()
    {
        if (hasExploded == false)
            hasExploded = true;

        // Stop shell movement.
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Prevent further collisions.
        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
            col.enabled = false;

        // Detach particle trails so they survive the shell.
        ParticleSystem[] particles =
            GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem particle in particles)
        {
            particle.transform.SetParent(null, true);

            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting
            );

            Destroy(particle.gameObject, trailCleanupTime);
        }

        // Detach Trail Renderers too, if the shell uses them.
        TrailRenderer[] trails =
            GetComponentsInChildren<TrailRenderer>();

        foreach (TrailRenderer trail in trails)
        {
            trail.transform.SetParent(null, true);
            trail.emitting = false;

            Destroy(trail.gameObject, trailCleanupTime);
        }

        StartCoroutine(DisappearSmoothly());
    }

    IEnumerator DisappearSmoothly()
    {
        Vector3 originalScale = transform.localScale;
        float timer = 0f;

        while (timer < disappearDuration)
        {
            timer += Time.deltaTime;

            float progress = Mathf.Clamp01(
                timer / disappearDuration
            );

            transform.localScale =
                Vector3.Lerp(originalScale, Vector3.zero, progress);

            yield return null;
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        bool isNetworkedShell = networkObject != null && networkObject.IsSpawned &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isNetworkedShell)
        {
            if (NetworkManager.Singleton.IsServer)
                networkObject.Despawn(true);

            yield break;
        }

        Destroy(gameObject);
    }
}
