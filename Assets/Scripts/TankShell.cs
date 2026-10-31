
using System.Collections;
using UnityEngine;

public class TankShell : MonoBehaviour
{
    [Header("Shell Settings")]
    public float damage = 25f;
    public float lifetime = 5f;

    [Header("Impact Effect")]
    public GameObject explosionPrefab;
    public float explosionDestroyDelay = 5f;

    [Header("Disappear Settings")]
    public float disappearDuration = 0.4f;
    public float trailCleanupTime = 5f;

    private Transform ownerRoot;
    private Rigidbody rb;
    private bool hasExploded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
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

        Vector3 hitPosition = other.ClosestPoint(transform.position);

        // Apply damage to a tank.
        TankHealth health = other.GetComponentInParent<TankHealth>();

        if (health != null)
            health.TakeDamage(damage);

        // Create the impact explosion.
        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionPrefab,
                hitPosition,
                explosionPrefab.transform.rotation
            );

            Destroy(effect, explosionDestroyDelay);
        }

        BeginDisappearance();
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

        Destroy(gameObject);
    }
}
