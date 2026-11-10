using UnityEngine;

public class DroneCamerafollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offsetFromTarget = new Vector3(0f, 2f, -6f);
    public float followSpeed = 5f;
    public float rotationSpeed = 5f;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Quaternion followLocalRotation;
    private float followYaw;
    private bool isFollowing;

    private void Awake()
    {
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;

        if (target == null)
        {
            Debug.LogError(
                "DroneCamerafollow: Assign the drone transform as the target.",
                this
            );
            enabled = false;
            return;
        }
    }

    private void LateUpdate()
    {
        if (!isFollowing || target == null)
            return;

        Vector3 targetPosition =
            target.position +
            Quaternion.Euler(0f, followYaw, 0f) * offsetFromTarget;
        Quaternion targetRotation =
            Quaternion.Euler(0f, followYaw, 0f) * followLocalRotation;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSpeed * Time.deltaTime)
        );
    }

    public void BeginFollowing()
    {
        if (target == null || isFollowing)
            return;

        followYaw = target.eulerAngles.y;
        followLocalRotation =
            Quaternion.Inverse(Quaternion.Euler(0f, followYaw, 0f)) *
            transform.rotation;
        transform.SetParent(null, true);
        isFollowing = true;
    }

    public void SetFollowYaw(float yaw)
    {
        followYaw = yaw;
    }

    public void StopFollowing()
    {
        if (!isFollowing)
            return;

        isFollowing = false;
        transform.SetParent(originalParent, false);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
    }
}