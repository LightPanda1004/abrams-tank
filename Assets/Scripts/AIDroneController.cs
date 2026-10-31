
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AIDroneController : MonoBehaviour
{
    public enum AttackPointType
    {
        TurretTop,
        TankBody
    }

    [Header("Target")]
    public Transform tank;
    public Transform turretTopTarget;
    public Transform tankBodyTarget;
    public AttackPointType attackPoint = AttackPointType.TurretTop;

    [Header("Detection")]
    public float detectionRange = 60f;
    public float detectionHeightDifference = 30f;
    public bool showDetectionRange = true;

    [Header("Patrol")]
    [Range(10f, 200f)]
    public float patrolRadius = 200f;
    public float cruiseHeight = 12f;
    public float patrolSpeed = 8f;
    public float patrolPointReachDistance = 5f;
    public float patrolHeightVariation = 5f;
    public float patrolPointWaitTime = 1f;

    [Header("Attack")]
    public float attackSpeed = 14f;
    public float diveStartDistance = 12f;
    public float impactDistance = 1.5f;
    public float impactDamage = 50f;
    public float turnSpeed = 150f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleMask;
    public float obstacleDetectionDistance = 8f;
    public float avoidanceStrength = 1.5f;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 4f;

    [Header("Drone Visual")]
    public Transform visualModel;
    public float maxBankAngle = 25f;
    public float tiltSmooth = 4f;

    [Header("Propellers")]
    public Transform[] propellers;
    public float propellerRPM = 1800f;
    public Vector3 propellerLocalAxis = Vector3.up;

    private Rigidbody rb;
    private Vector3 patrolPoint;
    private float waitTimer;
    private bool playerDetected;
    private bool diving;
    private bool exploded;

    private TankHealth playerHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (tank == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                tank = player.transform;
        }

        if (tank != null)
            playerHealth = tank.GetComponentInParent<TankHealth>();

        ChooseNewPatrolPoint();
    }

    private void Update()
    {
        if (exploded)
            return;

        // Spin each propeller around its own local shaft.
        if (propellers != null)
        {
            foreach (Transform propeller in propellers)
            {
                if (propeller != null)
                {
                    propeller.Rotate(
                        propellerLocalAxis.normalized,
                        propellerRPM * 6f * Time.deltaTime,
                        Space.Self
                    );
                }
            }
        }

        if (tank == null)
            return;

        // Once detected, the drone stays in attack mode.
        if (!playerDetected && CanDetectPlayer())
        {
            playerDetected = true;
            Debug.Log(name + " detected the player tank!");
        }

        if (playerDetected)
        {
            Transform target = GetAttackTarget();

            if (target != null)
            {
                float distance = Vector3.Distance(transform.position, target.position);

                if (distance <= diveStartDistance)
                    diving = true;
            }
        }

        UpdateVisualTilt();
    }

    private void FixedUpdate()
    {
        if (exploded || tank == null)
            return;

        if (!playerDetected)
        {
            Patrol();
        }
        else
        {
            AttackPlayer();
        }
    }

    private bool CanDetectPlayer()
    {
        if (tank == null)
            return false;

        Vector3 difference = tank.position - transform.position;

        // Detection is based on distance and height difference.
        // This avoids detecting targets far above or below the drone.
        if (Mathf.Abs(difference.y) > detectionHeightDifference)
            return false;

        return difference.magnitude <= detectionRange;
    }

    private void Patrol()
    {
        waitTimer -= Time.fixedDeltaTime;

        Vector3 destination = patrolPoint;
        destination.y = patrolPoint.y;

        MoveDrone(destination, patrolSpeed, true);

        if (Vector3.Distance(transform.position, patrolPoint)
            <= patrolPointReachDistance && waitTimer <= 0f)
        {
            waitTimer = patrolPointWaitTime;
            ChooseNewPatrolPoint();
        }
    }

    private void ChooseNewPatrolPoint()
    {
        if (tank == null)
        {
            patrolPoint = transform.position + transform.forward * 30f;
            return;
        }

        // Pick a random horizontal point around the tank.
        // patrolRadius is capped at 200 in the Inspector.
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;

        patrolPoint = new Vector3(
            tank.position.x + randomCircle.x,
            cruiseHeight + Random.Range(
                -patrolHeightVariation, patrolHeightVariation),
            tank.position.z + randomCircle.y
        );

        waitTimer = patrolPointWaitTime;
    }

    private void AttackPlayer()
    {
        Transform target = GetAttackTarget();

        if (target == null)
            return;

        Vector3 destination = target.position;

        if (diving)
        {
            // Direct final dive toward the assigned target.
            MoveDrone(destination, attackSpeed, false);
        }
        else
        {
            // Approach the tank while maintaining cruise altitude.
            destination.y = cruiseHeight;
            MoveDrone(destination, attackSpeed, true);
        }
    }

    private Transform GetAttackTarget()
    {
        if (attackPoint == AttackPointType.TurretTop &&
            turretTopTarget != null)
            return turretTopTarget;

        if (attackPoint == AttackPointType.TankBody &&
            tankBodyTarget != null)
            return tankBodyTarget;

        return tank;
    }

    private void MoveDrone(Vector3 destination, float speed, bool avoidObstacles)
    {
        Vector3 direction = destination - transform.position;

        if (direction.sqrMagnitude < 0.01f)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        direction.Normalize();

        if (avoidObstacles && obstacleDetectionDistance > 0f)
        {
            Vector3 origin = transform.position;
            if (Physics.SphereCast(
                origin,
                1f,
                direction,
                out RaycastHit hit,
                obstacleDetectionDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 avoidance =
                    Vector3.ProjectOnPlane(hit.normal, Vector3.up);

                if (avoidance.sqrMagnitude > 0.01f)
                {
                    direction = (direction +
                        avoidance.normalized * avoidanceStrength).normalized;
                }
            }
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            direction, Vector3.up);

        Quaternion rotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(rotation);

        Vector3 nextPosition = Vector3.MoveTowards(
            rb.position,
            destination,
            speed * Time.fixedDeltaTime);

        rb.MovePosition(nextPosition);
    }

    private void UpdateVisualTilt()
    {
        if (visualModel == null)
            return;

        float bank = 0f;

        if (rb != null)
        {
            Vector3 localVelocity =
                transform.InverseTransformDirection(rb.velocity);

            bank = -localVelocity.x * 3f;
            bank = Mathf.Clamp(bank, -maxBankAngle, maxBankAngle);
        }

        Quaternion targetTilt = Quaternion.Euler(0f, 0f, bank);

        visualModel.localRotation = Quaternion.Slerp(
            visualModel.localRotation,
            targetTilt,
            tiltSmooth * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded || !playerDetected)
            return;

        TankHealth hitHealth =
            collision.collider.GetComponentInParent<TankHealth>();

        if (hitHealth != null && hitHealth == playerHealth)
        {
            Explode(hitHealth);
        }
    }

    private void Explode(TankHealth hitHealth)
    {
        if (exploded)
            return;

        exploded = true;

        if (hitHealth != null)
            hitHealth.TakeDamage(impactDamage);

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                transform.position,
                Quaternion.identity);

            Destroy(explosion, explosionLifetime);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (showDetectionRange)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
