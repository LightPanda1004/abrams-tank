
using UnityEngine;

public class TankRotorAnimation : MonoBehaviour
{
    [Header("Tank Movement")]
    public TankMovement tankMovement;

    [Header("Rotor Settings")]
    public float rotorRadius = 0.3f;
    public float rotationMultiplier = 1f;

    [Tooltip("Local axle direction. Try X, Y, or Z.")]
    public Vector3 rotationAxis = Vector3.right;

    [Tooltip("Use -1 if the rotor spins backward.")]
    public float rotationDirection = 1f;

    private NetworkTankPlayer networkTankPlayer;

    void Start()
    {
        if (tankMovement == null)
            tankMovement = GetComponentInParent<TankMovement>();

        networkTankPlayer =
            GetComponentInParent<NetworkTankPlayer>();

        if (tankMovement == null)
        {
            Debug.LogError("TankMovement not assigned!");
            enabled = false;
        }

        if (rotorRadius <= 0f)
            rotorRadius = 0.3f;
    }

    void Update()
    {
        if (tankMovement == null) return;

        float speed = networkTankPlayer != null
            ? networkTankPlayer.DisplayedTankSpeed
            : tankMovement.currentSpeed;

        float degreesPerSecond =
            (speed / rotorRadius) * Mathf.Rad2Deg;

        transform.Rotate(
            rotationAxis.normalized,
            degreesPerSecond * rotationDirection *
            rotationMultiplier * Time.deltaTime,
            Space.Self
        );
    }
}
