
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AITankController : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    public float detectionRange = 80f;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float turnSpeed = 80f;
    public float stopDistance = 12f;

    [Header("Fixed Position and Rotation")]
    public float fixedWorldY = 0.65f;

    [Tooltip("Continuously force world X/Z rotation to zero.")]
    public bool enforceRotationAndHeight = true;

    [Header("Wall Avoidance")]
    public float obstacleCheckDistance = 5f;
    public float avoidTurnDuration = 1.2f;
    public LayerMask obstacleLayers = ~0;

    [Header("Turret")]
    public Transform turret;
    public float turretTurnSpeed = 120f;

    [Header("Weapons")]
    public GameObject shellPrefab;
    public Transform firePoint;
    public float fireRange = 40f;
    public float shellSpeed = 40f;
    public float fireCooldown = 2f;

    [Header("Imperfect AI")]
    [Range(0f, 30f)]
    public float maxAimError = 8f;

    [Range(0f, 1f)]
    public float mistakeChance = 0.35f;

    public float maxFireHesitation = 1.2f;
    public float aimChangeInterval = 1.0f;

    [Range(0f, 0.8f)]
    public float turnImprecision = 0.15f;

    [Min(0.1f)]
    public float aimUpdateInterval = 0.5f;

    private Rigidbody rb;
    private Collider tankCollider;

    private float avoidTimer;
    private float avoidDirection = 1f;
    private float nextFireTime;

    private float currentAimError;
    private float nextAimUpdateTime;
    private float currentTurnMultiplier = 1f;
    private float aimErrorUntil;
    private bool makingAimMistake;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        tankCollider = GetComponent<Collider>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        rb.constraints |= RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ
                        | RigidbodyConstraints.FreezePositionY;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");

            if (obj != null)
                player = obj.transform;
        }

        nextFireTime = Time.time + Random.Range(0.5f, 2f);
        nextAimUpdateTime = Time.time;
        EnforceFixedTransform();
    }

    void Update()
    {
        if (player == null) return;

        EnforceFixedTransform();

        float distance =
            Vector3.Distance(transform.position, player.position);

        if (distance > detectionRange) return;

        UpdateImperfectAim();
        AimTurret();

        if (distance <= fireRange &&
            HasClearLineOfSight() &&
            Time.time >= nextFireTime)
        {
            Fire();

            nextFireTime = Time.time
                + fireCooldown
                + Random.Range(0f, maxFireHesitation);
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        EnforceFixedTransform();

        if (player == null) return;

        Vector3 toPlayer = player.position - rb.position;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;

        if (distance > detectionRange) return;

        bool frontBlocked = IsBlocked(0f);
        bool leftBlocked = IsBlocked(-35f);
        bool rightBlocked = IsBlocked(35f);

        if (frontBlocked && avoidTimer <= 0f)
        {
            if (!leftBlocked && rightBlocked)
                avoidDirection = -1f;
            else if (!rightBlocked && leftBlocked)
                avoidDirection = 1f;
            else
            {
                float playerSide =
                    Vector3.Cross(transform.forward, toPlayer).y;

                avoidDirection = playerSide >= 0f ? -1f : 1f;
            }

            if (Random.value < 0.15f)
                avoidDirection *= -1f;

            avoidTimer = avoidTurnDuration
                * Random.Range(0.8f, 1.3f);
        }

        if (avoidTimer > 0f)
        {
            TurnTank(avoidDirection);
            avoidTimer -= Time.fixedDeltaTime;
            EnforceFixedTransform();
            return;
        }

        if (toPlayer.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(toPlayer);

            float adjustedTurnSpeed =
                turnSpeed * currentTurnMultiplier;

            Quaternion nextRotation = Quaternion.RotateTowards(
                rb.rotation,
                targetRotation,
                adjustedTurnSpeed * Time.fixedDeltaTime
            );

            // Preserve only the desired world Y rotation.
            rb.MoveRotation(Quaternion.Euler(
                0f,
                nextRotation.eulerAngles.y,
                0f
            ));
        }

        bool hesitate = Random.value < 0.01f;

        if (distance > stopDistance &&
            !frontBlocked &&
            !hesitate)
        {
            Vector3 movement =
                rb.rotation * Vector3.forward
                * moveSpeed * Time.fixedDeltaTime;

            Vector3 direction = movement.normalized;

            if (!IsObstacleAhead(direction, movement.magnitude))
            {
                rb.MovePosition(new Vector3(
                    rb.position.x + movement.x,
                    fixedWorldY,
                    rb.position.z + movement.z
                ));
            }
        }

        EnforceFixedTransform();
    }

    void LateUpdate()
    {
        EnforceFixedTransform();
    }

    void EnforceFixedTransform()
    {
        if (!enforceRotationAndHeight || rb == null)
            return;

        Vector3 position = rb.position;
        position.y = fixedWorldY;
        rb.position = position;

        float yRotation = rb.rotation.eulerAngles.y;

        rb.rotation = Quaternion.Euler(
            0f,
            yRotation,
            0f
        );
    }

    void UpdateImperfectAim()
    {
        if (Time.time < nextAimUpdateTime) return;

        nextAimUpdateTime = Time.time + aimUpdateInterval;

        currentTurnMultiplier = Random.Range(
            1f - turnImprecision,
            1f + turnImprecision
        );

        makingAimMistake = Random.value < mistakeChance;

        currentAimError = makingAimMistake
            ? Random.Range(-maxAimError, maxAimError)
            : 0f;

        aimErrorUntil = Time.time + aimChangeInterval;
    }

    bool IsBlocked(float angle)
    {
        Vector3 direction =
            Quaternion.AngleAxis(angle, Vector3.up)
            * transform.forward;

        Vector3 origin =
            transform.position + Vector3.up * 0.5f
            + direction * 1.2f;

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            obstacleCheckDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform.root != transform.root;
        }

        return false;
    }

    bool IsObstacleAhead(Vector3 direction, float moveDistance)
    {
        if (tankCollider == null || moveDistance <= 0f)
            return false;

        Bounds bounds = tankCollider.bounds;

        Vector3 origin = bounds.center;
        origin.y = bounds.min.y + 0.5f;

        float checkDistance =
            Mathf.Max(obstacleCheckDistance, moveDistance + 0.1f);

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            checkDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform.root != transform.root;
        }

        return false;
    }

    bool HasClearLineOfSight()
    {
        if (player == null || firePoint == null)
            return false;

        Vector3 direction = player.position - firePoint.position;
        float distance = direction.magnitude;

        if (distance <= 0.01f) return true;

        if (Physics.Raycast(
            firePoint.position,
            direction.normalized,
            out RaycastHit hit,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform == player ||
                   hit.transform.IsChildOf(player);
        }

        return false;
    }

    void TurnTank(float direction)
    {
        float turnAmount =
            direction * turnSpeed * currentTurnMultiplier
            * Time.fixedDeltaTime;

        float nextY =
            rb.rotation.eulerAngles.y + turnAmount;

        rb.MoveRotation(Quaternion.Euler(0f, nextY, 0f));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (player == null) return;

        if (collision.transform.root != player.root)
        {
            Vector3 toPlayer =
                player.position - transform.position;

            toPlayer.y = 0f;

            float playerSide =
                Vector3.Cross(transform.forward, toPlayer).y;

            avoidDirection = playerSide >= 0f ? -1f : 1f;
            avoidTimer = avoidTurnDuration;
        }
    }

    void AimTurret()
    {
        if (turret == null || player == null) return;

        Vector3 direction = player.position - turret.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        if (makingAimMistake && Time.time < aimErrorUntil)
        {
            targetRotation *= Quaternion.Euler(
                0f, currentAimError, 0f
            );
        }

        turret.rotation = Quaternion.RotateTowards(
            turret.rotation,
            targetRotation,
            turretTurnSpeed * Time.deltaTime
        );
    }

    void Fire()
    {
        if (shellPrefab == null || firePoint == null)
            return;

        GameObject shell = Instantiate(
            shellPrefab,
            firePoint.position,
            firePoint.rotation
        );

        Collider[] shellColliders =
            shell.GetComponentsInChildren<Collider>();

        Collider[] tankColliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider shellCollider in shellColliders)
        {
            foreach (Collider tankCol in tankColliders)
            {
                if (shellCollider != null && tankCol != null)
                {
                    Physics.IgnoreCollision(shellCollider, tankCol);
                }
            }
        }

        Rigidbody shellRb = shell.GetComponent<Rigidbody>();

        if (shellRb != null)
        {
            shellRb.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            shellRb.velocity = firePoint.forward * shellSpeed;
        }
        else
        {
            Debug.LogWarning("Shell prefab needs a Rigidbody!");
            Destroy(shell);
        }
    }
}
