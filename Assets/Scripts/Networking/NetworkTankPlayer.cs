using TMPro;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public enum TankTeam : byte
{
    Unassigned,
    Blue,
    Red
}

public class NetworkTankPlayer : NetworkBehaviour
{
    [SyncVar]
    private float syncedHealth = 500f;

    [SyncVar]
    private byte syncedTeam = (byte)TankTeam.Unassigned;

    [SyncVar]
    private string syncedPlayerName = "Player";

    [SyncVar]
    private int syncedEnemyTanksDestroyed = 0;

    [SyncVar]
    private float syncedTankSpeed = 0f;

    [SyncVar]
    private Quaternion syncedTurretRotation = Quaternion.identity;

    [SyncVar]
    private Quaternion syncedCannonRotation = Quaternion.identity;

    private TankHealth tankHealth;
    private TankMovement tankMovement;
    private TankFire tankFire;
    private AntiDroneLauncher missileLauncher;
    private AntiDetectDronelauncher antiDetectDronelauncher;
    private TankDronecontroll tankDroneController;
    private TankTurretMove turretControls;
    private TextMeshProUGUI nameTagText;
    private Canvas nameTagCanvas;
    private Canvas healthBarCanvas;
    private Image healthBarFill;
    private RectTransform healthBarFillRect;
    private float healthBarFillWidth;
    private float nextShellTime;
    private float nextTankSpeedUpdate;
    private float nextAimUpdate;
    private bool serverDroneDeployed;

    public TankTeam Team => (TankTeam)syncedTeam;
    public bool HasTeam => Team != TankTeam.Unassigned;
    public string PlayerName => syncedPlayerName;
    public int EnemyTanksDestroyed => syncedEnemyTanksDestroyed;
    public float DisplayedTankSpeed =>
        isServer
            ? syncedTankSpeed
            : tankMovement != null
                ? tankMovement.currentSpeed
                : 0f;
    public bool IsDroneDeployed =>
        tankDroneController != null && tankDroneController.IsDeployed;
    public static TankTeam LocalPlayerTeam { get; private set; }

