
using UnityEngine;

public class TankCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 6f, -10f);

    [Header("Camera Movement")]
    public float followSpeed = 5f;
    public float rotationSpeed = 5f;

    void LateUpdate()
    {
        if (target == null)
            return;

        // Calculate the camera position behind the tank.
        Vector3 desiredPosition =
            target.TransformPoint(offset);

        // Move the camera smoothly.
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        // Look at a point above the tank.
        Vector3 lookPosition = target.position
                               + Vector3.up * 2f;

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookPosition - transform.position
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
