
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class TankCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 6f, -10f);

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 cameraPosition = target.TransformPoint(offset);
        Vector3 lookPosition = target.position + Vector3.up * 2f;
        Vector3 lookDirection = lookPosition - cameraPosition;

        if (lookDirection.sqrMagnitude <= 0.001f)
            return;

        transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(lookDirection)
        );
    }
}