    private void Awake()
    {
        tankHealth = GetComponent<TankHealth>();
        tankMovement = GetComponent<TankMovement>();
        tankFire = GetComponent<TankFire>();
        missileLauncher = GetComponentInChildren<AntiDroneLauncher>(true);
        antiDetectDronelauncher = GetComponent<AntiDetectDronelauncher>();
        tankDroneController = GetComponent<TankDronecontroll>();
        turretControls = GetComponent<TankTurretMove>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (tankHealth != null)
            syncedHealth = tankHealth.CurrentHealth;

        if (turretControls != null)
        {
            if (turretControls.turret != null)
                syncedTurretRotation = turretControls.turret.localRotation;
            if (turretControls.cannon != null)
                syncedCannonRotation = turretControls.cannon.localRotation;
        }

        if (GameDirector.Instance != null)
            GameDirector.Instance.AssignIdentityToPlayer(this);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (syncedHealth != 500f && tankHealth != null)
            tankHealth.SetReplicatedHealth(syncedHealth);

        CreateNameTag();
        UpdateNameTag();

        if (isOwned)
        {
            LocalPlayerTeam = Team;

            if (GameDirector.Instance != null)
                GameDirector.Instance.SetLocalPlayerTank(this);

            TankCameraFollow cameraFollow =
                FindObjectOfType<TankCameraFollow>();

            if (cameraFollow != null)
            {
                cameraFollow.target = transform;
                TankTurretMove turretControls =
                    GetComponent<TankTurretMove>();
                cameraFollow.rotationTarget =
                    turretControls != null
                        ? turretControls.turret
                        : null;
            }

            Camera playerMinimapCamera = null;
            foreach (Camera tankCamera in
                         GetComponentsInChildren<Camera>(true))
            {
                if (tankCamera.gameObject.name == "MinimapCamera")
                {
                    playerMinimapCamera = tankCamera;
                    break;
                }
            }

            if (playerMinimapCamera != null)
            {
                MinimapMarker.SetLocalPlayerCamera(playerMinimapCamera);
            }
            else
            {
                Debug.LogError(
                    "NetworkTankPlayer: Could not find the local minimap camera.",
                    this);
            }
        }
        else
        {
            CreateHealthBar();
            UpdateHealthBar(syncedHealth);
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        serverDroneDeployed = false;

        if (tankDroneController != null)
            tankDroneController.ResetForDespawn();

        if (nameTagCanvas != null)
            Destroy(nameTagCanvas.gameObject);

        if (healthBarCanvas != null)
            Destroy(healthBarCanvas.gameObject);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        serverDroneDeployed = false;

        if (tankDroneController != null)
            tankDroneController.ResetForDespawn();

        if (isOwned)
        {
            LocalPlayerTeam = TankTeam.Unassigned;
            MinimapMarker.SetLocalPlayerCamera(null);
            if (GameDirector.Instance != null)
                GameDirector.Instance.ClearLocalPlayerTank(this);
        }

        if (nameTagCanvas != null)
            Destroy(nameTagCanvas.gameObject);

        if (healthBarCanvas != null)
            Destroy(healthBarCanvas.gameObject);
    }

    [ClientCallback]
    private void OnHealthChanged(float newHealth)
    {
        if (tankHealth != null)
            tankHealth.SetReplicatedHealth(newHealth);

        UpdateHealthBar(newHealth);
    }

    public void SetServerHealth(float health)
    {
        if (!isServer)
            return;

        syncedHealth = Mathf.Max(0f, health);
    }

    public void SetServerIdentity(TankTeam team, string playerName)
    {
        if (!isServer || team == TankTeam.Unassigned)
            return;

        syncedTeam = (byte)team;
        syncedPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;

        if (isOwned)
            LocalPlayerTeam = team;

        UpdateNameTag();
    }

    public void RegisterEnemyTankDestroyed()
    {
        if (!isServer)
            return;

        syncedEnemyTanksDestroyed++;
    }

    public void NotifyMultiplayerTankDestroyed()
    {
        if (!isServer || GameDirector.Instance == null)
            return;

        GameDirector.Instance.NotifyMultiplayerTankDestroyed(this);
    }

    [ClientRpc]
    public void BroadcastMultiplayerMatchEndClientRpc(byte winningTeam)
    {
        if (GameDirector.Instance == null)
            return;

        GameDirector.Instance.EndMultiplayerMatch(
            (TankTeam)winningTeam
        );
    }

    public void PublishDroneState(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool deployed,
        bool destroyed)
    {
        if (!isOwned)
            return;

        CmdPublishDroneState(
            worldPosition,
            worldRotation,
            deployed,
            destroyed
        );
    }

    [Command]
    private void CmdPublishDroneState(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool deployed,
        bool destroyed)
    {
        if (!isOwned)
            return;

        serverDroneDeployed = deployed && !destroyed;
        RpcPublishDroneState(
            worldPosition,
            worldRotation,
            deployed,
            destroyed
        );
    }

    public void RequestDroneImpactDamage(TankHealth target)
    {
        if (target == null || !isOwned)
            return;

        NetworkIdentity targetNetworkIdentity =
            target.GetComponent<NetworkIdentity>();
        if (targetNetworkIdentity == null)
        {
            Debug.LogWarning(
                "NetworkTankPlayer: Drone impact target is not a spawned network object.",
                target
            );
            return;
        }

        CmdDroneImpactDamage(targetNetworkIdentity.netId);
    }

    public bool RequestAntiDroneMissile(NetworkTankPlayer target)
    {
        if (target == null || !isOwned ||
            !target.isOwned && !target.isServer || target.Team == Team)
        {
            return false;
        }

        CmdRequestAntiDroneMissile(
            target.netId
        );
        return true;
    }

    [Command]
    private void CmdRequestAntiDroneMissile(
        int targetNetworkIdentityId)
    {
        if (!isOwned)
            return;

        if (antiDetectDronelauncher == null)
            antiDetectDronelauncher =
                GetComponent<AntiDetectDronelauncher>();

        NetworkTankPlayer target = null;
        if (NetworkServer.spawned.TryGetValue(
                targetNetworkIdentityId,
                out NetworkIdentity targetNetworkIdentity))
        {
            target = targetNetworkIdentity.GetComponent<NetworkTankPlayer>();
        }

        Transform firePoint = antiDetectDronelauncher != null
            ? antiDetectDronelauncher.firePoint
            : null;
        if (target == null || target == this ||
            !target.HasTeam || target.Team == Team ||
            !target.serverDroneDeployed ||
            antiDetectDronelauncher == null ||
            antiDetectDronelauncher.homingMissilePrefab == null ||
            firePoint == null)
        {
            TargetSendAntiDroneLaunchResult(
                connectionToClient,
                false
            );
            return;
        }

        TankDronecontroll targetDroneController =
            target.GetComponent<TankDronecontroll>();
        if (targetDroneController == null ||
            targetDroneController.droneTransform == null)
        {
            TargetSendAntiDroneLaunchResult(
                connectionToClient,
                false
            );
            return;
        }

        if (!antiDetectDronelauncher.TryReserveServerLaunch(target))
        {
            TargetSendAntiDroneLaunchResult(
                connectionToClient,
                false
            );
            return;
        }

        float targetDistance = Vector3.Distance(
            firePoint.position,
            targetDroneController.droneTransform.position
        );
        Vector2 aimErrorDegrees =
            antiDetectDronelauncher.GetAimError(targetDistance);
        TargetSendAntiDroneLaunchResult(
            connectionToClient,
            true
        );
        RpcSpawnAntiDroneMissile(
            targetNetworkIdentityId,
            firePoint.position,
            firePoint.rotation,
            connectionToClient.connectionId,
            aimErrorDegrees
        );
    }

    [TargetRpc]
    private void TargetSendAntiDroneLaunchResult(
        NetworkConnection target,
        bool accepted)
    {
        if (antiDetectDronelauncher == null)
            return;

        antiDetectDronelauncher.ApplyServerLaunchResult(
            accepted,
            antiDetectDronelauncher.ServerAmmoRemaining,
            antiDetectDronelauncher.ServerRemainingReload
        );
    }

    [ClientRpc]
    private void RpcSpawnAntiDroneMissile(
        int targetNetworkIdentityId,
        Vector3 launchPosition,
        Quaternion launchRotation,
        int launchingClientId,
        Vector2 aimErrorDegrees)
    {
        if (antiDetectDronelauncher == null)
            antiDetectDronelauncher =
                GetComponent<AntiDetectDronelauncher>();

        if (antiDetectDronelauncher != null)
        {
            antiDetectDronelauncher.SpawnNetworkMissile(
                (ulong)targetNetworkIdentityId,
                launchPosition,
                launchRotation,
                (ulong)launchingClientId,
                aimErrorDegrees
            );
        }
    }

    public void RequestEnemyDroneMissileHit(
        NetworkTankPlayer target,
        Vector3 impactPosition)
    {
        if (target == null || !isOwned ||
            !target.isOwned && !target.isServer)
        {
            return;
        }

        CmdEnemyDroneMissileHit(
            target.netId,
            impactPosition
        );
    }

    public void DestroyDroneFromTankShell(Vector3 impactPosition)
    {
        if (!isServer)
            return;

        serverDroneDeployed = false;
        RpcDroneShellImpact(impactPosition);
    }

    [Command]
    private void CmdEnemyDroneMissileHit(
        int targetNetworkIdentityId,
        Vector3 impactPosition)
    {
        if (!isOwned)
            return;

        if (NetworkServer.spawned.TryGetValue(
                targetNetworkIdentityId,
                out NetworkIdentity targetNetworkIdentity))
        {
            NetworkTankPlayer target =
                targetNetworkIdentity.GetComponent<NetworkTankPlayer>();
            if (target == null || target == this ||
                !target.HasTeam || target.Team == Team ||
                !target.serverDroneDeployed)
            {
                return;
            }

            target.serverDroneDeployed = false;
            target.RpcDroneImpact(impactPosition);
        }
    }

    [ClientRpc]
    private void RpcDroneImpact(Vector3 impactPosition)
    {
        if (tankDroneController != null)
            tankDroneController.ApplyMissileImpact(impactPosition);
    }

    [ClientRpc]
    private void RpcDroneShellImpact(Vector3 impactPosition)
    {
        if (tankDroneController != null)
            tankDroneController.ApplyTankShellImpact(impactPosition);
    }

    [Command]
    private void CmdDroneImpactDamage(
        int targetNetworkIdentityId)
    {
        if (!isOwned)
            return;

        if (NetworkServer.spawned.TryGetValue(
                targetNetworkIdentityId,
                out NetworkIdentity targetNetworkIdentity))
        {
            TankHealth targetHealth =
                targetNetworkIdentity.GetComponent<TankHealth>();
            if (targetHealth != null)
                targetHealth.TakeDamage(100f, transform);
        }
    }

    [ClientRpc]
    private void RpcPublishDroneState(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool deployed,
        bool destroyed)
    {
        if (isOwned || tankDroneController == null)
            return;

        tankDroneController.ApplyRemoteState(
            worldPosition,
            worldRotation,
            deployed,
            destroyed
        );
    }

    [ClientRpc]
    public void RpcSetSpawnPosition(
        Vector3 position,
        Quaternion rotation)
    {
        if (!isOwned)
            return;

        transform.SetPositionAndRotation(position, rotation);

        Rigidbody tankRigidbody = GetComponent<Rigidbody>();
        if (tankRigidbody != null)
        {
            tankRigidbody.position = position;
            tankRigidbody.rotation = rotation;
            tankRigidbody.velocity = Vector3.zero;
            tankRigidbody.angularVelocity = Vector3.zero;
        }
    }

    [Command]
    public void CmdFireShell(
        float shellSpeed)
    {
        if (!isServer || tankFire == null ||
            tankFire.shellPrefab == null || tankFire.firePoint == null)
            return;

        GameObject shellObject = Instantiate(
            tankFire.shellPrefab,
            tankFire.firePoint.position,
            tankFire.firePoint.rotation
        );

        Rigidbody shellRigidbody = shellObject.GetComponent<Rigidbody>();
        if (shellRigidbody != null)
        {
            shellRigidbody.isKinematic = false;
            shellRigidbody.velocity = tankFire.firePoint.forward * shellSpeed;
        }

        TankShell shell = shellObject.GetComponent<TankShell>();
        if (shell != null)
            shell.SetOwner(transform, Team);

        NetworkServer.Spawn(shellObject);
    }

    private void UpdateHealthBar(float health)
    {
        if (healthBarFill == null || tankHealth == null)
            return;

        float normalizedHealth = tankHealth.maxHealth > 0f
            ? Mathf.Clamp01(health / tankHealth.maxHealth)
            : 0f;
        healthBarFillRect.sizeDelta = new Vector2(
            healthBarFillWidth * normalizedHealth,
            -8f
        );
        healthBarFill.color = Color.Lerp(
            Color.red,
            new Color(0.2f, 0.9f, 0.25f),
            normalizedHealth
        );
    }

    private void CreateHealthBar()
    {
        GameObject canvasObject = new GameObject(
            "Tank Health Bar",
            typeof(RectTransform),
            typeof(Canvas)
        );
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = Vector3.up * 3.8f;
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        healthBarCanvas = canvasObject.GetComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.overrideSorting = true;
        healthBarCanvas.sortingOrder = 19;
        healthBarCanvas.enabled =
            GameDirector.Instance == null || !GameDirector.Instance.IsPaused;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220f, 28f);

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        background.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        fillObject.transform.SetParent(backgroundObject.transform, false);

        healthBarFillRect = fillObject.GetComponent<RectTransform>();
        healthBarFillRect.anchorMin = new Vector2(0f, 0f);
        healthBarFillRect.anchorMax = new Vector2(0f, 1f);
        healthBarFillRect.pivot = new Vector2(0f, 0.5f);
        healthBarFillRect.anchoredPosition = new Vector2(4f, 0f);
        healthBarFillWidth = canvasRect.sizeDelta.x - 8f;
        healthBarFillRect.sizeDelta = new Vector2(healthBarFillWidth, -8f);

        healthBarFill = fillObject.GetComponent<Image>();
        healthBarFill.type = Image.Type.Simple;
        healthBarFill.raycastTarget = false;
    }

