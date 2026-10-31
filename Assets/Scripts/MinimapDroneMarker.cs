
using UnityEngine;
using UnityEngine.UI;

public class MinimapDroneMarker : MonoBehaviour
{
    [Header("Target Drone")]
    public Transform targetDrone;

    [Header("Minimap References")]
    public Camera minimapCamera;
    public RectTransform minimapDisplay;

    [Header("Drone Marker Prefabs")]
    public GameObject droneMarker;
    public GameObject edgeMarker;

    [Header("Marker Settings")]
    [Min(0f)]
    public float edgePadding = 12f;

    private GameObject droneMarkerInstance;
    private GameObject edgeMarkerInstance;

    private RectTransform droneMarkerRect;
    private RectTransform edgeMarkerRect;

    private void Start()
    {
        if (targetDrone == null)
            targetDrone = transform;

        if (minimapCamera == null)
        {
            GameObject cameraObject =
                GameObject.Find("MinimapCamera");

            if (cameraObject != null)
                minimapCamera =
                    cameraObject.GetComponent<Camera>();
        }

        if (minimapDisplay == null)
        {
            GameObject displayObject =
                GameObject.Find("MinimapDisplay");

            if (displayObject != null)
                minimapDisplay =
                    displayObject.GetComponent<RectTransform>();
        }

        if (minimapCamera == null || minimapDisplay == null)
        {
            Debug.LogError(
                "MinimapDroneMarker: Missing minimap camera or display.",
                this
            );

            enabled = false;
            return;
        }

        // Create the drone marker.
        if (droneMarker != null)
        {
            droneMarkerInstance = Instantiate(
                droneMarker,
                minimapDisplay,
                false
            );

            droneMarkerRect =
                droneMarkerInstance.GetComponent<RectTransform>();
        }

        // Create the edge arrow.
        if (edgeMarker != null)
        {
            edgeMarkerInstance = Instantiate(
                edgeMarker,
                minimapDisplay,
                false
            );

            edgeMarkerRect =
                edgeMarkerInstance.GetComponent<RectTransform>();
        }

        SetupMarker(droneMarkerRect);
        SetupMarker(edgeMarkerRect);

        SetVisible(droneMarkerInstance, false);
        SetVisible(edgeMarkerInstance, false);
    }

    private void LateUpdate()
    {
        if (targetDrone == null ||
            minimapCamera == null ||
            minimapDisplay == null)
            return;

        Vector3 viewport =
            minimapCamera.WorldToViewportPoint(
                targetDrone.position
            );

        bool insideView =
            viewport.z > 0f &&
            viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;

        if (insideView)
        {
            SetVisible(
                droneMarkerInstance,
                droneMarkerRect != null
            );

            SetVisible(edgeMarkerInstance, false);

            if (droneMarkerRect != null)
            {
                droneMarkerRect.anchoredPosition =
                    new Vector2(
                        (viewport.x - 0.5f) *
                            minimapDisplay.rect.width,
                        (viewport.y - 0.5f) *
                            minimapDisplay.rect.height
                    );

                droneMarkerRect.localRotation =
                    Quaternion.identity;
            }

            return;
        }

        SetVisible(droneMarkerInstance, false);

        SetVisible(
            edgeMarkerInstance,
            edgeMarkerRect != null
        );

        if (edgeMarkerRect == null)
            return;

        Vector2 direction = new Vector2(
            viewport.x - 0.5f,
            viewport.y - 0.5f
        );

        // Handle drones behind the minimap camera.
        if (viewport.z <= 0f)
        {
            Vector3 localPosition =
                minimapCamera.transform.InverseTransformPoint(
                    targetDrone.position
                );

            direction = new Vector2(
                -localPosition.x,
                -localPosition.y
            );
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        direction.Normalize();

        float halfWidth = Mathf.Max(
            0f,
            minimapDisplay.rect.width * 0.5f - edgePadding
        );

        float halfHeight = Mathf.Max(
            0f,
            minimapDisplay.rect.height * 0.5f - edgePadding
        );

        float scaleX =
            Mathf.Abs(direction.x) > 0.0001f
            ? halfWidth / Mathf.Abs(direction.x)
            : float.PositiveInfinity;

        float scaleY =
            Mathf.Abs(direction.y) > 0.0001f
            ? halfHeight / Mathf.Abs(direction.y)
            : float.PositiveInfinity;

        float scale = Mathf.Min(scaleX, scaleY);

        edgeMarkerRect.anchoredPosition =
            direction * scale;

        // Assumes the arrow points upward at zero rotation.
        float angle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg - 90f;

        edgeMarkerRect.localRotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    private void SetupMarker(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = rect.GetComponent<Image>();

        if (image != null)
            image.raycastTarget = false;
    }

    private void SetVisible(
        GameObject marker,
        bool visible
    )
    {
        if (marker != null &&
            marker.activeSelf != visible)
        {
            marker.SetActive(visible);
        }
    }

    public void RemoveMarkers()
    {
        if (droneMarkerInstance != null)
        {
            Destroy(droneMarkerInstance);
            droneMarkerInstance = null;
            droneMarkerRect = null;
        }

        if (edgeMarkerInstance != null)
        {
            Destroy(edgeMarkerInstance);
            edgeMarkerInstance = null;
            edgeMarkerRect = null;
        }
    }

    private void OnDestroy()
    {
        RemoveMarkers();
    }
}
