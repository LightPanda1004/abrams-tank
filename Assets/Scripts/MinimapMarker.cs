
using UnityEngine;
using UnityEngine.UI;

public class MinimapMarker : MonoBehaviour
{
    [Header("Target Tank")]
    public Transform targetTank;

    [Header("Minimap References")]
    public Camera minimapCamera;
    public RectTransform minimapDisplay;

    [Header("Marker Prefabs")]
    public GameObject TankMarker;
    public GameObject EdgeMarker;

    [Header("Edge Settings")]
    [Min(0f)]
    public float edgePadding = 12f;

    private GameObject tankMarkerInstance;
    private GameObject edgeMarkerInstance;

    private RectTransform tankMarkerRect;
    private RectTransform edgeMarkerRect;

    private void Start()
    {
        if (targetTank == null)
            targetTank = transform;

        if (minimapCamera == null)
        {
            GameObject cameraObject =
                GameObject.Find("MinimapCamera");

            if (cameraObject != null)
                minimapCamera = cameraObject.GetComponent<Camera>();
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
                "MinimapMarker: Missing minimap camera or display.",
                this);
            enabled = false;
            return;
        }

        // Create only one instance of each marker.
        if (TankMarker != null)
        {
            tankMarkerInstance = Instantiate(
                TankMarker, minimapDisplay, false);

            tankMarkerRect =
                tankMarkerInstance.GetComponent<RectTransform>();
        }

        if (EdgeMarker != null)
        {
            edgeMarkerInstance = Instantiate(
                EdgeMarker, minimapDisplay, false);

            edgeMarkerRect =
                edgeMarkerInstance.GetComponent<RectTransform>();
        }

        SetupMarker(tankMarkerRect);
        SetupMarker(edgeMarkerRect);

        SetVisible(tankMarkerInstance, false);
        SetVisible(edgeMarkerInstance, false);
    }

    private void LateUpdate()
    {
        if (targetTank == null ||
            minimapCamera == null ||
            minimapDisplay == null)
            return;

        Vector3 viewport =
            minimapCamera.WorldToViewportPoint(targetTank.position);

        bool insideView =
            viewport.z > 0f &&
            viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;

        if (insideView)
        {
            SetVisible(tankMarkerInstance, tankMarkerRect != null);
            SetVisible(edgeMarkerInstance, false);

            if (tankMarkerRect != null)
            {
                tankMarkerRect.anchoredPosition = new Vector2(
                    (viewport.x - 0.5f) * minimapDisplay.rect.width,
                    (viewport.y - 0.5f) * minimapDisplay.rect.height
                );

                tankMarkerRect.localRotation = Quaternion.identity;
            }

            return;
        }

        SetVisible(tankMarkerInstance, false);
        SetVisible(edgeMarkerInstance, edgeMarkerRect != null);

        if (edgeMarkerRect == null)
            return;

        // Use viewport coordinates for the edge direction.
        Vector2 direction = new Vector2(
            viewport.x - 0.5f,
            viewport.y - 0.5f
        );

        // If the target is behind the camera, use its
        // position relative to the camera's ground plane.
        if (viewport.z <= 0f)
        {
            Vector3 local =
                minimapCamera.transform.InverseTransformPoint(
                    targetTank.position);

            direction = new Vector2(local.x, local.y);
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        direction.Normalize();

        float halfWidth = Mathf.Max(
            0f, minimapDisplay.rect.width * 0.5f - edgePadding);

        float halfHeight = Mathf.Max(
            0f, minimapDisplay.rect.height * 0.5f - edgePadding);

        float scaleX = Mathf.Abs(direction.x) > 0.0001f
            ? halfWidth / Mathf.Abs(direction.x)
            : float.PositiveInfinity;

        float scaleY = Mathf.Abs(direction.y) > 0.0001f
            ? halfHeight / Mathf.Abs(direction.y)
            : float.PositiveInfinity;

        float scale = Mathf.Min(scaleX, scaleY);

        edgeMarkerRect.anchoredPosition = direction * scale;

        // Assumes the arrow graphic points upward at zero rotation.
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

        // Marker images should not intercept UI clicks.
        Image image = rect.GetComponent<Image>();

        if (image != null)
            image.raycastTarget = false;
    }

    private void SetVisible(GameObject marker, bool visible)
    {
        if (marker != null && marker.activeSelf != visible)
            marker.SetActive(visible);
    }

    public void RemoveMarkers()
    {
        if (tankMarkerInstance != null)
        {
            Destroy(tankMarkerInstance);
            tankMarkerInstance = null;
            tankMarkerRect = null;
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
