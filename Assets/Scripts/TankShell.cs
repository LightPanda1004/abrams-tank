
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankShell : MonoBehaviour
{
    [Header("Shell Settings")]
    public float damage = 25f;
    public float lifetime = 5f;

    [Header("Impact Effect")]
    public GameObject explosionPrefab;
    public float explosionDestroyDelay = 5f;
    public GameObject concreteImpactPrefab;
    public float concreteImpactDestroyDelay = 4f;
    public float blastRadius = 5f;
    public float blastForce = 250f;
    public float blastUpwardsModifier = 0.5f;

    [Header("Disappear Settings")]
    public float disappearDuration = 0.4f;
    public float trailCleanupTime = 5f;

    private Transform ownerRoot;
    private Rigidbody rb;
    private Collider shellCollider;
    private bool hasExploded;
    private Vector3 previousPosition;
    private readonly RaycastHit[] sweepHits = new RaycastHit[64];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        shellCollider = GetComponent<Collider>();
        previousPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (hasExploded || rb == null)
            return;

        Vector3 currentPosition = rb.position;
        Vector3 travel = currentPosition - previousPosition;
        float travelDistance = travel.magnitude;
        if (travelDistance > 0.001f)
        {
            Vector3 colliderOffset = shellCollider != null
                ? shellCollider.bounds.center - rb.position
                : Vector3.zero;
            Vector3 sweepStart = previousPosition + colliderOffset;
            Vector3 travelDirection = travel / travelDistance;
            int hitCount = CastShell(
                sweepStart,
                travelDirection,
                travelDistance
            );

            RaycastHit nearestHit = default;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = sweepHits[i];
                if (hit.collider == null ||
                    hit.collider.transform == transform ||
                    hit.collider.transform.IsChildOf(transform) ||
                    ShouldIgnoreCollider(hit.collider) ||
                    (foundHit && hit.distance >= nearestHit.distance))
                {
                    continue;
                }

                nearestHit = hit;
                foundHit = true;
            }

            if (foundHit)
                OnTriggerEnter(nearestHit.collider);
        }

        if (!hasExploded)
            previousPosition = currentPosition;
    }

    private int CastShell(
        Vector3 startCenter,
        Vector3 travelDirection,
        float travelDistance)
    {
        if (shellCollider is CapsuleCollider capsuleCollider)
        {
            Vector3 scale = capsuleCollider.transform.lossyScale;
            Vector3 localAxis = capsuleCollider.direction == 0
                ? Vector3.right
                : capsuleCollider.direction == 1
                    ? Vector3.up
                    : Vector3.forward;
            Vector3 axis = capsuleCollider.transform.TransformDirection(
                localAxis
            ).normalized;
            float axisScale = capsuleCollider.direction == 0
                ? Mathf.Abs(scale.x)
                : capsuleCollider.direction == 1
                    ? Mathf.Abs(scale.y)
                    : Mathf.Abs(scale.z);
            float radius = GetShellSweepRadius(travelDirection);
            float halfSegment = Mathf.Max(
                0f,
                capsuleCollider.height * axisScale * 0.5f - radius
            );

            return Physics.CapsuleCastNonAlloc(
                startCenter - axis * halfSegment,
                startCenter + axis * halfSegment,
                radius,
                travelDirection,
                sweepHits,
                travelDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide
            );
        }

        return Physics.SphereCastNonAlloc(
            startCenter,
            GetShellSweepRadius(travelDirection),
            travelDirection,
            sweepHits,
            travelDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide
        );
    }

    private float GetShellSweepRadius(Vector3 travelDirection)
    {
        if (shellCollider is SphereCollider sphereCollider)
        {
            Vector3 scale = sphereCollider.transform.lossyScale;
            return sphereCollider.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z)
            );
        }

        if (shellCollider is CapsuleCollider capsuleCollider)
        {
            Vector3 scale = capsuleCollider.transform.lossyScale;
            float perpendicularScale = capsuleCollider.direction == 0
                ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                : capsuleCollider.direction == 1
                    ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))
                    : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            return capsuleCollider.radius * perpendicularScale;
        }

        if (shellCollider != null)
        {
            Vector3 extents = shellCollider.bounds.extents;
            float alongTravel = Mathf.Abs(travelDirection.x) * extents.x +
                               Mathf.Abs(travelDirection.y) * extents.y +
                               Mathf.Abs(travelDirection.z) * extents.z;
            return Mathf.Sqrt(Mathf.Max(
                0f,
                extents.sqrMagnitude - alongTravel * alongTravel
            ));
        }

        return 0f;
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

        if (ShouldIgnoreCollider(other))
            return;

        TankHealth health = other.GetComponentInParent<TankHealth>();
        TankDroneImpact droneImpact =
            other.GetComponentInParent<TankDroneImpact>();

        if (droneImpact != null && !droneImpact.IsDeployed)
            return;

        hasExploded = true;

        Vector3 hitPosition = GetImpactPoint(other, out Vector3 hitNormal);
        bool hitStaticMesh =
            other is MeshCollider &&
            other.attachedRigidbody == null &&
            !other.isTrigger;

        ApplyBlastForce(hitPosition);

        if (droneImpact != null)
            droneImpact.DestroyFromTankShell(hitPosition);

        // Apply damage to a tank.
        if (health != null)
            health.TakeDamage(damage, ownerRoot);

        PlayImpactEffects(hitPosition, hitNormal, hitStaticMesh);
    }

    private void PlayImpactEffects(
        Vector3 hitPosition,
        Vector3 hitNormal,
        bool hitStaticMesh)
    {
        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionPrefab,
                hitPosition,
                explosionPrefab.transform.rotation
            );

            Destroy(effect, explosionDestroyDelay);
        }

        if (hitStaticMesh && concreteImpactPrefab != null)
        {
            Vector3 surfacePosition =
                hitPosition + hitNormal * 0.02f;
            Quaternion surfaceRotation =
                Quaternion.LookRotation(hitNormal) *
                concreteImpactPrefab.transform.rotation;
            GameObject impact = Instantiate(
                concreteImpactPrefab,
                surfacePosition,
                surfaceRotation
            );

            Destroy(impact, concreteImpactDestroyDelay);
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

        Vector3 closestPoint = other.bounds.ClosestPoint(currentPosition);
        hitNormal = direction.sqrMagnitude > 0.001f
            ? -direction
            : Vector3.up;

        return closestPoint;
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (ownerRoot != null && other.transform.root == ownerRoot)
            return true;

        return false;
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
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

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
