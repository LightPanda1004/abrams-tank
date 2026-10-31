
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TankMovement : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 6f;
    public float reverseSpeed = 3f;
    public float acceleration = 2f;
    public float deceleration = 3f;
    public float turnSpeed = 45f;

    [Header("Keep Tank Level")]
    public float fixedWorldY = 0.65f;

    [Header("Current Speed")]
    public float currentSpeed = 0f;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        rb.constraints |= RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ
                        | RigidbodyConstraints.FreezePositionY;

        EnforcePositionAndRotation();
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        float targetSpeed = moveInput >= 0f
            ? moveInput * maxSpeed
            : moveInput * reverseSpeed;

        bool accelerating =
            Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) &&
            (Mathf.Sign(targetSpeed) == Mathf.Sign(currentSpeed) ||
             Mathf.Abs(currentSpeed) < 0.01f);

        float rate = accelerating ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            rate * Time.deltaTime
        );
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        EnforcePositionAndRotation();

        float direction = currentSpeed < -0.01f ? -1f : 1f;

        float turnAmount =
            turnInput * turnSpeed * direction * Time.fixedDeltaTime;

        float targetYRotation =
            rb.rotation.eulerAngles.y + turnAmount;

        Quaternion targetRotation =
            Quaternion.Euler(0f, targetYRotation, 0f);

        rb.MoveRotation(targetRotation);

        Vector3 movement =
            targetRotation * Vector3.forward *
            currentSpeed * Time.fixedDeltaTime;

        rb.MovePosition(new Vector3(
            rb.position.x + movement.x,
            fixedWorldY,
            rb.position.z + movement.z
        ));
    }

    private void LateUpdate()
    {
        EnforcePositionAndRotation();
    }

    private void EnforcePositionAndRotation()
    {
        if (rb == null) return;

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

    public float GetSpeed01()
    {
        float highestSpeed = Mathf.Max(maxSpeed, reverseSpeed);

        if (highestSpeed <= 0f)
            return 0f;

        return Mathf.Clamp01(
            Mathf.Abs(currentSpeed) / highestSpeed
        );
    }
}
