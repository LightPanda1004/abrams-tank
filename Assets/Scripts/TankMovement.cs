
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

    [Header("Speed Boost")]
    public KeyCode speedBoostKey = KeyCode.LeftShift;
    public float speedBoostAmount = 10f;
    public float speedBoostDuration = 5f;
    public float speedBoostRampDuration = 1.5f;
    public float speedBoostReturnDuration = 2f;

    [Header("Keep Tank Level")]
    public float fixedWorldY = 0f;

    [Header("Current Speed")]
    public float currentSpeed = 0f;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;
    private bool speedBoostActive;
    private bool speedBoostReturning;
    private float speedBoostElapsed;
    private float speedBoostReturnElapsed;
    private float speedBoostReturnStartAmount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        rb.constraints |= RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ
                        | RigidbodyConstraints.FreezePositionY;

        Vector3 position = rb.position;
        position.y = fixedWorldY;
        rb.position = position;
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        bool boostPressed = Input.GetKeyDown(speedBoostKey) ||
            (speedBoostKey == KeyCode.LeftShift &&
             Input.GetKeyDown(KeyCode.RightShift));
        GameDirector gameDirector = GameDirector.Instance;
        bool canControlTank = gameDirector == null ||
            (!gameDirector.IsPaused && !gameDirector.IsGameOver);

        if (boostPressed && canControlTank && !speedBoostActive &&
            Mathf.Abs(moveInput) > 0.01f)
        {
            speedBoostActive = true;
            speedBoostReturning = false;
            speedBoostElapsed = 0f;
            speedBoostReturnElapsed = 0f;
        }

        if (speedBoostActive)
        {
            if (!speedBoostReturning)
            {
                if (!IsSpeedBoostKeyHeld() ||
                    speedBoostElapsed >= Mathf.Max(0f, speedBoostDuration))
                {
                    BeginSpeedBoostReturn();
                }
                else
                {
                    speedBoostElapsed += Time.deltaTime;
                    if (speedBoostElapsed >=
                        Mathf.Max(0f, speedBoostDuration))
                    {
                        BeginSpeedBoostReturn();
                    }
                }
            }
            else
            {
                speedBoostReturnElapsed += Time.deltaTime;
            }
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        float targetSpeed = moveInput >= 0f
            ? moveInput * maxSpeed
            : moveInput * reverseSpeed;
        float normalTargetSpeed = targetSpeed;

        float boostAmount = GetSpeedBoostAmount();
        if (boostAmount > 0f && Mathf.Abs(moveInput) > 0.01f)
        {
            float boostDirection = Mathf.Sign(moveInput);
            targetSpeed += boostDirection * boostAmount;
        }

        bool accelerating =
            Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) &&
            (Mathf.Sign(targetSpeed) == Mathf.Sign(currentSpeed) ||
             Mathf.Abs(currentSpeed) < 0.01f);

        float rate = accelerating ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            rate * Time.fixedDeltaTime
        );
        AdvanceSpeedBoost(normalTargetSpeed);

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

    public float GetSpeed01()
    {
        float highestSpeed = Mathf.Max(maxSpeed, reverseSpeed);

        if (highestSpeed <= 0f)
            return 0f;

        return Mathf.Clamp01(
            Mathf.Abs(currentSpeed) / highestSpeed
        );
    }

    private float GetSpeedBoostAmount()
    {
        if (!speedBoostActive)
            return 0f;

        if (speedBoostReturning)
        {
            float returnDuration =
                Mathf.Max(0.01f, speedBoostReturnDuration);
            float returnProgress = Mathf.Clamp01(
                speedBoostReturnElapsed / returnDuration
            );
            return speedBoostReturnStartAmount *
                   (1f - Mathf.SmoothStep(0f, 1f, returnProgress));
        }

        float rampDuration = Mathf.Max(0.01f, speedBoostRampDuration);
        if (speedBoostElapsed < rampDuration)
        {
            float ramp = Mathf.Clamp01(speedBoostElapsed / rampDuration);
            return speedBoostAmount * Mathf.SmoothStep(0f, 1f, ramp);
        }

        return speedBoostAmount;
    }

    private void AdvanceSpeedBoost(float normalTargetSpeed)
    {
        if (!speedBoostActive || !speedBoostReturning)
            return;

        if (speedBoostReturnElapsed >=
                Mathf.Max(0.01f, speedBoostReturnDuration) &&
            Mathf.Abs(currentSpeed - normalTargetSpeed) <= 0.1f)
        {
            speedBoostActive = false;
            speedBoostReturning = false;
            speedBoostElapsed = 0f;
            speedBoostReturnElapsed = 0f;
            speedBoostReturnStartAmount = 0f;
        }
    }

    private bool IsSpeedBoostKeyHeld()
    {
        return Input.GetKey(speedBoostKey) ||
               (speedBoostKey == KeyCode.LeftShift &&
                Input.GetKey(KeyCode.RightShift));
    }

    private void BeginSpeedBoostReturn()
    {
        if (speedBoostReturning)
            return;

        speedBoostReturnStartAmount = GetSpeedBoostAmount();
        speedBoostReturning = true;
        speedBoostReturnElapsed = 0f;
    }
}
