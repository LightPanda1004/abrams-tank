
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class HomingMissile : MonoBehaviour
{
    [Header("Launch")]
    public float boostDuration = 0.6f;
    public float boostSpeed = 15f;

    [Header("Homing")]
    public float homingSpeed = 35f;
    public float turnSpeed = 120f;
    public float hitDistance = 1.5f;
    public float targetHeightOffset = 0f;

    [Header("Lifetime")]
    public float lifetime = 12f;

    [Header("Visual Model")]
    public Transform visualModel;
    public Vector3 visualRotationOffset;

    [Header("Explosion")]
    public GameObject impactExplosionPrefab;
    public float explosionLifetime = 4f;

    [Header("Impact Disappearance")]
    public float disappearDuration = 0.4f;
    public float trailCleanupTime = 5f;

    private Rigidbody rb;
    private Camera missileViewCamera;
    private Transform target;
    private Transform ownerRoot;
    private Vector3 flightDirection = Vector3.up;
    private float boostTimer;
    private bool homing;
    private bool exploded;
    private bool droneImpact;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        missileViewCamera = GetComponentInChildren<Camera>();
        if (missileViewCamera != null)
        {
            missileViewCamera.enabled = false;

            AudioListener audioListener =
                missileViewCamera.GetComponent<AudioListener>();
            if (audioListener != null)
                audioListener.enabled = false;
        }

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        if (visualModel != null)
            visualModel.localRotation =
                Quaternion.Euler(visualRotationOffset);
    }

    void Start()
    {
        if (missileViewCamera != null && GameDirector.Instance != null)
            GameDirector.Instance.RegisterMissileViewCamera(missileViewCamera);

        boostTimer = boostDuration;
        rb.velocity = Vector3.up * boostSpeed;
        UpdateRotation(Vector3.up);

        // Lifetime now triggers a smooth disappearance.
        StartCoroutine(LifetimeRoutine());
    }

    IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (!exploded)
            BeginDisappearance();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetOwner(Transform owner)
    {
        ownerRoot = owner;

        if (ownerRoot == null)
            return;

        Collider[] missileColliders =
            GetComponentsInChildren<Collider>();

        Collider[] ownerColliders =
            ownerRoot.GetComponentsInChildren<Collider>();

        foreach (Collider missileCollider in missileColliders)
        {
            foreach (Collider ownerCollider in ownerColliders)
            {
                if (missileCollider != null &&
                    ownerCollider != null)
                {
                    Physics.IgnoreCollision(
                        missileCollider, ownerCollider, true);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (exploded)
            return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!homing)
        {
            boostTimer -= Time.fixedDeltaTime;
            flightDirection = Vector3.up;
            rb.velocity = flightDirection * boostSpeed;
            UpdateRotation(flightDirection);

            if (boostTimer <= 0f)
                homing = true;

            return;
        }

        Vector3 targetPosition =
            target.position + Vector3.up * targetHeightOffset;
        Vector3 toTarget = targetPosition - rb.position;

        Vector3 actualTargetPosition =
            target.position + Vector3.up * targetHeightOffset;
        if (Vector3.SqrMagnitude(actualTargetPosition - rb.position) <=
            hitDistance * hitDistance)
        {
            AIDroneController drone =
                target.GetComponentInParent<AIDroneController>();

            if (drone != null)
            {
                DestroyDrone(drone);
                return;
            }

        }

        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(toTarget.normalized);

            Quaternion nextRotation = Quaternion.RotateTowards(
                rb.rotation,
                desiredRotation,
                turnSpeed * Time.fixedDeltaTime);

            flightDirection = nextRotation * Vector3.forward;
            flightDirection.Normalize();
        }

        UpdateRotation(flightDirection);
        rb.velocity = flightDirection * homingSpeed;
    }

    void UpdateRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 up = Mathf.Abs(
            Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;

        rb.MoveRotation(Quaternion.LookRotation(direction, up));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (exploded)
            return;

        AIDroneController drone =
            collision.collider.GetComponentInParent<AIDroneController>();

        if (drone != null)
        {
            DestroyDrone(drone);
            return;
        }

        Explode();
    }

    void DestroyDrone(AIDroneController drone)
    {
        if (exploded || drone == null)
            return;

        exploded = true;
        droneImpact = true;

        CreateExplosion();

        Destroy(drone.gameObject);

        BeginDisappearance();
    }

    void Explode()
    {
        if (exploded)
            return;

        exploded = true;

        CreateExplosion();

        BeginDisappearance();
    }

    void CreateExplosion()
    {
        if (impactExplosionPrefab == null)
            return;

        GameObject effect = Instantiate(
            impactExplosionPrefab,
            transform.position,
            Quaternion.identity);

        Destroy(effect, explosionLifetime);
    }

    void BeginDisappearance()
    {
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Prevent additional collisions.
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
            col.enabled = false;

        // Detach all particle systems before destroying the missile.
        ParticleSystem[] trails =
            GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem trail in trails)
        {
            trail.transform.SetParent(null, true);

            trail.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);

            Destroy(trail.gameObject, trailCleanupTime);
        }

        if (droneImpact)
        {
            if (missileViewCamera != null && GameDirector.Instance != null)
            {
                GameDirector.Instance.BeginMissileViewImpact(
                    missileViewCamera,
                    disappearDuration
                );
            }

            missileViewCamera = null;
            Destroy(gameObject);
            return;
        }

        StartCoroutine(DisappearSmoothly());
    }

    IEnumerator DisappearSmoothly()
    {
        float elapsed = 0f;

        Vector3 originalScale = transform.localScale;

        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / disappearDuration);

            transform.localScale =
                Vector3.Lerp(originalScale, Vector3.zero, progress);

            yield return null;
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (missileViewCamera != null && GameDirector.Instance != null)
            GameDirector.Instance.ClearMissileViewCamera(missileViewCamera);
    }
}
