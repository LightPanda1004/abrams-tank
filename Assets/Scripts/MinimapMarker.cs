
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

    [Header("Multiplayer Colors")]
    public Color localPlayerColor = new Color(0.05f, 0.3f, 1f, 1f);
    public Color remotePlayerColor = new Color(1f, 0.12f, 0.08f, 1f);

    private GameObject tankMarkerInstance;
    private GameObject edgeMarkerInstance;

    private RectTransform tankMarkerRect;
    private RectTransform edgeMarkerRect;
    private Sprite edgeArrowSprite;
    private Texture2D edgeArrowTexture;
    private bool playerColorInitialized;

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
        ApplyPlayerMarkerColor();
        SetupEdgeArrow();

        SetVisible(tankMarkerInstance, false);
        SetVisible(edgeMarkerInstance, false);
    }

    private void LateUpdate()
    {
        if (!playerColorInitialized)
            ApplyPlayerMarkerColor();

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

    private void ApplyPlayerMarkerColor()
    {
        NetworkTankPlayer networkPlayer =
            GetComponent<NetworkTankPlayer>();
        TankHealth health = GetComponent<TankHealth>();

        Color markerColor;
        if (networkPlayer != null)
        {
            if (!networkPlayer.IsSpawned)
                return;

            markerColor = networkPlayer.IsOwner
                ? localPlayerColor
                : remotePlayerColor;
        }
        else if (health != null && health.isPlayerTank)
        {
            markerColor = localPlayerColor;
        }
        else
        {
            return;
        }

        Image tankImage = tankMarkerRect != null
            ? tankMarkerRect.GetComponent<Image>()
            : null;
        if (tankImage != null)
            tankImage.color = markerColor;

        Image edgeImage = edgeMarkerRect != null
            ? edgeMarkerRect.GetComponent<Image>()
            : null;
        if (edgeImage != null)
            edgeImage.color = markerColor;

        playerColorInitialized = true;
    }

    private void SetupEdgeArrow()
    {
        Image edgeImage = edgeMarkerRect != null
            ? edgeMarkerRect.GetComponent<Image>()
            : null;
        if (edgeImage == null)
            return;

        const int textureSize = 32;
        edgeArrowTexture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false
        );
        edgeArrowTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            float halfWidth = (textureSize - 1 - y) * 0.42f;
            int minimumX = Mathf.CeilToInt(textureSize * 0.5f - halfWidth);
            int maximumX = Mathf.FloorToInt(textureSize * 0.5f + halfWidth);

            for (int x = minimumX; x <= maximumX; x++)
                pixels[y * textureSize + x] = Color.white;
        }

        edgeArrowTexture.SetPixels(pixels);
        edgeArrowTexture.Apply();
        edgeArrowSprite = Sprite.Create(
            edgeArrowTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize
        );
        edgeImage.sprite = edgeArrowSprite;
        edgeImage.preserveAspect = true;

        edgeMarkerRect.sizeDelta = new Vector2(18f, 18f);
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

        if (edgeArrowSprite != null)
            Destroy(edgeArrowSprite);

        if (edgeArrowTexture != null)
            Destroy(edgeArrowTexture);
    }
}
