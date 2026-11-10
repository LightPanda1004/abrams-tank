using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Tankdetectsystem : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 200f;
    public float scanInterval = 0.2f;
    public bool requireLineOfSight = true;

    [Header("Detection Markers")]
    public Sprite blueDetectMarker;
    public Sprite redDetectMarker;
    public Vector2 markerSize = new Vector2(36f, 36f);
    public Vector2 markerPadding = new Vector2(24f, 24f);

    private sealed class DetectedTank
    {
        public TankHealth health;
        public Collider[] colliders;
        public RectTransform marker;
        public Image image;
    }

    private readonly List<DetectedTank> detectedTanks =
        new List<DetectedTank>();
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[32];

    private TankDronecontroll droneController;
    private TankHealth ownerHealth;
    private NetworkTankPlayer networkPlayer;
    private Canvas markerCanvas;
    private RectTransform markerCanvasRect;
    private float nextScanTime;

    private void Awake()
    {
        droneController = GetComponent<TankDronecontroll>();
        ownerHealth = GetComponent<TankHealth>();
        networkPlayer = GetComponent<NetworkTankPlayer>();
    }

    private void LateUpdate()
    {
        if (!IsLocalPlayer() || droneController == null ||
            !droneController.IsDeployed)
        {
            ClearDetections();
            return;
        }

        EnsureCanvas();
        if (markerCanvas == null)
            return;

        if (Time.time >= nextScanTime)
        {
            ScanForTanks();
            nextScanTime = Time.time + Mathf.Max(0.05f, scanInterval);
        }

        UpdateMarkers();
    }

    private bool IsLocalPlayer()
    {
        if (networkPlayer == null)
            return true;

        if (!networkPlayer.IsSpawned)
        {
            return GameDirector.Instance == null ||
                   !GameDirector.Instance.IsMultiplayerMode;
        }

        return networkPlayer.IsOwner;
    }

    private void EnsureCanvas()
    {
        if (markerCanvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "Drone Tank Detection HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        markerCanvas = canvasObject.GetComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = 100;
        markerCanvasRect = canvasObject.GetComponent<RectTransform>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void ScanForTanks()
    {
        Camera sensorCamera = droneController.droneCamera;
        if (sensorCamera == null)
            return;

        TankHealth[] tanks = FindObjectsOfType<TankHealth>();
        Vector3 origin = sensorCamera.transform.position;

        foreach (TankHealth tank in tanks)
        {
            if (tank == null || tank.IsDestroyed)
                continue;

            if (tank == ownerHealth)
            {
                RemoveDetection(tank);
                continue;
            }

            Collider[] colliders = tank.GetComponentsInChildren<Collider>();
            Vector3 target = GetAimPosition(tank, colliders);
            Vector3 offset = target - origin;
            float distance = offset.magnitude;
            Vector3 viewport = sensorCamera.WorldToViewportPoint(target);
            bool insideDroneView =
                viewport.z > 0f &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
            if (distance > detectionRange || distance <= 0.001f ||
                !insideDroneView)
            {
                continue;
            }

            if (requireLineOfSight &&
                !HasLineOfSight(origin, offset / distance, distance, tank))
            {
                continue;
            }

            DetectedTank detection = FindDetection(tank);
            if (detection == null)
            {
                detection = new DetectedTank
                {
                    health = tank,
                    colliders = colliders,
                    marker = CreateMarker(out Image image),
                    image = image
                };
                detectedTanks.Add(detection);
                ApplyTeamSprite(detection);
            }

        }

    }

    private Vector3 GetAimPosition(TankHealth tank, Collider[] colliders)
    {
        return TryGetBounds(colliders, out Bounds bounds)
            ? bounds.center
            : tank.transform.position + Vector3.up;
    }

    private static bool TryGetBounds(Collider[] colliders, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (colliders == null)
            return false;

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private bool HasLineOfSight(
        Vector3 origin,
        Vector3 direction,
        float distance,
        TankHealth target)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            lineOfSightHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.PositiveInfinity;
        Transform closestTransform = null;
        Transform ownerTransform = ownerHealth != null
            ? ownerHealth.transform
            : transform;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = lineOfSightHits[i];
            Transform hitTransform = hit.transform;
            if (hitTransform == null ||
                hitTransform == ownerTransform ||
                hitTransform.IsChildOf(ownerTransform) ||
                hitTransform == droneController.droneTransform ||
                hitTransform.IsChildOf(droneController.droneTransform))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestTransform = hitTransform;
            }
        }

        if (closestTransform == null)
            return true;

        TankHealth hitTank =
            closestTransform.GetComponentInParent<TankHealth>();
        return hitTank == target;
    }

    private DetectedTank FindDetection(TankHealth tank)
    {
        foreach (DetectedTank detection in detectedTanks)
        {
            if (detection.health == tank)
                return detection;
        }

        return null;
    }

    private void RemoveDetection(TankHealth tank)
    {
        DetectedTank detection = FindDetection(tank);
        if (detection == null)
            return;

        if (detection.marker != null)
            Destroy(detection.marker.gameObject);

        detectedTanks.Remove(detection);
    }

    private RectTransform CreateMarker(out Image image)
    {
        GameObject markerObject = new GameObject(
            "Tank Detection Marker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform marker = markerObject.GetComponent<RectTransform>();
        marker.SetParent(markerCanvasRect, false);
        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = markerSize;
        image = markerObject.GetComponent<Image>();
        image.raycastTarget = false;
        return marker;
    }

    private void ApplyTeamSprite(DetectedTank detection)
    {
        if (detection.image == null)
            return;

        NetworkTankPlayer targetNetworkPlayer =
            detection.health.GetComponent<NetworkTankPlayer>();
        bool isBlue;
        if (targetNetworkPlayer != null && targetNetworkPlayer.IsSpawned &&
            targetNetworkPlayer.HasTeam)
        {
            isBlue = targetNetworkPlayer.Team == TankTeam.Blue;
        }
        else
        {
            isBlue = detection.health.isPlayerTank;
        }
        detection.image.sprite = isBlue ? blueDetectMarker : redDetectMarker;
        detection.image.enabled = detection.image.sprite != null;
    }

    private void UpdateMarkers()
    {
        Camera displayCamera = droneController.DetectionDisplayCamera;
        if (displayCamera == null)
        {
            SetMarkersVisible(false);
            return;
        }

        foreach (DetectedTank detection in detectedTanks)
        {
            if (detection.marker == null)
                continue;

            if (detection.health == null)
            {
                detection.marker.gameObject.SetActive(false);
                continue;
            }

            ApplyTeamSprite(detection);
            bool onScreen = TryGetScreenBounds(
                displayCamera,
                detection.colliders,
                out Rect screenBounds
            );
            if (!onScreen)
            {
                PlaceMarkerAtScreenEdge(
                    displayCamera,
                    detection
                );
                continue;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerCanvasRect,
                screenBounds.center,
                null,
                out Vector2 localPosition
            );
            detection.marker.anchoredPosition = localPosition;
            detection.marker.sizeDelta = new Vector2(
                Mathf.Max(markerSize.x, screenBounds.width + markerPadding.x),
                Mathf.Max(markerSize.y, screenBounds.height + markerPadding.y)
            );
        }
    }

    private void PlaceMarkerAtScreenEdge(
        Camera camera,
        DetectedTank detection)
    {
        Vector3 target = GetAimPosition(
            detection.health,
            detection.colliders
        );
        Vector3 screenPoint = camera.WorldToScreenPoint(target);
        Vector2 direction;

        if (screenPoint.z > 0f)
        {
            direction = new Vector2(
                screenPoint.x - Screen.width * 0.5f,
                screenPoint.y - Screen.height * 0.5f
            );
        }
        else
        {
            Vector3 localDirection =
                camera.transform.InverseTransformPoint(target);
            direction = new Vector2(localDirection.x, localDirection.y);
            direction = -direction;
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        direction.Normalize();
        float halfWidth = Mathf.Max(
            1f,
            Screen.width * 0.5f - markerSize.x * 0.5f
        );
        float halfHeight = Mathf.Max(
            1f,
            Screen.height * 0.5f - markerSize.y * 0.5f
        );
        float xScale = Mathf.Abs(direction.x) > 0.0001f
            ? halfWidth / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float yScale = Mathf.Abs(direction.y) > 0.0001f
            ? halfHeight / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        Vector2 screenPosition =
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) +
            direction * Mathf.Min(xScale, yScale);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            markerCanvasRect,
            screenPosition,
            null,
            out Vector2 localPosition
        );
        detection.marker.anchoredPosition = localPosition;
        detection.marker.sizeDelta = markerSize;
        detection.marker.gameObject.SetActive(true);
    }

    private static bool TryGetScreenBounds(
        Camera camera,
        Collider[] colliders,
        out Rect screenBounds)
    {
        screenBounds = default;
        if (camera == null || !TryGetBounds(colliders, out Bounds bounds))
            return false;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z
                    );
                    Vector3 screenPoint = camera.WorldToScreenPoint(corner);
                    if (screenPoint.z <= 0f)
                        return false;

                    minX = Mathf.Min(minX, screenPoint.x);
                    minY = Mathf.Min(minY, screenPoint.y);
                    maxX = Mathf.Max(maxX, screenPoint.x);
                    maxY = Mathf.Max(maxY, screenPoint.y);
                }
            }
        }

        if (maxX < 0f || minX > Screen.width ||
            maxY < 0f || minY > Screen.height)
        {
            return false;
        }

        screenBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private void SetMarkersVisible(bool visible)
    {
        foreach (DetectedTank detection in detectedTanks)
        {
            if (detection.marker != null)
                detection.marker.gameObject.SetActive(visible);
        }
    }

    private void ClearDetections()
    {
        foreach (DetectedTank detection in detectedTanks)
        {
            if (detection.marker != null)
                Destroy(detection.marker.gameObject);
        }

        detectedTanks.Clear();
        nextScanTime = 0f;
    }

    private void OnDestroy()
    {
        if (markerCanvas != null)
            Destroy(markerCanvas.gameObject);
    }
}