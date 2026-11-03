
using UnityEngine;

public class TankWheelRotation : MonoBehaviour
{
    [System.Serializable]
    public class Wheel
    {
        public Transform wheel;
        public Vector3 localRotationAxis = Vector3.right;
        public float direction = 1f;
    }

    [Header("Tank Movement")]
    public TankMovement tankMovement;

    [Header("Wheels")]
    public Wheel[] wheels;

    [Header("Settings")]
    [Tooltip("Wheel radius in meters.")]
    public float wheelRadius = 0.35f;

    [Tooltip("Reverse this if the wheels rotate backward.")]
    public float rotationMultiplier = 1f;

    private Quaternion[] initialRotations;
    private NetworkTankPlayer networkTankPlayer;

    void Start()
    {
        if (tankMovement == null)
            tankMovement = GetComponentInParent<TankMovement>();

        networkTankPlayer =
            GetComponentInParent<NetworkTankPlayer>();

        if (tankMovement == null)
        {
            Debug.LogError(
                "TankWheelRotation: Assign TankMovement!"
            );
            enabled = false;
            return;
        }

        initialRotations = new Quaternion[wheels.Length];

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i].wheel != null)
                initialRotations[i] = wheels[i].wheel.localRotation;
        }
    }

    void Update()
    {
        // Signed distance traveled, in meters.
        float speed = networkTankPlayer != null
            ? networkTankPlayer.DisplayedTankSpeed
            : tankMovement.currentSpeed;
        float distance = speed * Time.deltaTime;

        // Convert distance into wheel rotation in degrees.
        float rotationDegrees =
            distance / Mathf.Max(wheelRadius, 0.01f)
            * Mathf.Rad2Deg
            * rotationMultiplier;

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i].wheel == null)
                continue;

            Vector3 axis = wheels[i].localRotationAxis.normalized;

            wheels[i].wheel.localRotation =
                initialRotations[i] *
                Quaternion.AngleAxis(
                    rotationDegrees * wheels[i].direction,
                    axis
                );
        }
    }
}
