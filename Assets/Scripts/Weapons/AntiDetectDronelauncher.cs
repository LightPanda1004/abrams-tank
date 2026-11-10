using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class AntiDetectDronelauncher : MonoBehaviour
{
    [Header("Launcher")]
    public Transform firePoint;
    public GameObject homingMissilePrefab;
    public GameObject launchEffectPrefab;
    public KeyCode fireKey = KeyCode.R;
    public float detectionRange = 100f;
    public float scanInterval = 0.2f;
    public float reloadTime = 4f;
    public int ammunition = 8;
    [Range(0f, 100f)]
    [Tooltip("Percentage of launches that receive accurate target guidance.")]
    public float accuracy = 100f;
    [Min(0f)]
    public float maximumInaccuracyDegrees = 3f;
    [Min(0f)]
    public float missDeviationDistance = 5f;
    [Min(0.1f)]
    public float hitDistance = 2.5f;

    [Header("Detection Marker")]
    public Sprite redDetectMarker;
    public Vector2 markerPadding = new Vector2(24f, 24f);
    public Vector2 minimumMarkerSize = new Vector2(36f, 36f);

    private NetworkTankPlayer networkPlayer;
    private AntiDroneLauncher aiDroneLauncher;
    private TankScopeZoom scopeControls;
    private Camera tankCamera;
    private NetworkTankPlayer currentTarget;
    private Canvas markerCanvas;
    private RectTransform markerCanvasRect;
    private RectTransform markerRect;
    private Image markerImage;
    private float scanTimer;
    private float reloadTimer;
    private float serverNextFireTime;
    private int serverAmmunition;
    private bool launchPending;

    public bool IsReloading => reloadTimer > 0f || launchPending;
    public float RemainingReload =>
        launchPending
            ? Mathf.Max(0.1f, reloadTime)
            : Mathf.Max(0f, reloadTimer);
    public float ReloadProgress =>
        launchPending
            ? 0f
            : reloadTimer > 0f
                ? 1f - Mathf.Clamp01(
                    reloadTimer / Mathf.Max(0.1f, reloadTime))
                : 1f;
    public int AmmoRemaining => ammunition;
    public int ServerAmmoRemaining => serverAmmunition;
    public float ServerRemainingReload =>
        Mathf.Max(0f, serverNextFireTime - Time.time);

    private void Awake()
    {
        networkPlayer = GetComponent<NetworkTankPlayer>();
        aiDroneLauncher = GetComponentInChildren<AntiDroneLauncher>(true);
        scopeControls = GetComponentInChildren<TankScopeZoom>(true);
        serverAmmunition = ammunition;
    }

    private void Update()
    {
        bool multiplayer = GameDirector.Instance != null &&
                           GameDirector.Instance.IsMultiplayerMode;

        if (aiDroneLauncher != null && aiDroneLauncher.enabled)
            aiDroneLauncher.enabled = false;

        if (multiplayer &&
            (networkPlayer == null || !networkPlayer.isOwned ||
             !isActiveAndEnabled))
        {
            currentTarget = null;
            SetMarkerVisible(false);
            return;
        }

        if (!isActiveAndEnabled)
        {
            currentTarget = null;
            SetMarkerVisible(false);
            return;
        }

        reloadTimer = Mathf.Max(0f, reloadTimer - Time.deltaTime);

        if (tankCamera == null || !tankCamera.isActiveAndEnabled)
            tankCamera = FindTankCamera();

        if (tankCamera == null || !tankCamera.isActiveAndEnabled)
        {
            currentTarget = null;
            SetMarkerVisible(false);
            return;
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = Mathf.Max(0.05f, scanInterval);
            FindNearestEnemyDrone();
        }

        UpdateMarker();

        if (Input.GetKeyDown(fireKey))
            FireMissile();
    }

    private Camera FindTankCamera()
    {
        if (scopeControls != null && scopeControls.IsAiming &&
            scopeControls.CannonCamera != null &&
            scopeControls.CannonCamera.isActiveAndEnabled)
        {
            return scopeControls.CannonCamera;
        }

        TankCameraFollow cameraFollow =
            FindObjectOfType<TankCameraFollow>();
        if (cameraFollow != null)
            return cameraFollow.GetComponent<Camera>();

        return Camera.main;
    }

    private void FindNearestEnemyDrone()
    {
        currentTarget = null;
        if (tankCamera == null)
            return;

        float nearestDistance = detectionRange;
        NetworkTankPlayer[] players =
            FindObjectsOfType<NetworkTankPlayer>();

        foreach (NetworkTankPlayer candidate in players)
        {
            if (candidate == null || candidate == networkPlayer ||
                !candidate.isOwned && !candidate.isServer ||
                !candidate.HasTeam ||
                candidate.Team == networkPlayer.Team ||
                !candidate.IsDroneDeployed)
            {
                continue;
            }

            TankDronecontroll droneController =
                candidate.GetComponent<TankDronecontroll>();
            Transform drone = droneController != null
                ? droneController.droneTransform
                : null;
            if (drone == null)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                drone.position
            );
            if (distance > nearestDistance ||
                !IsInsideTankCamera(drone.position))
            {
                continue;
            }

            nearestDistance = distance;
            currentTarget = candidate;
        }
    }

    private bool IsInsideTankCamera(Vector3 worldPosition)
    {
        Vector3 viewport = tankCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f &&
               viewport.x >= 0f && viewport.x <= 1f &&
               viewport.y >= 0f && viewport.y <= 1f;
    }

    private void EnsureMarker()
    {
        if (markerRect != null)
            return;

        GameObject canvasObject = new GameObject(
            "Enemy Drone Detection HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        markerCanvas = canvasObject.GetComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = 101;
        markerCanvasRect = canvasObject.GetComponent<RectTransform>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject markerObject = new GameObject(
            "Enemy Drone Detection Marker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.SetParent(markerCanvasRect, false);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = minimumMarkerSize;
        markerImage = markerObject.GetComponent<Image>();
        markerImage.sprite = redDetectMarker;
        markerImage.raycastTarget = false;
        markerObject.SetActive(false);
    }

    private void UpdateMarker()
    {
        if (currentTarget == null || tankCamera == null)
        {
            SetMarkerVisible(false);
            return;
        }

        TankDronecontroll droneController =
            currentTarget.GetComponent<TankDronecontroll>();
        if (droneController == null ||
            droneController.droneTransform == null ||
            !IsInsideTankCamera(droneController.droneTransform.position))
        {
            SetMarkerVisible(false);
            return;
        }

        EnsureMarker();
        if (markerRect == null || markerImage == null)
            return;

        markerImage.sprite = redDetectMarker;
        markerImage.enabled = redDetectMarker != null;

        Collider[] colliders =
            droneController.droneTransform.GetComponentsInChildren<Collider>();
        if (!TryGetScreenBounds(colliders, out Rect bounds))
        {
            SetMarkerVisible(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            markerCanvasRect,
            bounds.center,
            null,
            out Vector2 localPosition
        );
        markerRect.anchoredPosition = localPosition;
        markerRect.sizeDelta = new Vector2(
            Mathf.Max(minimumMarkerSize.x, bounds.width + markerPadding.x),
            Mathf.Max(minimumMarkerSize.y, bounds.height + markerPadding.y)
        );
        SetMarkerVisible(true);
    }

    private bool TryGetScreenBounds(
        Collider[] colliders,
        out Rect screenBounds)
    {
        screenBounds = default;
        Bounds bounds = default;
        bool hasBounds = false;

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

        if (!hasBounds)
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
                    Vector3 screenPoint =
                        tankCamera.WorldToScreenPoint(corner);
                    if (screenPoint.z <= 0f)
                        return false;

                    minX = Mathf.Min(minX, screenPoint.x);
                    minY = Mathf.Min(minY, screenPoint.y);
                    maxX = Mathf.Max(maxX, screenPoint.x);
                    maxY = Mathf.Max(maxY, screenPoint.y);
                }
            }
        }

        if (maxX < tankCamera.pixelRect.xMin ||
            minX > tankCamera.pixelRect.xMax ||
            maxY < tankCamera.pixelRect.yMin ||
            minY > tankCamera.pixelRect.yMax)
        {
            return false;
        }

        screenBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private void FireMissile()
    {
        if (reloadTimer > 0f || launchPending || ammunition <= 0 ||
            currentTarget == null || firePoint == null ||
            homingMissilePrefab == null)
        {
            return;
        }

        launchPending = true;
        if (!networkPlayer.RequestAntiDroneMissile(currentTarget))
            launchPending = false;
    }

    public void ApplyServerLaunchResult(
        bool accepted,
        int serverAmmo,
        float serverReload)
    {
        if (networkPlayer == null || !networkPlayer.isOwned)
            return;

        launchPending = false;
        ammunition = Mathf.Max(0, serverAmmo);
        reloadTimer = accepted
            ? Mathf.Max(0f, reloadTime)
            : Mathf.Max(0f, serverReload);
    }

    public bool TryReserveServerLaunch(NetworkTankPlayer target)
    {
        if (target == null || firePoint == null ||
            homingMissilePrefab == null ||
            serverAmmunition <= 0 ||
            Time.time < serverNextFireTime)
        {
            return false;
        }

        TankDronecontroll targetController =
            target.GetComponent<TankDronecontroll>();
        if (targetController == null ||
            targetController.droneTransform == null ||
            Vector3.Distance(
                transform.position,
                targetController.droneTransform.position
            ) > detectionRange)
        {
            return false;
        }

        serverAmmunition--;
        serverNextFireTime = Time.time + Mathf.Max(0f, reloadTime);
        return true;
    }

    public Vector2 GetAimError(float targetDistance)
    {
        float accuracyChance = Mathf.Clamp01(accuracy / 100f);
        if (Random.value > accuracyChance)
        {
            Vector2 missDirection = Random.insideUnitCircle.normalized;
            float missDistance = missDeviationDistance *
                                 Random.Range(0.9f, 1.1f);
            float missAngle = Mathf.Atan2(
                missDistance,
                Mathf.Max(0.1f, targetDistance)
            ) * Mathf.Rad2Deg;
            return missDirection * missAngle;
        }

        float inaccuracy = maximumInaccuracyDegrees * 0.05f;
        return Random.insideUnitCircle * inaccuracy;
    }

    public void SpawnNetworkMissile(
        int targetNetworkIdentityId,
        Vector3 position,
        Quaternion rotation,
        int launchingClientId,
        Vector2 aimErrorDegrees)
    {
        if (NetworkServer.spawned.TryGetValue(
                targetNetworkIdentityId,
                out NetworkIdentity targetIdentity))
        {
            NetworkTankPlayer targetPlayer =
                targetIdentity.GetComponent<NetworkTankPlayer>();
            TankDronecontroll targetController =
                targetIdentity.GetComponent<TankDronecontroll>();
            if (targetPlayer == null || targetController == null ||
                targetController.droneTransform == null)
            {
                return;
            }

            GameObject missileObject = Instantiate(
                homingMissilePrefab,
                position,
                rotation
            );
            HomingMissile missile =
                missileObject.GetComponent<HomingMissile>();
            if (missile == null)
            {
                Debug.LogError(
                    "The homing missile prefab needs a HomingMissile component.",
                    missileObject
                );
                Destroy(missileObject);
                return;
            }

            missile.SetOwner(transform.root);
            missile.SetNetworkTarget(
                targetController.droneTransform,
                targetPlayer,
                NetworkClient.isConnected &&
                NetworkClient.connection.connectionId == launchingClientId,
                aimErrorDegrees,
                hitDistance
            );

            if (launchEffectPrefab != null)
            {
                GameObject effect = Instantiate(
                    launchEffectPrefab,
                    position,
                    rotation
                );
                Destroy(effect, 3f);
            }
        }
    }

    private void SetMarkerVisible(bool visible)
    {
        if (markerRect != null && markerRect.gameObject.activeSelf != visible)
            markerRect.gameObject.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (markerCanvas != null)
            Destroy(markerCanvas.gameObject);
    }
}