    public void SetHealthBarVisible(bool visible)
    {
        if (healthBarCanvas != null)
            healthBarCanvas.enabled = visible;
    }

    private void CreateNameTag()
    {
        GameObject canvasObject = new GameObject(
            "Tank Name Tag",
            typeof(RectTransform),
            typeof(Canvas)
        );
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = Vector3.up * 4.5f;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        nameTagCanvas = canvasObject.GetComponent<Canvas>();
        nameTagCanvas.renderMode = RenderMode.WorldSpace;
        nameTagCanvas.overrideSorting = true;
        nameTagCanvas.sortingOrder = 20;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400f, 80f);

        GameObject textObject = new GameObject(
            "Player Name",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        nameTagText = textObject.GetComponent<TextMeshProUGUI>();
        nameTagText.alignment = TextAlignmentOptions.Center;
        nameTagText.fontSize = 34f;
        nameTagText.enableAutoSizing = true;
        nameTagText.fontSizeMin = 20f;
        nameTagText.fontSizeMax = 34f;
        nameTagText.fontStyle = FontStyles.Bold;
        nameTagText.outlineWidth = 0.15f;
        nameTagText.outlineColor = Color.black;
        nameTagText.raycastTarget = false;
    }

    private void UpdateNameTag()
    {
        if (nameTagText == null)
            return;

        nameTagCanvas.enabled = !isOwned;
        nameTagText.text = PlayerName;
        nameTagText.color = Team == TankTeam.Red
            ? new Color(1f, 0.18f, 0.15f, 1f)
            : Team == TankTeam.Blue
                ? new Color(0.15f, 0.45f, 1f, 1f)
                : Color.white;
    }

