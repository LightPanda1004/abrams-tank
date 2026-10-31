
using UnityEngine;

public class TankTrackAnimation : MonoBehaviour
{
    [Header("Track Objects")]
    public Transform leftTrack;
    public Transform rightTrack;

    [Header("Animation")]
    public float rotationSpeed = 360f;

    void Update()
    {
        float move = Input.GetAxis("Vertical");

        // Rotate the tracks while moving forward or backward.
        float rotation = move * rotationSpeed * Time.deltaTime;

        if (leftTrack != null)
            leftTrack.Rotate(Vector3.right, rotation, Space.Self);

        if (rightTrack != null)
            rightTrack.Rotate(Vector3.right, rotation, Space.Self);
    }
}
