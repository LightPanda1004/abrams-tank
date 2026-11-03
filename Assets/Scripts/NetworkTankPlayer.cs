using TMPro;
using Unity.Collections;
using Unity.Netcode;
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
    private readonly NetworkVariable<float> syncedHealth = new NetworkVariable<float>(
        500f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<byte> syncedTeam = new NetworkVariable<byte>(
        (byte)TankTeam.Unassigned,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<FixedString64Bytes> syncedPlayerName =
        new NetworkVariable<FixedString64Bytes>(
            new FixedString64Bytes("Player"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    private readonly NetworkVariable<int> syncedEnemyTanksDestroyed =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    private readonly NetworkVariable<float> syncedTankSpeed =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private TankHealth tankHealth;
    private TankMovement tankMovement;
    private TankFire tankFire;
    private AntiDroneLauncher missileLauncher;
    private AntiDetectDronelauncher antiDetectDronelauncher;
    private TankDronecontroll tankDroneController;
    private TextMeshProUGUI nameTagText;
    private Canvas nameTagCanvas;
    private Canvas healthBarCanvas;
    private Image healthBarFill;
    private RectTransform healthBarFillRect;
    private float healthBarFillWidth;
    private float nextShellTime;
    private float nextTankSpeedUpdate;
    private bool serverDroneDeployed;

    public TankTeam Team => (TankTeam)syncedTeam.Value;
    public bool HasTeam => Team != TankTeam.Unassigned;
    public string PlayerName => syncedPlayerName.Value.ToString();
    public int EnemyTanksDestroyed => syncedEnemyTanksDestroyed.Value;
    public float DisplayedTankSpeed =>
        IsSpawned
            ? syncedTankSpeed.Value
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
        antiDetectDronelauncher =
            GetComponent<AntiDetectDronelauncher>();
        tankDroneController = GetComponent<TankDronecontroll>();
    }

    public override void OnNetworkSpawn()
    {
        syncedHealth.OnValueChanged += OnHealthChanged;
        syncedTeam.OnValueChanged += OnTeamChanged;
        syncedPlayerName.OnValueChanged += OnPlayerNameChanged;

        if (IsServer && tankHealth != null)
            syncedHealth.Value = tankHealth.CurrentHealth;

        if (IsServer && GameDirector.Instance != null)
            GameDirector.Instance.AssignIdentityToPlayer(this);

        SetLocalControls(IsOwner);
        if (tankDroneController != null)
            tankDroneController.Initialize(IsOwner);

        CreateNameTag();
        UpdateNameTag();

        if (IsOwner)
        {
            LocalPlayerTeam = Team;

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.SetLocalPlayerTank(this);
            }

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
            UpdateHealthBar(syncedHealth.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        serverDroneDeployed = false;
        syncedHealth.OnValueChanged -= OnHealthChanged;
        syncedTeam.OnValueChanged -= OnTeamChanged;
        syncedPlayerName.OnValueChanged -= OnPlayerNameChanged;

        if (tankDroneController != null)
            tankDroneController.ResetForDespawn();

        if (IsOwner)
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

    public void SetServerHealth(float health)
    {
        if (IsServer)
            syncedHealth.Value = Mathf.Max(0f, health);
    }

    public void SetServerIdentity(TankTeam team, string playerName)
    {
        if (!IsServer || team == TankTeam.Unassigned)
            return;

        syncedTeam.Value = (byte)team;
        syncedPlayerName.Value = new FixedString64Bytes(
            string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName);

        if (IsOwner)
            LocalPlayerTeam = team;

        UpdateNameTag();
    }

    public void RegisterEnemyTankDestroyed()
    {
        if (IsServer)
            syncedEnemyTanksDestroyed.Value++;
    }

    public void NotifyMultiplayerTankDestroyed()
    {
        if (!IsServer || !IsSpawned ||
            GameDirector.Instance == null)
        {
            return;
        }

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
        if (IsOwner && IsSpawned)
        {
            PublishDroneStateServerRpc(
                worldPosition,
                worldRotation,
                deployed,
                destroyed
            );
        }
    }

    [ServerRpc]
    private void PublishDroneStateServerRpc(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool deployed,
        bool destroyed,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        serverDroneDeployed = deployed && !destroyed;
        PublishDroneStateClientRpc(
            worldPosition,
            worldRotation,
            deployed,
            destroyed
        );
    }

    public void RequestDroneImpactDamage(TankHealth target)
    {
        if (target == null || !IsOwner || !IsSpawned)
            return;

        NetworkObject targetNetworkObject =
            target.GetComponent<NetworkObject>();
        if (targetNetworkObject == null || !targetNetworkObject.IsSpawned)
        {
            Debug.LogWarning(
                "NetworkTankPlayer: Drone impact target is not a spawned network object.",
                target
            );
            return;
        }

        DroneImpactDamageServerRpc(targetNetworkObject.NetworkObjectId);
    }

    public bool RequestAntiDroneMissile(NetworkTankPlayer target)
    {
        if (target == null || !IsOwner || !IsSpawned ||
            !target.IsSpawned || target.Team == Team)
        {
            return false;
        }

        RequestAntiDroneMissileServerRpc(
            target.NetworkObjectId
        );
        return true;
    }

    [ServerRpc]
    private void RequestAntiDroneMissileServerRpc(
        ulong targetNetworkObjectId,
        ServerRpcParams rpcParams = default)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!IsServer || !IsSpawned ||
            rpcParams.Receive.SenderClientId != OwnerClientId ||
            networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        if (antiDetectDronelauncher == null)
            antiDetectDronelauncher =
                GetComponent<AntiDetectDronelauncher>();

        NetworkTankPlayer target = null;
        if (networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId,
                out NetworkObject targetNetworkObject))
        {
            target = targetNetworkObject.GetComponent<NetworkTankPlayer>();
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
            SendAntiDroneLaunchResult(
                rpcParams.Receive.SenderClientId,
                false
            );
            return;
        }

        TankDronecontroll targetDroneController =
            target.GetComponent<TankDronecontroll>();
        if (targetDroneController == null ||
            targetDroneController.droneTransform == null)
        {
            SendAntiDroneLaunchResult(
                rpcParams.Receive.SenderClientId,
                false
            );
            return;
        }

        if (!antiDetectDronelauncher.TryReserveServerLaunch(target))
        {
            SendAntiDroneLaunchResult(
                rpcParams.Receive.SenderClientId,
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
        SendAntiDroneLaunchResult(
            rpcParams.Receive.SenderClientId,
            true
        );
        SpawnAntiDroneMissileClientRpc(
            targetNetworkObjectId,
            firePoint.position,
            firePoint.rotation,
            OwnerClientId,
            aimErrorDegrees
        );
    }

    private void SendAntiDroneLaunchResult(
        ulong clientId,
        bool accepted)
    {
        if (antiDetectDronelauncher == null)
            return;

        AntiDroneLaunchResultClientRpc(
            accepted,
            antiDetectDronelauncher.ServerAmmoRemaining,
            antiDetectDronelauncher.ServerRemainingReload,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            }
        );
    }

    [ClientRpc]
    private void AntiDroneLaunchResultClientRpc(
        bool accepted,
        int serverAmmo,
        float serverReload,
        ClientRpcParams clientRpcParams = default)
    {
        if (antiDetectDronelauncher == null)
            antiDetectDronelauncher =
                GetComponent<AntiDetectDronelauncher>();

        if (antiDetectDronelauncher != null)
        {
            antiDetectDronelauncher.ApplyServerLaunchResult(
                accepted,
                serverAmmo,
                serverReload
            );
        }
    }

    [ClientRpc]
    private void SpawnAntiDroneMissileClientRpc(
        ulong targetNetworkObjectId,
        Vector3 launchPosition,
        Quaternion launchRotation,
        ulong launchingClientId,
        Vector2 aimErrorDegrees)
    {
        if (antiDetectDronelauncher == null)
            antiDetectDronelauncher =
                GetComponent<AntiDetectDronelauncher>();

        if (antiDetectDronelauncher != null)
        {
            antiDetectDronelauncher.SpawnNetworkMissile(
                targetNetworkObjectId,
                launchPosition,
                launchRotation,
                launchingClientId,
                aimErrorDegrees
            );
        }
    }

    public void RequestEnemyDroneMissileHit(
        NetworkTankPlayer target,
        Vector3 impactPosition)
    {
        if (target == null || !IsOwner || !IsSpawned ||
            !target.IsSpawned)
        {
            return;
        }

        EnemyDroneMissileHitServerRpc(
            target.NetworkObjectId,
            impactPosition
        );
    }

    public void DestroyDroneFromTankShell(Vector3 impactPosition)
    {
        if (!IsServer || !IsSpawned)
            return;

        serverDroneDeployed = false;
        DroneShellImpactClientRpc(impactPosition);
    }

    [ServerRpc]
    private void EnemyDroneMissileHitServerRpc(
        ulong targetNetworkObjectId,
        Vector3 impactPosition,
        ServerRpcParams rpcParams = default)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!IsServer || !IsSpawned ||
            rpcParams.Receive.SenderClientId != OwnerClientId ||
            networkManager == null || !networkManager.IsListening ||
            !networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId,
                out NetworkObject targetNetworkObject))
        {
            return;
        }

        NetworkTankPlayer target =
            targetNetworkObject.GetComponent<NetworkTankPlayer>();
        if (target == null || target == this ||
            !target.HasTeam || target.Team == Team ||
            !target.serverDroneDeployed)
        {
            return;
        }

        target.serverDroneDeployed = false;
        target.DroneImpactClientRpc(impactPosition);
    }

    [ClientRpc]
    private void DroneImpactClientRpc(Vector3 impactPosition)
    {
        if (tankDroneController != null)
            tankDroneController.ApplyMissileImpact(impactPosition);
    }

    [ClientRpc]
    private void DroneShellImpactClientRpc(Vector3 impactPosition)
    {
        if (tankDroneController != null)
            tankDroneController.ApplyTankShellImpact(impactPosition);
    }

    [ServerRpc]
    private void DroneImpactDamageServerRpc(
        ulong targetNetworkObjectId,
        ServerRpcParams rpcParams = default)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!IsServer || !IsSpawned || !serverDroneDeployed ||
            rpcParams.Receive.SenderClientId != OwnerClientId ||
            networkManager == null || !networkManager.IsListening ||
            !networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId,
                out NetworkObject targetNetworkObject))
        {
            return;
        }

        TankHealth targetHealth =
            targetNetworkObject.GetComponent<TankHealth>();
        if (targetHealth != null)
            targetHealth.TakeDamage(100f);
    }

    [ClientRpc]
    private void PublishDroneStateClientRpc(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool deployed,
        bool destroyed)
    {
        if (IsOwner || tankDroneController == null)
            return;

        tankDroneController.ApplyRemoteState(
            worldPosition,
            worldRotation,
            deployed,
            destroyed
        );
    }

    [ClientRpc]
    public void SetSpawnPositionClientRpc(
        Vector3 position,
        Quaternion rotation,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
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

    [ServerRpc]
    public void FireShellServerRpc(
        float shellSpeed,
        ServerRpcParams rpcParams = default)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!IsSpawned || networkManager == null ||
            !networkManager.IsListening || !IsServer ||
            Time.time < nextShellTime || tankFire == null ||
            tankFire.shellPrefab == null || tankFire.firePoint == null)
            return;

        nextShellTime = Time.time + Mathf.Max(0.1f, tankFire.fireCooldown);

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

        NetworkObject shellNetworkObject =
            shellObject.GetComponent<NetworkObject>();

        if (shellNetworkObject == null)
        {
            Debug.LogError("The shell prefab needs a NetworkObject component.");
            Destroy(shellObject);
            return;
        }

        shellNetworkObject.Spawn();
    }

    private void OnHealthChanged(float previousHealth, float newHealth)
    {
        if (!IsServer && tankHealth != null)
            tankHealth.SetReplicatedHealth(newHealth);

        UpdateHealthBar(newHealth);
    }

    private void OnTeamChanged(byte previousTeam, byte newTeam)
    {
        if (IsOwner)
            LocalPlayerTeam = (TankTeam)newTeam;

        UpdateNameTag();
    }

    private void OnPlayerNameChanged(
        FixedString64Bytes previousName,
        FixedString64Bytes newName)
    {
        UpdateNameTag();
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
        if (!IsSpawned || !IsOwner || tankMovement == null ||
            Time.time < nextTankSpeedUpdate)
        {
            return;
        }

        nextTankSpeedUpdate = Time.time + 0.1f;
        syncedTankSpeed.Value = tankMovement.currentSpeed;
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