    private void LateUpdate()
    {
        Camera viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        FaceCamera(nameTagCanvas, viewCamera);
        FaceCamera(healthBarCanvas, viewCamera);
    }

    private static void FaceCamera(Canvas canvas, Camera viewCamera)
    {
        if (canvas == null || !canvas.enabled)
            return;

        Transform canvasTransform = canvas.transform;
        Vector3 towardCamera =
            canvasTransform.position - viewCamera.transform.position;
        if (towardCamera.sqrMagnitude > 0.001f)
            canvasTransform.rotation = Quaternion.LookRotation(towardCamera);
    }

    private void Update()
    {
        if (isServer && turretControls != null)
        {
            if (isOwned)
            {
                if (Time.unscaledTime >= nextAimUpdate)
                {
                    nextAimUpdate = Time.unscaledTime + 0.05f;
                    if (turretControls.turret != null)
                        syncedTurretRotation =
                            turretControls.turret.localRotation;
                    if (turretControls.cannon != null)
                        syncedCannonRotation =
                            turretControls.cannon.localRotation;
                }
            }
            else
            {
                if (turretControls.turret != null)
                    turretControls.turret.localRotation =
                        syncedTurretRotation;
                if (turretControls.cannon != null)
                    turretControls.cannon.localRotation =
                        syncedCannonRotation;
            }
        }

        if (!isOwned || tankMovement == null ||
            Time.time < nextTankSpeedUpdate)
        {
            return;
        }

        nextTankSpeedUpdate = Time.time + 0.1f;
        syncedTankSpeed = tankMovement.currentSpeed;
    }

    private void SetLocalControls(bool isLocalPlayer)
    {
        if (tankMovement != null)
            tankMovement.enabled = isLocalPlayer;

        TankTurretMove turretControls = GetComponent<TankTurretMove>();
        if (turretControls != null)
            turretControls.enabled = isLocalPlayer;

        if (tankFire != null)
            tankFire.enabled = isLocalPlayer;

        AntiDroneLauncher missileLauncher =
            GetComponentInChildren<AntiDroneLauncher>();
        if (missileLauncher != null)
            missileLauncher.enabled = isLocalPlayer;

        TankScopeZoom scopeControls = GetComponentInChildren<TankScopeZoom>();
        if (scopeControls != null)
            scopeControls.enabled = isLocalPlayer;

        if (!isLocalPlayer)
        {
            foreach (Camera tankCamera in GetComponentsInChildren<Camera>(true))
                tankCamera.enabled = false;

            foreach (AudioListener audioListener in
                         GetComponentsInChildren<AudioListener>(true))
                audioListener.enabled = false;
        }
    }
}
