
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
    private NetworkTankPlayer networkTargetPlayer;
    private Transform ownerRoot;
    private NetworkTankPlayer ownerNetworkPlayer;
    private Vector3 flightDirection = Vector3.up;
    private Vector2 networkAimErrorDegrees;
    private float networkAimErrorDistance;
    private float networkHitDistance;
    private float boostTimer;
    private bool homing;
    private bool exploded;
    private bool droneImpact;
    private bool networkedMissile;
    private bool allowMissileCamera = true;

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
        if (missileViewCamera != null && GameDirector.Instance != null &&
            (!networkedMissile || allowMissileCamera))
            GameDirector.Instance.RegisterMissileViewCamera(missileViewCamera);

        if (networkedMissile && target != null)
        {
            boostTimer = 0f;
            homing = true;
            flightDirection = (target.position - rb.position).normalized;
            if (flightDirection.sqrMagnitude < 0.001f)
                flightDirection = transform.forward;
            rb.velocity = flightDirection * homingSpeed;
            UpdateRotation(flightDirection);
        }
        else
        {
            boostTimer = boostDuration;
            rb.velocity = Vector3.up * boostSpeed;
            UpdateRotation(Vector3.up);
        }

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

        ownerNetworkPlayer =
            ownerRoot.GetComponent<NetworkTankPlayer>();
        if (ownerNetworkPlayer == null)
            ownerNetworkPlayer =
                ownerRoot.GetComponentInParent<NetworkTankPlayer>();

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

    public void SetNetworkTarget(
        Transform newTarget,
        NetworkTankPlayer targetPlayer,
        bool permitMissileCamera,
        Vector2 aimErrorDegrees,
        float targetHitDistance)
    {
        target = newTarget;
        networkTargetPlayer = targetPlayer;
        networkedMissile = true;
        allowMissileCamera = permitMissileCamera;
        networkAimErrorDegrees = aimErrorDegrees;
        networkAimErrorDistance = target != null
            ? Vector3.Distance(transform.position, target.position)
            : 0f;
        networkHitDistance = Mathf.Max(0.1f, targetHitDistance);
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
        if (networkedMissile)
        {
            float horizontalError =
                Mathf.Tan(networkAimErrorDegrees.x * Mathf.Deg2Rad) *
                networkAimErrorDistance;
            float verticalError =
                Mathf.Tan(networkAimErrorDegrees.y * Mathf.Deg2Rad) *
                networkAimErrorDistance;
            targetPosition += target.right * horizontalError +
                              target.up * verticalError;
        }

        Vector3 toTarget = targetPosition - rb.position;

        float impactDistance = networkedMissile
            ? networkHitDistance
            : hitDistance;
        Vector3 actualTargetPosition =
            target.position + Vector3.up * targetHeightOffset;
        if (Vector3.SqrMagnitude(actualTargetPosition - rb.position) <=
            impactDistance * impactDistance)
        {
            AIDroneController drone =
                target.GetComponentInParent<AIDroneController>();

            if (drone != null)
            {
                DestroyDrone(drone);
                return;
            }

            if (TryHitNetworkDrone())
                return;
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

        if (networkedMissile &&
            TryHitNetworkDrone(collision.collider.transform))
            return;

        Explode();
    }

    private bool TryHitNetworkDrone(Transform hitTransform = null)
    {
        if (!networkedMissile || exploded || networkTargetPlayer == null)
            return false;

        TankDronecontroll droneController =
            networkTargetPlayer.GetComponent<TankDronecontroll>();
        Transform targetDrone = droneController != null
            ? droneController.droneTransform
            : null;
        if (targetDrone == null ||
            (hitTransform != null && hitTransform != targetDrone &&
             !hitTransform.IsChildOf(targetDrone)))
            return false;

        exploded = true;
        droneImpact = true;
        CreateExplosion();

        if (ownerNetworkPlayer != null && ownerNetworkPlayer.isOwned)
        {
            ownerNetworkPlayer.RequestEnemyDroneMissileHit(
                networkTargetPlayer,
                transform.position
            );
        }

        BeginDisappearance();
        return true;
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
            if (allowMissileCamera && missileViewCamera != null &&
                GameDirector.Instance != null)
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
