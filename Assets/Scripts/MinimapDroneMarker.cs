
using UnityEngine;
using UnityEngine.UI;

public class MinimapDroneMarker : MonoBehaviour
{
    private static Sprite directionTriangleSprite;

    [Header("Target Drone")]
    public Transform targetDrone;
    public Transform directionOrigin;

    [Header("Minimap References")]
    public Camera minimapCamera;
    public RectTransform minimapDisplay;

    [Header("Drone Marker Prefabs")]
    public GameObject droneMarker;
    public GameObject edgeMarker;

    [Header("Marker Settings")]
    [Min(0f)]
    public float edgePadding = 12f;
    public Vector2 edgeMarkerSize = new Vector2(20f, 20f);

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
        if (edgeMarkerRect != null)
            edgeMarkerRect.sizeDelta = edgeMarkerSize;
        SetupEdgeMarkerSprite();

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

        Transform origin = directionOrigin != null
            ? directionOrigin
            : minimapCamera.transform;
        Vector3 localDirection = minimapCamera.transform
            .InverseTransformDirection(targetDrone.position - origin.position);
        Vector2 direction = new Vector2(
            localDirection.x * minimapDisplay.rect.width,
            localDirection.y * minimapDisplay.rect.height
        );

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        Vector2 normalizedDirection = direction.normalized;
        Vector2 edgeLimit = new Vector2(
            Mathf.Max(
                0f,
                minimapDisplay.rect.width * 0.5f -
                    edgePadding -
                    GetMarkerHalfSize(edgeMarkerRect.rect.width)
            ),
            Mathf.Max(
                0f,
                minimapDisplay.rect.height * 0.5f -
                    edgePadding -
                    GetMarkerHalfSize(edgeMarkerRect.rect.height)
            )
        );
        float scaleX =
            Mathf.Abs(normalizedDirection.x) > 0.0001f
            ? edgeLimit.x / Mathf.Abs(normalizedDirection.x)
            : float.PositiveInfinity;

        float scaleY =
            Mathf.Abs(normalizedDirection.y) > 0.0001f
            ? edgeLimit.y / Mathf.Abs(normalizedDirection.y)
            : float.PositiveInfinity;

        float scale = Mathf.Min(scaleX, scaleY);

        edgeMarkerRect.anchoredPosition =
            normalizedDirection * scale;

        // Assumes the arrow points upward at zero rotation.
        float angle =
            Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) *
            Mathf.Rad2Deg + 90f;

        edgeMarkerRect.localRotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        SetVisible(droneMarkerInstance, false);
        SetVisible(edgeMarkerInstance, false);
    }

    private static float GetMarkerHalfSize(float size)
    {
        return Mathf.Max(0f, size) * 0.5f;
    }

    private void SetupEdgeMarkerSprite()
    {
        if (edgeMarkerRect == null)
            return;

        Image image = edgeMarkerRect.GetComponent<Image>();
        if (image == null)
            return;

        if (directionTriangleSprite == null)
            directionTriangleSprite = CreateDirectionTriangleSprite();

        image.sprite = directionTriangleSprite;
        image.preserveAspect = true;
    }

    private static Sprite CreateDirectionTriangleSprite()
    {
        const int textureSize = 64;
        const int baseY = 8;
        const int tipY = 58;
        const float halfBaseWidth = 28f;

        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "Minimap Drone Direction Triangle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[textureSize * textureSize];
        float centerX = (textureSize - 1) * 0.5f;

        for (int y = baseY; y <= tipY; y++)
        {
            float height = (y - baseY) / (float)(tipY - baseY);
            float halfWidth = halfBaseWidth * height;
            for (int x = 0; x < textureSize; x++)
            {
                if (Mathf.Abs(x - centerX) <= halfWidth)
                    pixels[y * textureSize + x] = Color.white;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize
        );
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
