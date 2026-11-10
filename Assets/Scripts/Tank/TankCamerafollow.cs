
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class TankCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Transform rotationTarget;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 6f, -10f);

    void LateUpdate()
    {
        if (target == null)
            return;

        if (rotationTarget == null ||
            !rotationTarget.IsChildOf(target))
        {
            TankTurretMove turretControls =
                target.GetComponent<TankTurretMove>();
            rotationTarget = turretControls != null
                ? turretControls.turret
                : null;
        }

        float yaw = rotationTarget != null
            ? rotationTarget.eulerAngles.y
            : target.eulerAngles.y;
        Quaternion followRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 cameraPosition = target.position +
                                 followRotation * offset;
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
