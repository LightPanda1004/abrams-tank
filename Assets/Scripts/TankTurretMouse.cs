
using UnityEngine;

public class TankTurretMove : MonoBehaviour
{
    [Header("Tank Parts")]
    public Transform turret;
    public Transform cannon;

    [Header("Keyboard Rotation")]
    public float turretTurnSpeed = 60f;
    public float cannonTurnSpeed = 20f;

    [Header("Mouse Rotation")]
    [Tooltip("Degrees of turret rotation per mouse pixel.")]
    public float mouseSensitivityX = 0.12f;

    [Tooltip("Degrees of cannon rotation per mouse pixel.")]
    public float mouseSensitivityY = 0.10f;

    public bool invertMouseY = false;

    [Header("Cannon Limits")]
    public float minCannonAngle = -5f;
    public float maxCannonAngle = 20f;

    [Header("Keys")]
    public KeyCode turnLeftKey = KeyCode.J;
    public KeyCode turnRightKey = KeyCode.L;
    public KeyCode cannonDownKey = KeyCode.I;
    public KeyCode cannonUpKey = KeyCode.K;

    private float cannonAngle;

    private void Start()
    {
        if (turret == null)
            Debug.LogError("TankTurretMove: Assign the Turret!");

        if (cannon == null)
            Debug.LogError("TankTurretMove: Assign the Cannon!");

        if (cannon != null)
        {
            cannonAngle = cannon.localEulerAngles.x;

            if (cannonAngle > 180f)
                cannonAngle -= 360f;

            cannonAngle = Mathf.Clamp(
                cannonAngle,
                minCannonAngle,
                maxCannonAngle
            );
        }
    }

    private void Update()
    {
        if (GameDirector.Instance != null &&
            GameDirector.Instance.IsPaused)
            return;

        bool useMouse = GameDirector.Instance == null ||
            GameDirector.Instance.MouseTurretControlEnabled;

        if (useMouse)
            MouseControl();
        else
            KeyboardControl();
    }

    private void KeyboardControl()
    {
        if (turret != null)
        {
            float turn = 0f;

            if (Input.GetKey(turnLeftKey))
                turn -= 1f;

            if (Input.GetKey(turnRightKey))
                turn += 1f;

            turret.Rotate(
                Vector3.up,
                turn * turretTurnSpeed * Time.deltaTime,
                Space.Self
            );
        }

        float elevation = 0f;

        if (Input.GetKey(cannonUpKey))
            elevation += 1f;

        if (Input.GetKey(cannonDownKey))
            elevation -= 1f;

        ChangeCannonAngle(
            elevation * cannonTurnSpeed * Time.deltaTime
        );
    }

    private void MouseControl()
    {
        if (turret != null)
        {
            float mouseX = Input.GetAxisRaw("Mouse X");

            turret.Rotate(
                Vector3.up,
                mouseX * mouseSensitivityX,
                Space.Self
            );
        }

        float mouseY = Input.GetAxisRaw("Mouse Y");

        if (invertMouseY)
            mouseY = -mouseY;

        // Mouse up raises the cannon by default.
        ChangeCannonAngle(-mouseY * mouseSensitivityY);
    }

    private void ChangeCannonAngle(float amount)
    {
        if (cannon == null)
            return;

        cannonAngle = Mathf.Clamp(
            cannonAngle + amount,
            minCannonAngle,
            maxCannonAngle
        );

        Vector3 angles = cannon.localEulerAngles;
        angles.x = cannonAngle;
        cannon.localEulerAngles = angles;
    }
}
