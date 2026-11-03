
using UnityEngine;

public class TankTrackAnimation : MonoBehaviour
{
    [Header("Tank Movement")]
    public TankMovement tankMovement;

    [Header("Track Objects")]
    public Transform leftTrack;
    public Transform rightTrack;

    [Header("Animation")]
    public float rotationSpeed = 360f;

    private NetworkTankPlayer networkTankPlayer;

    private void Start()
    {
        if (tankMovement == null)
            tankMovement = GetComponentInParent<TankMovement>();

        networkTankPlayer =
            GetComponentInParent<NetworkTankPlayer>();
    }

    void Update()
    {
        float speed = networkTankPlayer != null
            ? networkTankPlayer.DisplayedTankSpeed
            : tankMovement != null
                ? tankMovement.currentSpeed
                : 0f;
        float move = tankMovement != null &&
                     tankMovement.maxSpeed > 0f
            ? speed / tankMovement.maxSpeed
            : 0f;

        // Rotate the tracks while moving forward or backward.
        float rotation = move * rotationSpeed * Time.deltaTime;

        if (leftTrack != null)
            leftTrack.Rotate(Vector3.right, rotation, Space.Self);

        if (rightTrack != null)
            rightTrack.Rotate(Vector3.right, rotation, Space.Self);
    }
}
