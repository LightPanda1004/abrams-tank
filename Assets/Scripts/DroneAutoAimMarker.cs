
using UnityEngine;

public class DroneAutoAimMarker : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Canvas targetCanvas;
    public RectTransform aimMarker;
    public Transform playerTank;

    [Header("Detection")]
    public float detectionRange = 100f;
    public float scanInterval = 0.25f;

    private Transform currentTarget;
    private float scanTimer;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (aimMarker != null)
            aimMarker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (playerCamera == null ||
            targetCanvas == null ||
            aimMarker == null)
            return;

        scanTimer -= Time.deltaTime;

        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            FindNearestDrone();
        }

        UpdateMarkerPosition();
    }

   
void FindNearestDrone()
{
    currentTarget = null;
    float nearestDistance = detectionRange;

    AIDroneController[] drones =
        FindObjectsOfType<AIDroneController>();

    foreach (AIDroneController drone in drones)
    {
        if (drone == null || !drone.gameObject.activeInHierarchy)
            continue;

        TankHealth health =
            drone.GetComponentInParent<TankHealth>();

        if (health != null && health.IsDestroyed)
            continue;

        float distance = playerTank != null
            ? Vector3.Distance(playerTank.position, drone.transform.position)
            : Vector3.Distance(playerCamera.transform.position,
                               drone.transform.position);

        if (distance <= detectionRange && distance < nearestDistance)
        {
            nearestDistance = distance;
            currentTarget = drone.transform;
        }
    }
}



    void UpdateMarkerPosition()
    {
        if (currentTarget == null ||
            playerCamera == null ||
            aimMarker == null)
        {
            aimMarker.gameObject.SetActive(false);
            return;
        }

        // Convert the drone's world position to screen pixels.
        Vector3 screenPos =
            playerCamera.WorldToScreenPoint(currentTarget.position);

        // The drone is behind the camera.
        if (screenPos.z <= 0f)
        {
            aimMarker.gameObject.SetActive(false);
            return;
        }

        // Hide the marker when the drone is outside the camera view.
        Rect cameraRect = playerCamera.pixelRect;

        bool onScreen =
            screenPos.x >= cameraRect.xMin &&
            screenPos.x <= cameraRect.xMax &&
            screenPos.y >= cameraRect.yMin &&
            screenPos.y <= cameraRect.yMax;

        if (!onScreen)
        {
            aimMarker.gameObject.SetActive(false);
            return;
        }

        // Convert screen pixels into the marker parent's local coordinates.
        RectTransform parentRect =
            aimMarker.parent as RectTransform;

        if (parentRect == null)
        {
            Debug.LogError(
                "Aim Marker must be inside a UI Canvas.");
            aimMarker.gameObject.SetActive(false);
            return;
        }

        Camera uiCamera =
            targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            screenPos,
            uiCamera,
            out Vector2 localPoint))
        {
            aimMarker.anchoredPosition = localPoint;
            aimMarker.gameObject.SetActive(true);
        }
    }

}
