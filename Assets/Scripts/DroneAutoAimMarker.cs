
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
    private TankScopeZoom scopeZoom;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        scopeZoom = FindObjectOfType<TankScopeZoom>();

        if (aimMarker != null)
            aimMarker.gameObject.SetActive(false);
    }

    void Update()
    {
        Camera viewCamera = GetActiveViewCamera(out Rect displayRect);
        if (viewCamera == null || targetCanvas == null ||
            aimMarker == null)
        {
            if (aimMarker != null)
                aimMarker.gameObject.SetActive(false);
            return;
        }

        scanTimer -= Time.deltaTime;

        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            FindNearestDrone(viewCamera);
        }

        UpdateMarkerPosition(viewCamera, displayRect);
    }

    private Camera GetActiveViewCamera(out Rect displayRect)
    {
        displayRect = default;
        if (playerCamera == null)
            playerCamera = Camera.main;

        GameDirector gameDirector = GameDirector.Instance;
        if (gameDirector != null &&
            gameDirector.ActiveMissileViewCamera != null)
        {
            if (gameDirector.TryGetMissileViewRect(out displayRect))
                return gameDirector.ActiveMissileViewCamera;
        }

        if (scopeZoom == null)
            scopeZoom = FindObjectOfType<TankScopeZoom>();

        if (scopeZoom != null &&
            scopeZoom.CannonCamera != null &&
            scopeZoom.CannonCamera.isActiveAndEnabled)
        {
            displayRect = scopeZoom.CannonCamera.pixelRect;
            return scopeZoom.CannonCamera;
        }

        if (playerCamera != null && playerCamera.isActiveAndEnabled)
        {
            displayRect = playerCamera.pixelRect;
            return playerCamera;
        }

        return null;
    }

    private void FindNearestDrone(Camera viewCamera)
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
                ? Vector3.Distance(
                    playerTank.position,
                    drone.transform.position
                )
                : Vector3.Distance(
                    viewCamera.transform.position,
                    drone.transform.position
                );

            if (distance <= detectionRange &&
                distance < nearestDistance &&
                IsInsideCameraView(viewCamera, drone.transform.position))
            {
                nearestDistance = distance;
                currentTarget = drone.transform;
            }
        }
    }

    private void UpdateMarkerPosition(
        Camera viewCamera,
        Rect displayRect)
    {
        if (currentTarget == null ||
            viewCamera == null ||
            aimMarker == null)
        {
            aimMarker.gameObject.SetActive(false);
            return;
        }

        Vector3 viewportPosition =
            viewCamera.WorldToViewportPoint(currentTarget.position);

        // The drone is behind the camera.
        if (viewportPosition.z <= 0f)
        {
            aimMarker.gameObject.SetActive(false);
            return;
        }

        if (viewportPosition.x < 0f || viewportPosition.x > 1f ||
            viewportPosition.y < 0f || viewportPosition.y > 1f)
        {
            aimMarker.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPos = new Vector3(
            displayRect.x + viewportPosition.x * displayRect.width,
            displayRect.y + viewportPosition.y * displayRect.height,
            viewportPosition.z
        );

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

    private static bool IsInsideCameraView(
        Camera viewCamera,
        Vector3 worldPosition)
    {
        Vector3 viewportPosition =
            viewCamera.WorldToViewportPoint(worldPosition);

        return viewportPosition.z > 0f &&
               viewportPosition.x >= 0f && viewportPosition.x <= 1f &&
               viewportPosition.y >= 0f && viewportPosition.y <= 1f;
    }

}
