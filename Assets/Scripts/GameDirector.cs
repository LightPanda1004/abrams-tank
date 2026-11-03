
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum GameMode
    {
        SinglePlay,
        Multiplayer
    }

    [Header("Game Mode")]
    public GameMode currentGameMode = GameMode.SinglePlay;

    [Header("Battle Statistics")]
    public int totalAITanks = 0;
    public int destroyedTanks = 0;

    [Header("Player")]
    public TankHealth playerHealth;
    public TankMovement playerMovement;
    public GameObject multiplayerTankPrefab;

    [Header("Player HUD")]
    public float maximumSpeedDisplay = 10f;

    [Header("AI Objects")]
    [Tooltip("Add AI tank objects, AI drone objects, and AI spawn managers here.")]
    public GameObject[] aiObjectsToDisableForMultiplayer;

    [Header("Turret Controls")]
    public bool MouseTurretControlEnabled = true;

    [Header("Startup")]
    public bool showMainMenu = true;

    [Header("Game Over")]
    public float finishMenuDelay = 3f;

    private readonly HashSet<int> registeredTanks = new HashSet<int>();
    private readonly HashSet<int> destroyedTankIds = new HashSet<int>();

    private bool gameOver;
    private bool victory;
    private TankTeam multiplayerWinningTeam = TankTeam.Unassigned;
    private bool finishMenuVisible;
    private bool multiplayerMatchEnded;

    private bool showPauseMenu;
    private bool showSettings;
    private bool showControlSettings;
    private bool showMultiplayerMenu;
    private bool showTeamSelection;
    private bool showCreateRoomMenu;
    private bool showJoinRoomMenu;

    private bool isFullscreen;
    private int selectedResolution = 1;

    private readonly Vector2Int[] resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440)
    };

    private readonly Dictionary<string, Texture2D> ringTextures =
        new Dictionary<string, Texture2D>();

    private TankFire playerTankFire;
    private AntiDroneLauncher playerMissileLauncher;
    private AntiDetectDronelauncher playerAntiDetectLauncher;
    private NetworkTankPlayer localNetworkTankPlayer;
    private int localPlayerEnemyTanksDestroyed;
    private TankHealth scenePlayerHealth;
    private TankMovement scenePlayerMovement;
    private Camera missileViewCamera;
    private bool missileViewVisible;
    private bool missileViewBlack;
    private LanRoomDiscovery lanRoomDiscovery;
    private NetworkManager lanNetworkManager;
    private readonly Dictionary<ulong, TankTeam> approvedClientTeams =
        new Dictionary<ulong, TankTeam>();
    private readonly Dictionary<ulong, string> approvedClientNames =
        new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, Vector3> approvedClientSpawnPositions =
        new Dictionary<ulong, Vector3>();
    private TankTeam selectedTeam = TankTeam.Unassigned;
    private string playerNameInput = "Player";
    private string roomNameInput = "Abrams Room";
    private string roomStatusMessage = "";
    private Vector2 roomListScroll;

    private const ushort LanGamePort = 7777;
    private const int LanMaxPlayers = 4;
    private const uint LegacyShellPrefabHashBeforeLastUpdate = 1732461695u;
    private const uint LegacyShellPrefabHashBeforePrefabRefresh = 2144552063u;
    private const float MultiplayerSpawnMinDistance = 15f;
    private const float MultiplayerSpawnAreaEdgePadding = 8f;
    private const float MultiplayerSpawnBuildingPadding = 8f;
    private const float MultiplayerSpawnMaximumBuildingPadding = 20f;

    private GUIStyle statsStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle hudPanelStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle hudMeterValueStyle;
    private GUIStyle hudBarBackgroundStyle;
    private GUIStyle hudBarFillStyle;
    private GUIStyle hudFillBlueStyle;
    private GUIStyle hudDangerPanelStyle;
    private GUIStyle hudDangerFillStyle;
    private GUIStyle hudBadgeStyle;

    public bool IsGameOver => gameOver;
    public bool IsPaused => showPauseMenu || showMainMenu || finishMenuVisible;

    public bool IsMultiplayerMode =>
        currentGameMode == GameMode.Multiplayer;

    public Camera ActiveMissileViewCamera =>
        missileViewVisible && !missileViewBlack &&
        missileViewCamera != null && missileViewCamera.isActiveAndEnabled
            ? missileViewCamera
            : null;

    public bool TryGetMissileViewRect(out Rect viewRect)
    {
        viewRect = default;
        if (!missileViewVisible || missileViewCamera == null ||
            missileViewCamera.targetTexture == null)
        {
            return false;
        }

        const float viewWidth = 512f;
        const float viewHeight = 256f;
        const float borderSize = 10f;
        const float screenMargin = 20f;

        float scale = Mathf.Min(
            1f,
            (Screen.width - screenMargin * 2f) /
                (viewWidth + borderSize * 2f),
            (Screen.height - screenMargin * 2f) /
                (viewHeight + borderSize * 2f)
        );

        float scaledBorder = borderSize * scale;
        float scaledWidth = viewWidth * scale;
        float scaledHeight = viewHeight * scale;
        viewRect = new Rect(
            Screen.width - scaledWidth -
                scaledBorder * 2f - screenMargin + scaledBorder,
            screenMargin + scaledBorder,
            scaledWidth,
            scaledHeight
        );
        return true;
    }

    private int LocalPlayerEnemyTanksDestroyed =>
        localNetworkTankPlayer != null
            ? localNetworkTankPlayer.EnemyTanksDestroyed
            : localPlayerEnemyTanksDestroyed;

    public Transform MultiplayerSpawnAnchor =>
        scenePlayerHealth != null
            ? scenePlayerHealth.transform
            : playerHealth != null
                ? playerHealth.transform
                : null;

    public void SetLocalPlayerTank(NetworkTankPlayer tank)
    {
        if (tank == null)
            return;

        playerHealth = tank.GetComponent<TankHealth>();
        playerMovement = tank.GetComponent<TankMovement>();
        playerTankFire = tank.GetComponent<TankFire>();
        playerMissileLauncher =
            tank.GetComponentInChildren<AntiDroneLauncher>(true);
        playerAntiDetectLauncher =
            tank.GetComponentInChildren<AntiDetectDronelauncher>(true);
        localNetworkTankPlayer = tank;
        localPlayerEnemyTanksDestroyed = tank.EnemyTanksDestroyed;
    }

    public void ClearLocalPlayerTank(NetworkTankPlayer tank)
    {
        if (tank == null || playerHealth != tank.GetComponent<TankHealth>())
            return;

        localPlayerEnemyTanksDestroyed = tank.EnemyTanksDestroyed;
        playerHealth = scenePlayerHealth;
        playerMovement = scenePlayerMovement;
        playerTankFire = playerHealth != null
            ? playerHealth.GetComponent<TankFire>()
            : null;
        playerMissileLauncher = playerHealth != null
            ? playerHealth.GetComponentInChildren<AntiDroneLauncher>(true)
            : null;
        playerAntiDetectLauncher = null;
        localNetworkTankPlayer = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        isFullscreen = Screen.fullScreen;

        lanRoomDiscovery = GetComponent<LanRoomDiscovery>();
        if (lanRoomDiscovery == null)
            lanRoomDiscovery = gameObject.AddComponent<LanRoomDiscovery>();

        if (playerHealth == null)
            playerHealth = FindObjectOfType<TankHealth>();

        if (playerMovement == null)
            playerMovement = FindObjectOfType<TankMovement>();

        if (playerTankFire == null)
            playerTankFire = FindObjectOfType<TankFire>();

        if (playerMissileLauncher == null)
            playerMissileLauncher = FindObjectOfType<AntiDroneLauncher>();

        scenePlayerHealth = playerHealth;
        scenePlayerMovement = playerMovement;

        if (showMainMenu)
        {
            Time.timeScale = 0f;
            SetCursor(true);
        }
        else
        {
            Time.timeScale = 1f;
            SetCursor(false);
        }
    }

    private void Update()
    {
        if (missileViewCamera != null &&
            Input.GetKeyDown(KeyCode.C))
        {
            missileViewVisible = !missileViewVisible;
            missileViewCamera.enabled =
                missileViewVisible && !missileViewBlack;
        }

        if (gameOver || showMainMenu)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (showSettings || showControlSettings)
            {
                showSettings = false;
                showControlSettings = false;
                showPauseMenu = true;
                Time.timeScale = 0f;
                SetCursor(true);
            }
            else if (showPauseMenu)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    private void SetCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;
    }

    public void RegisterMissileViewCamera(Camera camera)
    {
        if (camera == null)
            return;

        if (missileViewCamera != null && missileViewCamera != camera)
            missileViewCamera.enabled = false;

        missileViewCamera = camera;
        missileViewVisible = false;
        missileViewBlack = false;
        missileViewCamera.enabled = false;

        AudioListener listener = missileViewCamera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    public void BeginMissileViewImpact(Camera camera, float explosionViewDuration)
    {
        if (camera == null)
            return;

        camera.transform.SetParent(null, true);
        StartCoroutine(MissileViewImpactRoutine(camera, explosionViewDuration));
    }

    public void ClearMissileViewCamera(Camera camera)
    {
        if (missileViewCamera != camera)
            return;

        missileViewCamera = null;
        missileViewVisible = false;
        missileViewBlack = false;
    }

    private IEnumerator MissileViewImpactRoutine(
        Camera camera,
        float explosionViewDuration)
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, explosionViewDuration)
        );

        if (missileViewCamera == camera)
        {
            missileViewBlack = true;
            camera.enabled = false;
        }

        yield return new WaitForSecondsRealtime(1f);

        if (missileViewCamera == camera)
            ClearMissileViewCamera(camera);

        if (camera != null)
            Destroy(camera.gameObject);
    }

    // --------------------------------------------------
    // AI REGISTRATION AND DESTRUCTION
    // --------------------------------------------------

    public void RegisterAITank(TankHealth tank)
    {
        if (tank == null || tank.isPlayerTank)
            return;

        if (IsMultiplayerMode)
            return;

        int id = tank.gameObject.GetInstanceID();

        if (registeredTanks.Add(id))
        {
            totalAITanks++;

            Debug.Log("AI tanks registered: " + totalAITanks);
        }
    }

    private void RefreshAITankCounts()
    {
        registeredTanks.Clear();
        destroyedTankIds.Clear();
        destroyedTanks = 0;
        totalAITanks = 0;

        TankHealth[] allTanks = FindObjectsOfType<TankHealth>(true);

        foreach (TankHealth tank in allTanks)
        {
            if (tank == null || tank.isPlayerTank)
                continue;

            int id = tank.gameObject.GetInstanceID();

            if (registeredTanks.Add(id))
            {
                totalAITanks++;
            }
        }
    }

    public void NotifyDestroyed(TankHealth tank)
    {
        if (tank == null)
            return;

        if (tank.isPlayerTank)
        {
            if (IsMultiplayerMode)
                return;

            NotifyPlayerDestroyed();
            return;
        }

        if (IsMultiplayerMode)
            return;

        int id = tank.gameObject.GetInstanceID();

        if (!registeredTanks.Contains(id))
            return;

        if (!destroyedTankIds.Add(id))
            return;

        destroyedTanks = destroyedTankIds.Count;

        Debug.Log(
            "Destroyed AI tanks: " + destroyedTanks +
            " / " + totalAITanks
        );

        if (totalAITanks > 0 &&
            destroyedTanks >= totalAITanks)
        {
            EndGame(true);
        }
    }

    public void NotifyMultiplayerTankDestroyed(
        NetworkTankPlayer destroyedPlayer)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (!IsMultiplayerMode || multiplayerMatchEnded ||
            destroyedPlayer == null || manager == null ||
            !manager.IsServer || !manager.IsListening)
        {
            return;
        }

        bool blueHasSurvivors = false;
        bool redHasSurvivors = false;
        foreach (NetworkObject networkObject in
                 manager.SpawnManager.SpawnedObjects.Values)
        {
            NetworkTankPlayer player =
                networkObject.GetComponent<NetworkTankPlayer>();
            TankHealth health =
                networkObject.GetComponent<TankHealth>();
            if (player == null || health == null ||
                health.IsDestroyed || health.CurrentHealth <= 0f)
            {
                continue;
            }

            if (player.Team == TankTeam.Blue)
                blueHasSurvivors = true;
            else if (player.Team == TankTeam.Red)
                redHasSurvivors = true;
        }

        TankTeam losingTeam = destroyedPlayer.Team;
        if ((losingTeam == TankTeam.Blue && blueHasSurvivors) ||
            (losingTeam == TankTeam.Red && redHasSurvivors) ||
            (!blueHasSurvivors && !redHasSurvivors))
        {
            return;
        }

        multiplayerMatchEnded = true;
        destroyedPlayer.BroadcastMultiplayerMatchEndClientRpc(
            (byte)(losingTeam == TankTeam.Blue
                ? TankTeam.Red
                : TankTeam.Blue)
        );
    }

    public void EndMultiplayerMatch(TankTeam winningTeam)
    {
        if (!IsMultiplayerMode || gameOver ||
            (winningTeam != TankTeam.Blue &&
             winningTeam != TankTeam.Red))
        {
            return;
        }

        gameOver = true;
        multiplayerWinningTeam = winningTeam;
        if (localNetworkTankPlayer != null)
        {
            localPlayerEnemyTanksDestroyed =
                localNetworkTankPlayer.EnemyTanksDestroyed;
        }
        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;
        finishMenuVisible = false;
        SetCursor(true);
        StartCoroutine(ShowFinishMenuAfterDelay());
    }

    // --------------------------------------------------
    // GAME OVER
    // --------------------------------------------------

    public void NotifyPlayerDestroyed()
    {
        EndGame(false);
    }

    private void EndGame(bool playerWon)
    {
        if (gameOver)
            return;

        gameOver = true;
        victory = playerWon;

        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;
        finishMenuVisible = false;

        SetCursor(true);

        // Do not pause immediately.
        // Allow the final explosion to play first.
        StartCoroutine(ShowFinishMenuAfterDelay());
    }

    private IEnumerator ShowFinishMenuAfterDelay()
    {
        yield return new WaitForSecondsRealtime(finishMenuDelay);

        finishMenuVisible = true;
        Time.timeScale = 0f;

        Debug.Log("Finish menu displayed.");
    }

    // --------------------------------------------------
    // GAME START
    // --------------------------------------------------

    private void StartGame(GameMode selectedMode)
    {
        currentGameMode = selectedMode;

        if (selectedMode == GameMode.SinglePlay)
        {
            RestoreScenePlayerAndAI();
        }

        totalAITanks = 0;
        destroyedTanks = 0;

        registeredTanks.Clear();
        destroyedTankIds.Clear();
        RefreshAITankCounts();

        gameOver = false;
        victory = false;
        localPlayerEnemyTanksDestroyed = 0;
        multiplayerWinningTeam = TankTeam.Unassigned;
        finishMenuVisible = false;
        multiplayerMatchEnded = false;

        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;

        // Enable AI for Single Play.
        // Disable AI for Multiplayer.
        bool enableAI = selectedMode == GameMode.SinglePlay;

        if (aiObjectsToDisableForMultiplayer != null)
        {
            foreach (GameObject aiObject in
                     aiObjectsToDisableForMultiplayer)
            {
                if (aiObject != null)
                    aiObject.SetActive(enableAI);
            }
        }

        Time.timeScale = 1f;
        SetCursor(false);

        Debug.Log("Game mode: " + selectedMode);
    }

    private void CreateLanRoom()
    {
        if (selectedTeam == TankTeam.Unassigned)
        {
            roomStatusMessage = "Select a team before creating a room.";
            return;
        }

        if (multiplayerTankPrefab == null)
        {
            roomStatusMessage = "Assign the multiplayer tank prefab on GameDirector.";
            return;
        }

        PrepareMultiplayerScene();
        lanNetworkManager = CreateLanNetworkManager();

        UnityTransport transport =
            lanNetworkManager.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", LanGamePort, "0.0.0.0");
        lanNetworkManager.NetworkConfig.ConnectionData =
            CreateConnectionPayload();

        if (!lanNetworkManager.StartHost())
        {
            roomStatusMessage = "Could not start the LAN host.";
            CleanupLanNetworkManager();
            RestoreScenePlayerAndAI();
            return;
        }

        lanRoomDiscovery.StartAdvertising(
            string.IsNullOrWhiteSpace(roomNameInput)
                ? "Abrams Room"
                : roomNameInput.Trim(),
            LanGamePort,
            LanMaxPlayers,
            () => lanNetworkManager != null && lanNetworkManager.IsServer
                ? lanNetworkManager.ConnectedClients.Count
                : 1
        );

        roomStatusMessage = "Room created. Waiting for players...";
    }

    private void JoinLanRoom(LanRoomInfo room)
    {
        if (selectedTeam == TankTeam.Unassigned)
        {
            roomStatusMessage = "Select a team before joining a room.";
            return;
        }

        if (room == null || multiplayerTankPrefab == null)
        {
            roomStatusMessage = "This room cannot be joined.";
            return;
        }

        if (room.playerCount >= room.maxPlayers)
        {
            roomStatusMessage = "That room is full.";
            return;
        }

        PrepareMultiplayerScene();
        lanNetworkManager = CreateLanNetworkManager();

        UnityTransport transport =
            lanNetworkManager.GetComponent<UnityTransport>();
        transport.SetConnectionData(room.address, (ushort)room.gamePort);
        lanNetworkManager.NetworkConfig.ConnectionData =
            CreateConnectionPayload();

        if (!lanNetworkManager.StartClient())
        {
            roomStatusMessage = "Could not connect to that room.";
            CleanupLanNetworkManager();
            RestoreScenePlayerAndAI();
            return;
        }

        roomStatusMessage = "Connecting to " + room.roomName + "...";
    }

    private void CleanupLanNetworkManager()
    {
        if (lanNetworkManager == null)
            return;

        NetworkManager manager = lanNetworkManager;
        lanNetworkManager = null;
        manager.OnClientConnectedCallback -= OnLanClientConnected;
        manager.OnClientDisconnectCallback -= OnLanClientDisconnected;

        if (manager.IsListening)
            manager.Shutdown();

        Destroy(manager.gameObject);
    }

    private NetworkManager CreateLanNetworkManager()
    {
        GameObject managerObject = new GameObject("LAN Network Manager");
        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig();
        UnityTransport transport = managerObject.AddComponent<UnityTransport>();

        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.PlayerPrefab = multiplayerTankPrefab;
        manager.NetworkConfig.TickRate = 30;
        manager.NetworkConfig.EnableSceneManagement = true;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = ApproveLanClient;
        manager.AddNetworkPrefab(multiplayerTankPrefab);

        TankFire prefabTankFire = multiplayerTankPrefab.GetComponent<TankFire>();
        if (prefabTankFire != null && prefabTankFire.shellPrefab != null)
        {
            manager.AddNetworkPrefab(prefabTankFire.shellPrefab);
            // These hashes were reported for shells during late-join synchronization.
            AddLegacyShellPrefabHash(
                manager,
                prefabTankFire.shellPrefab,
                LegacyShellPrefabHashBeforeLastUpdate
            );
            AddLegacyShellPrefabHash(
                manager,
                prefabTankFire.shellPrefab,
                LegacyShellPrefabHashBeforePrefabRefresh
            );
        }

        manager.OnClientConnectedCallback += OnLanClientConnected;
        manager.OnClientDisconnectCallback += OnLanClientDisconnected;
        return manager;
    }

    private static void AddLegacyShellPrefabHash(
        NetworkManager manager,
        GameObject shellPrefab,
        uint legacyHash)
    {
        NetworkPrefab legacyShellPrefab = new NetworkPrefab
        {
            Override = NetworkPrefabOverride.Hash,
            SourceHashToOverride = legacyHash,
            Prefab = shellPrefab,
            OverridingTargetPrefab = shellPrefab
        };

        if (!manager.NetworkConfig.Prefabs.Add(legacyShellPrefab))
        {
            Debug.LogError(
                $"Could not register legacy shell prefab hash {legacyHash}."
            );
        }
    }

    private void ApproveLanClient(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        bool hasCapacity = lanNetworkManager == null ||
            lanNetworkManager.ConnectedClients.Count < LanMaxPlayers;
        bool isLocalHost = lanNetworkManager != null &&
            lanNetworkManager.IsHost &&
            request.ClientNetworkId == lanNetworkManager.LocalClientId;
        bool hasPayloadTeam = request.Payload != null &&
            request.Payload.Length >= 1 &&
            (request.Payload[0] == (byte)TankTeam.Blue ||
             request.Payload[0] == (byte)TankTeam.Red);
        TankTeam approvedTeam = hasPayloadTeam
            ? (TankTeam)request.Payload[0]
            : isLocalHost
                ? selectedTeam
                : TankTeam.Unassigned;
        bool hasValidTeam = approvedTeam == TankTeam.Blue ||
            approvedTeam == TankTeam.Red;

        response.Approved = hasCapacity && hasValidTeam;
        response.CreatePlayerObject = response.Approved;

        if (!response.Approved)
        {
            response.Reason = !hasCapacity
                ? "The room is full."
                : "Select a team before joining.";
            return;
        }

        approvedClientTeams[request.ClientNetworkId] =
            approvedTeam;
        approvedClientNames[request.ClientNetworkId] =
            hasPayloadTeam && request.Payload.Length > 1
                ? SanitizePlayerName(Encoding.UTF8.GetString(
                    request.Payload, 1, request.Payload.Length - 1))
                : SanitizePlayerName(playerNameInput);
        Vector3 spawnPosition = GetRandomMultiplayerSpawnPosition();
        approvedClientSpawnPositions[request.ClientNetworkId] =
            spawnPosition;
        response.Position = spawnPosition;
        response.Rotation = Quaternion.Euler(
            0f, Random.Range(0f, 360f), 0f);
    }

    public void AssignIdentityToPlayer(NetworkTankPlayer player)
    {
        if (player == null || lanNetworkManager == null ||
            !lanNetworkManager.IsServer)
            return;

        TankTeam team;
        if (!approvedClientTeams.TryGetValue(
                player.OwnerClientId, out team))
        {
            if (player.OwnerClientId == lanNetworkManager.LocalClientId)
                team = selectedTeam;
            else
            {
                Debug.LogError(
                    "No approved team was recorded for network client " +
                    player.OwnerClientId + ".",
                    player);
                return;
            }
        }

        string playerName;
        if (!approvedClientNames.TryGetValue(
                player.OwnerClientId, out playerName))
            playerName = "Player";

        player.SetServerIdentity(team, playerName);
    }

    private byte[] CreateConnectionPayload()
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(
            SanitizePlayerName(playerNameInput));
        byte[] payload = new byte[nameBytes.Length + 1];
        payload[0] = (byte)selectedTeam;
        System.Buffer.BlockCopy(nameBytes, 0, payload, 1, nameBytes.Length);
        return payload;
    }

    private string SanitizePlayerName(string playerName)
    {
        string trimmedName = string.IsNullOrWhiteSpace(playerName)
            ? "Player"
            : playerName.Trim();
        StringBuilder safeName = new StringBuilder(trimmedName.Length);
        foreach (char character in trimmedName)
        {
            if (char.IsControl(character))
                continue;

            safeName.Append(character);
        }

        string cleanedName = safeName.Length > 0
            ? safeName.ToString()
            : "Player";
        byte[] encodedName = Encoding.UTF8.GetBytes(cleanedName);
        int byteLength = Mathf.Min(encodedName.Length, 48);
        while (byteLength > 0)
        {
            string truncatedName = Encoding.UTF8.GetString(
                encodedName, 0, byteLength);
            if (Encoding.UTF8.GetByteCount(truncatedName) <= 48)
                return truncatedName;

            byteLength--;
        }

        return "Player";
    }

    private Vector3 GetRandomMultiplayerSpawnPosition()
    {
        Transform anchor = MultiplayerSpawnAnchor;
        Vector3 center = anchor != null ? anchor.position : Vector3.zero;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Terrain[] terrains = FindObjectsOfType<Terrain>();
            if (terrains.Length > 0)
                terrain = terrains[0];
        }

        if (TryGetFenceEnclosedBounds(out Bounds fencedArea))
        {
            float minimumX =
                fencedArea.min.x + MultiplayerSpawnAreaEdgePadding;
            float maximumX =
                fencedArea.max.x - MultiplayerSpawnAreaEdgePadding;
            float minimumZ =
                fencedArea.min.z + MultiplayerSpawnAreaEdgePadding;
            float maximumZ =
                fencedArea.max.z - MultiplayerSpawnAreaEdgePadding;

            if (minimumX < maximumX && minimumZ < maximumZ)
            {
                Vector3 lastPosition = new Vector3(
                    fencedArea.center.x, 0f, fencedArea.center.z);
                for (int attempt = 0; attempt < 160; attempt++)
                {
                    Vector3 candidate = new Vector3(
                        Random.Range(minimumX, maximumX),
                        0f,
                        Random.Range(minimumZ, maximumZ)
                    );
                    lastPosition = candidate;

                    if (!IsMultiplayerSpawnPositionClear(candidate) ||
                        IsTooCloseToConnectedPlayer(candidate))
                        continue;

                    return candidate;
                }

                Debug.LogWarning(
                    "Could not find a clear multiplayer spawn inside the " +
                    "fence bounds; using an in-bounds random position.",
                    this);
                return lastPosition;
            }

            Debug.LogWarning(
                "The fence bounds are too small for multiplayer spawning; " +
                "using nearby scene objects instead.",
                this);
        }

        List<Bounds> landmarks = new List<Bounds>();
        Collider[] colliders = FindObjectsOfType<Collider>();
        foreach (Collider worldCollider in colliders)
        {
            if (worldCollider == null || !worldCollider.enabled ||
                worldCollider.isTrigger ||
                worldCollider.GetComponentInParent<Terrain>() != null ||
                !IsStaticWorldObject(worldCollider.transform) ||
                IsPlayerOrDynamicObject(worldCollider.transform))
                continue;

            landmarks.Add(worldCollider.bounds);
        }

        if (landmarks.Count == 0)
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (Renderer worldRenderer in renderers)
            {
                if (worldRenderer == null || !worldRenderer.enabled ||
                    worldRenderer.GetComponentInParent<Terrain>() != null ||
                    !IsStaticWorldObject(worldRenderer.transform) ||
                    IsPlayerOrDynamicObject(worldRenderer.transform))
                    continue;

                landmarks.Add(worldRenderer.bounds);
            }
        }

        if (landmarks.Count == 0)
        {
            Debug.LogError(
                "No static scene objects were found for multiplayer spawn placement.",
                this);
            return new Vector3(center.x, 0f, center.z);
        }

        Vector3 terrainOrigin = terrain != null
            ? terrain.transform.position
            : Vector3.zero;
        Vector3 terrainSize = terrain != null && terrain.terrainData != null
            ? terrain.terrainData.size
            : Vector3.zero;
        Vector3 lastCandidate = new Vector3(center.x, 0f, center.z);
        bool foundCandidateOnTerrain = terrain == null;

        for (int attempt = 0; attempt < 120; attempt++)
        {
            Bounds landmark = landmarks[Random.Range(0, landmarks.Count)];
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;
            direction.Normalize();

            float landmarkExtent =
                Mathf.Abs(direction.x) * landmark.extents.x +
                Mathf.Abs(direction.y) * landmark.extents.z;
            float padding = Random.Range(
                MultiplayerSpawnBuildingPadding,
                MultiplayerSpawnMaximumBuildingPadding
            );
            Vector3 candidate = new Vector3(
                landmark.center.x + direction.x * (landmarkExtent + padding),
                0f,
                landmark.center.z + direction.y * (landmarkExtent + padding)
            );
            lastCandidate = candidate;

            if (terrain != null && terrainSize != Vector3.zero &&
                (candidate.x < terrainOrigin.x ||
                 candidate.x > terrainOrigin.x + terrainSize.x ||
                 candidate.z < terrainOrigin.z ||
                 candidate.z > terrainOrigin.z + terrainSize.z))
                continue;

            lastCandidate = candidate;
            foundCandidateOnTerrain = true;

            if (!IsMultiplayerSpawnPositionClear(candidate))
                continue;

            if (!IsTooCloseToConnectedPlayer(candidate))
                return candidate;
        }

        Debug.LogWarning(
            "Could not find a clear multiplayer spawn near a scene object; " +
            "using the last in-bounds position sampled.",
            this);
        if (foundCandidateOnTerrain)
            return lastCandidate;

        Debug.LogError(
            "Scene objects suitable for spawning were outside the active terrain.",
            this);
        return new Vector3(center.x, 0f, center.z);
    }

    private bool TryGetFenceEnclosedBounds(out Bounds bounds)
    {
        bounds = default;
        GameObject fenceObject = GameObject.Find("Fence");
        if (fenceObject == null)
            return false;

        Renderer[] fenceRenderers =
            fenceObject.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer fenceRenderer in fenceRenderers)
        {
            if (fenceRenderer == null || !fenceRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = fenceRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(fenceRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] fenceColliders =
                fenceObject.GetComponentsInChildren<Collider>(true);
            foreach (Collider fenceCollider in fenceColliders)
            {
                if (fenceCollider == null || !fenceCollider.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = fenceCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(fenceCollider.bounds);
                }
            }
        }

        return hasBounds;
    }

    private bool IsTooCloseToConnectedPlayer(Vector3 candidate)
    {
        if (lanNetworkManager == null)
            return false;

        foreach (NetworkClient client in
                 lanNetworkManager.ConnectedClients.Values)
        {
            if (client.PlayerObject == null)
                continue;

            Vector3 existingPosition =
                client.PlayerObject.transform.position;
            existingPosition.y = 0f;

            if (Vector3.Distance(candidate, existingPosition) <
                MultiplayerSpawnMinDistance)
                return true;
        }

        return false;
    }

    private bool IsStaticWorldObject(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.gameObject.isStatic)
                return true;
        }

        return false;
    }

    private bool IsPlayerOrDynamicObject(Transform target)
    {
        if (target.GetComponentInParent<TankHealth>() != null ||
            target.GetComponentInParent<NetworkObject>() != null)
            return true;

        Rigidbody body = target.GetComponentInParent<Rigidbody>();
        return body != null && !body.isKinematic;
    }

    private bool IsMultiplayerSpawnPositionClear(Vector3 position)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            position + Vector3.up * 2f,
            3.5f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null ||
                overlap.GetComponentInParent<Terrain>() != null ||
                overlap.GetComponentInParent<TankHealth>() != null)
                continue;

            return false;
        }

        return true;
    }

    private void OnLanClientConnected(ulong clientId)
    {
        if (lanNetworkManager == null)
            return;

        if (lanNetworkManager.IsServer)
        {
            if (!approvedClientSpawnPositions.ContainsKey(clientId) &&
                lanNetworkManager.IsHost &&
                clientId == lanNetworkManager.LocalClientId)
            {
                approvedClientTeams[clientId] = selectedTeam;
                approvedClientNames[clientId] =
                    SanitizePlayerName(playerNameInput);
                approvedClientSpawnPositions[clientId] =
                    GetRandomMultiplayerSpawnPosition();
            }

            if (approvedClientSpawnPositions.TryGetValue(
                    clientId, out Vector3 spawnPosition))
            {
                StartCoroutine(
                    ApplyApprovedSpawnPosition(clientId, spawnPosition));
            }
        }

        if (clientId != lanNetworkManager.LocalClientId)
            return;

        RemoveSceneAIObjects();

        showMainMenu = false;
        showPauseMenu = false;
        showMultiplayerMenu = false;
        showCreateRoomMenu = false;
        showJoinRoomMenu = false;
        roomStatusMessage = "";
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StopSearching();
        Time.timeScale = 1f;
        SetCursor(false);
    }

    private IEnumerator ApplyApprovedSpawnPosition(
        ulong clientId,
        Vector3 spawnPosition)
    {
        NetworkClient client = null;
        for (int frame = 0; frame < 120; frame++)
        {
            if (lanNetworkManager == null ||
                !lanNetworkManager.IsListening)
                yield break;

            if (lanNetworkManager.ConnectedClients.TryGetValue(
                    clientId, out client) &&
                client.PlayerObject != null &&
                client.PlayerObject.IsSpawned)
                break;

            yield return null;
        }

        if (client == null || client.PlayerObject == null ||
            !client.PlayerObject.IsSpawned)
        {
            Debug.LogError(
                "Could not apply the approved spawn position for client " +
                clientId + ".",
                this);
            yield break;
        }

        Quaternion spawnRotation =
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Transform playerTransform = client.PlayerObject.transform;
        playerTransform.SetPositionAndRotation(
            spawnPosition, spawnRotation);

        Rigidbody playerRigidbody =
            playerTransform.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.position = spawnPosition;
            playerRigidbody.rotation = spawnRotation;
        }

        NetworkTankPlayer player =
            playerTransform.GetComponent<NetworkTankPlayer>();
        if (player == null)
        {
            Debug.LogError(
                "The multiplayer player prefab is missing " +
                "NetworkTankPlayer.",
                playerTransform);
            yield break;
        }

        if (approvedClientTeams.TryGetValue(clientId, out TankTeam team) &&
            approvedClientNames.TryGetValue(clientId, out string playerName))
        {
            player.SetServerIdentity(team, playerName);
        }

        if (clientId != lanNetworkManager.LocalClientId)
        {
            player.SetSpawnPositionClientRpc(
                spawnPosition,
                spawnRotation,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    }
                });
        }
    }

    private void OnLanClientDisconnected(ulong clientId)
    {
        approvedClientTeams.Remove(clientId);
        approvedClientNames.Remove(clientId);
        approvedClientSpawnPositions.Remove(clientId);

        if (lanNetworkManager == null ||
            clientId != lanNetworkManager.LocalClientId ||
            lanNetworkManager.IsHost)
            return;

        NetworkManager disconnectedManager = lanNetworkManager;
        string disconnectReason = disconnectedManager.DisconnectReason;
        lanNetworkManager = null;
        disconnectedManager.OnClientConnectedCallback -= OnLanClientConnected;
        disconnectedManager.OnClientDisconnectCallback -= OnLanClientDisconnected;
        disconnectedManager.Shutdown();
        Destroy(disconnectedManager.gameObject);

        string failureMessage = string.IsNullOrWhiteSpace(disconnectReason)
            ? "Connection lost. Select a room to try again."
            : "Connection failed: " + disconnectReason;
        Debug.LogWarning(failureMessage, this);

        showMainMenu = true;
        showMultiplayerMenu = true;
        showTeamSelection = false;
        showCreateRoomMenu = false;
        showJoinRoomMenu = true;
        roomStatusMessage = failureMessage;
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StartSearching();
        Time.timeScale = 0f;
        SetCursor(true);
        RestoreScenePlayerAndAI();
    }

    private void PrepareMultiplayerScene()
    {
        currentGameMode = GameMode.Multiplayer;
        totalAITanks = 0;
        destroyedTanks = 0;
        localPlayerEnemyTanksDestroyed = 0;
        gameOver = false;
        victory = false;
        finishMenuVisible = false;
        multiplayerWinningTeam = TankTeam.Unassigned;
        multiplayerMatchEnded = false;

        if (playerHealth != null)
            playerHealth.gameObject.SetActive(false);

        showMainMenu = true;
        Time.timeScale = 1f;
        SetCursor(true);
    }

    private void RemoveSceneAIObjects()
    {
        AIRandomSpawner[] tankSpawners =
            FindObjectsOfType<AIRandomSpawner>(true);
        foreach (AIRandomSpawner spawner in tankSpawners)
            spawner.enabled = false;

        AIDroneSpawner[] droneSpawners =
            FindObjectsOfType<AIDroneSpawner>(true);
        foreach (AIDroneSpawner spawner in droneSpawners)
            spawner.enabled = false;

        TankHealth[] tanks = FindObjectsOfType<TankHealth>(true);
        foreach (TankHealth tank in tanks)
        {
            if (tank != null && !tank.isPlayerTank)
                Destroy(tank.gameObject);
        }

        AIDroneController[] drones =
            FindObjectsOfType<AIDroneController>(true);
        foreach (AIDroneController drone in drones)
        {
            if (drone != null)
                Destroy(drone.gameObject);
        }
    }

    private void RestoreScenePlayerAndAI()
    {
        currentGameMode = GameMode.SinglePlay;

        if (scenePlayerHealth != null)
            playerHealth = scenePlayerHealth;

        if (scenePlayerMovement != null)
            playerMovement = scenePlayerMovement;

        if (playerHealth != null)
            playerHealth.gameObject.SetActive(true);

        AIRandomSpawner[] tankSpawners =
            FindObjectsOfType<AIRandomSpawner>(true);
        foreach (AIRandomSpawner spawner in tankSpawners)
            spawner.enabled = true;

        AIDroneSpawner[] droneSpawners =
            FindObjectsOfType<AIDroneSpawner>(true);
        foreach (AIDroneSpawner spawner in droneSpawners)
            spawner.enabled = true;

        TankHealth[] tanks = FindObjectsOfType<TankHealth>(true);
        bool foundAITank = false;
        foreach (TankHealth tank in tanks)
        {
            if (tank != null && !tank.isPlayerTank)
            {
                tank.gameObject.SetActive(true);
                foundAITank = true;
            }
        }

        if (!foundAITank)
        {
            AIRandomSpawner tankSpawner =
                FindObjectOfType<AIRandomSpawner>();

            if (tankSpawner != null)
                tankSpawner.SpawnTanks();
        }

        AIDroneController[] drones =
            FindObjectsOfType<AIDroneController>(true);
        foreach (AIDroneController drone in drones)
        {
            if (drone != null)
                drone.gameObject.SetActive(true);
        }
    }

    // --------------------------------------------------
    // PAUSE AND RESUME
    // --------------------------------------------------

    private void PauseGame()
    {
        if (gameOver)
            return;

        showPauseMenu = true;
        showSettings = false;
        showControlSettings = false;

        Time.timeScale = 0f;
        SetCursor(true);
    }

    private void ResumeGame()
    {
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;

        Time.timeScale = 1f;
        SetCursor(false);
    }

    // --------------------------------------------------
    // BUTTON ACTIONS
    // --------------------------------------------------

    private void PlayAgain()
    {
        Time.timeScale = 1f;

        if (IsMultiplayerMode)
        {
            if (lanRoomDiscovery != null)
                lanRoomDiscovery.StopAdvertising();
            CleanupLanNetworkManager();
        }

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StopAdvertising();
        CleanupLanNetworkManager();
        Application.Quit();

        Debug.Log("Exit requested.");
    }

    // --------------------------------------------------
    // GUI
    // --------------------------------------------------

    private void OnGUI()
    {
        CreateStyles();

        if (showMainMenu)
        {
            DrawMainMenu();
            DrawMissileView();
            return;
        }

        if (showPauseMenu)
        {
            DrawPauseMenu();
            DrawMissileView();
            return;
        }

        DrawBattleHUD();

        if (finishMenuVisible)
            DrawGameOverMenu();

        DrawMissileView();
    }

    private void DrawMissileView()
    {
        if (!TryGetMissileViewRect(out Rect viewRect))
            return;

        float scaledBorder = viewRect.width / 512f * 10f;
        Rect guiViewRect = new Rect(
            viewRect.x,
            Screen.height - viewRect.y - viewRect.height,
            viewRect.width,
            viewRect.height
        );
        Rect frameRect = new Rect(
            guiViewRect.x - scaledBorder,
            guiViewRect.y - scaledBorder,
            guiViewRect.width + scaledBorder * 2f,
            guiViewRect.height + scaledBorder * 2f
        );

        Color previousColor = GUI.color;
        GUI.color = new Color(0.18f, 0.19f, 0.21f, 1f);
        GUI.DrawTexture(frameRect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.DrawTexture(
            guiViewRect,
            missileViewBlack
                ? Texture2D.blackTexture
                : missileViewCamera.targetTexture,
            ScaleMode.StretchToFill,
            false
        );
    }

    // --------------------------------------------------
    // MAIN MENU
    // --------------------------------------------------

    private void DrawMainMenu()
    {
        float width = 420f;
        float height = showSettings || showControlSettings
            ? 440f
            : 390f;

        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "");

        GUI.Label(
            new Rect(x + 20, y + 20, width - 40, 60),
            showMultiplayerMenu ? "MULTIPLAYER" : "LEOPARD BATTLE",
            titleStyle
        );

        if (showMultiplayerMenu)
        {
            DrawMultiplayerMenu(x, y, width);
            return;
        }

        if (showControlSettings)
        {
            DrawControlSettings(x, y, width);
            return;
        }

        if (showSettings)
        {
            DrawSettings(x, y, width);
            return;
        }

        if (GUI.Button(
            new Rect(x + 50, y + 105, width - 100, 48),
            "SINGLE PLAY",
            buttonStyle))
        {
            StartGame(GameMode.SinglePlay);
        }

        if (GUI.Button(
            new Rect(x + 50, y + 165, width - 100, 48),
            "MULTIPLAYER",
            buttonStyle))
        {
            showMultiplayerMenu = true;
            showTeamSelection = true;
            showCreateRoomMenu = false;
            showJoinRoomMenu = false;
            selectedTeam = TankTeam.Unassigned;
        }

        if (GUI.Button(
            new Rect(x + 50, y + 225, width - 100, 48),
            "SETTINGS",
            buttonStyle))
        {
            showSettings = true;
        }

        if (GUI.Button(
            new Rect(x + 50, y + 285, width - 100, 48),
            "EXIT",
            buttonStyle))
        {
            QuitGame();
        }
    }

    private void DrawMultiplayerMenu(float x, float y, float width)
    {
        if (showTeamSelection)
        {
            GUI.Label(
                new Rect(x + 40f, y + 78f, width - 80f, 24f),
                "PLAYER NAME",
                statsStyle
            );
            playerNameInput = GUI.TextField(
                new Rect(x + 40f, y + 102f, width - 80f, 34f),
                playerNameInput,
                32
            );

            GUI.Label(
                new Rect(x + 40f, y + 142f, width - 80f, 24f),
                "SELECT YOUR TEAM",
                statsStyle
            );

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.3f, 0.95f);
            if (GUI.Button(
                new Rect(x + 40f, y + 170f, (width - 100f) * 0.5f, 48f),
                selectedTeam == TankTeam.Blue ? "BLUE TEAM  SELECTED" : "BLUE TEAM",
                buttonStyle))
            {
                selectedTeam = TankTeam.Blue;
            }

            GUI.backgroundColor = new Color(0.9f, 0.16f, 0.12f);
            if (GUI.Button(
                new Rect(x + 60f + (width - 100f) * 0.5f, y + 170f,
                    (width - 100f) * 0.5f, 48f),
                selectedTeam == TankTeam.Red ? "RED TEAM  SELECTED" : "RED TEAM",
                buttonStyle))
            {
                selectedTeam = TankTeam.Red;
            }
            GUI.backgroundColor = previousColor;

            GUI.Label(
                new Rect(x + 40f, y + 222f, width - 80f, 30f),
                selectedTeam == TankTeam.Unassigned
                    ? "Choose a team to continue."
                    : "Selected: " + selectedTeam.ToString().ToUpperInvariant(),
                statsStyle
            );

            bool canContinue = selectedTeam != TankTeam.Unassigned &&
                !string.IsNullOrWhiteSpace(playerNameInput);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = canContinue;
            if (GUI.Button(
                new Rect(x + 50f, y + 260f, width - 100f, 42f),
                "CONTINUE",
                buttonStyle))
            {
                showTeamSelection = false;
                roomStatusMessage = "";
            }
            GUI.enabled = previousEnabled;

            if (GUI.Button(
                new Rect(x + 50f, y + 310f, width - 100f, 38f),
                "BACK",
                buttonStyle))
            {
                showTeamSelection = false;
                showMultiplayerMenu = false;
                selectedTeam = TankTeam.Unassigned;
            }

            return;
        }

        if (showCreateRoomMenu)
        {
            GUI.Label(
                new Rect(x + 40f, y + 100f, width - 80f, 28f),
                "ROOM NAME",
                statsStyle
            );

            roomNameInput = GUI.TextField(
                new Rect(x + 40f, y + 132f, width - 80f, 38f),
                roomNameInput,
                24
            );

            GUI.Label(
                new Rect(x + 40f, y + 176f, width - 80f, 40f),
                roomStatusMessage,
                statsStyle
            );

            if (GUI.Button(
                new Rect(x + 50f, y + 225f, width - 100f, 46f),
                "CREATE SERVER",
                buttonStyle))
            {
                CreateLanRoom();
            }

            if (GUI.Button(
                new Rect(x + 50f, y + 282f, width - 100f, 42f),
                "BACK",
                buttonStyle))
            {
                showCreateRoomMenu = false;
                roomStatusMessage = "";
            }

            return;
        }

        if (showJoinRoomMenu)
        {
            GUI.Label(
                new Rect(x + 34f, y + 86f, width - 68f, 24f),
                roomStatusMessage,
                statsStyle
            );

            Rect listRect = new Rect(x + 28f, y + 116f, width - 56f, 190f);
            float contentHeight = Mathf.Max(
                190f,
                lanRoomDiscovery != null
                    ? lanRoomDiscovery.Rooms.Count * 48f
                    : 190f
            );

            roomListScroll = GUI.BeginScrollView(
                listRect,
                roomListScroll,
                new Rect(0f, 0f, listRect.width - 18f, contentHeight)
            );

            if (lanRoomDiscovery != null)
            {
                for (int i = 0; i < lanRoomDiscovery.Rooms.Count; i++)
                {
                    LanRoomInfo room = lanRoomDiscovery.Rooms[i];
                    Rect row = new Rect(0f, i * 48f, listRect.width - 22f, 42f);
                    string label = room.roomName + "   " +
                        room.playerCount + "/" + room.maxPlayers +
                        "   " + room.address;

                    if (GUI.Button(row, label, buttonStyle))
                        JoinLanRoom(room);
                }
            }

            GUI.EndScrollView();

            if (lanRoomDiscovery == null || lanRoomDiscovery.Rooms.Count == 0)
            {
                GUI.Label(
                    new Rect(x + 40f, y + 175f, width - 80f, 50f),
                    "No rooms found on this LAN yet.",
                    statsStyle
                );
            }

            if (GUI.Button(
                new Rect(x + 40f, y + 316f, (width - 100f) * 0.5f, 42f),
                "REFRESH",
                buttonStyle))
            {
                if (lanRoomDiscovery != null)
                    lanRoomDiscovery.StartSearching();
                roomStatusMessage = "Searching local network...";
            }

            if (GUI.Button(
                new Rect(x + 60f + (width - 100f) * 0.5f, y + 316f, (width - 100f) * 0.5f, 42f),
                "BACK",
                buttonStyle))
            {
                showJoinRoomMenu = false;
                roomStatusMessage = "";

                if (lanRoomDiscovery != null)
                    lanRoomDiscovery.StopSearching();
            }

            return;
        }

        if (GUI.Button(
            new Rect(x + 50f, y + 112f, width - 100f, 48f),
            "CREATE SERVER",
            buttonStyle))
        {
            showCreateRoomMenu = true;
            roomStatusMessage = "";
        }

        if (GUI.Button(
            new Rect(x + 50f, y + 172f, width - 100f, 48f),
            "JOIN SERVER",
            buttonStyle))
        {
            showJoinRoomMenu = true;
            roomStatusMessage = "Searching local network...";
            if (lanRoomDiscovery != null)
                lanRoomDiscovery.StartSearching();
        }

        if (GUI.Button(
            new Rect(x + 50f, y + 232f, width - 100f, 48f),
            "BACK",
            buttonStyle))
        {
            showMultiplayerMenu = false;
        }
    }

    // --------------------------------------------------
    // PAUSE MENU
    // --------------------------------------------------

    private void DrawPauseMenu()
    {
        float width = 380f;
        float height = showSettings || showControlSettings
            ? 440f
            : 360f;

        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "");

        GUI.Label(
            new Rect(x + 20, y + 15, width - 40, 60),
            "GAME PAUSED",
            titleStyle
        );

        if (showControlSettings)
        {
            DrawControlSettings(x, y, width);
            return;
        }

        if (showSettings)
        {
            DrawSettings(x, y, width);
            return;
        }

        if (GUI.Button(
            new Rect(x + 50, y + 95, width - 100, 45),
            "RESUME",
            buttonStyle))
        {
            ResumeGame();
        }

        if (GUI.Button(
            new Rect(x + 50, y + 150, width - 100, 45),
            "PLAY AGAIN",
            buttonStyle))
        {
            PlayAgain();
        }

        if (GUI.Button(
            new Rect(x + 50, y + 205, width - 100, 45),
            "SETTINGS",
            buttonStyle))
        {
            showSettings = true;
        }

        if (GUI.Button(
            new Rect(x + 50, y + 260, width - 100, 45),
            "EXIT",
            buttonStyle))
        {
            QuitGame();
        }
    }

    // --------------------------------------------------
    // SETTINGS
    // --------------------------------------------------

    private void DrawSettings(float x, float y, float width)
    {
        GUI.Label(
            new Rect(x + 30, y + 85, width - 60, 30),
            "Resolution",
            statsStyle
        );

        string[] options =
        {
            "1280 x 720",
            "1920 x 1080",
            "2560 x 1440"
        };

        selectedResolution = Mathf.Clamp(
            selectedResolution, 0, resolutions.Length - 1
        );

        selectedResolution = GUI.SelectionGrid(
            new Rect(x + 30, y + 115, width - 60, 75),
            selectedResolution,
            options,
            1
        );

        isFullscreen = GUI.Toggle(
            new Rect(x + 30, y + 195, width - 60, 30),
            isFullscreen,
            " Fullscreen"
        );

        if (GUI.Button(
            new Rect(x + 30, y + 230, width - 60, 35),
            "APPLY",
            buttonStyle))
        {
            Vector2Int resolution = resolutions[selectedResolution];

            Screen.SetResolution(
                resolution.x,
                resolution.y,
                isFullscreen
            );
        }

        if (GUI.Button(
            new Rect(x + 30, y + 275, width - 60, 35),
            "CONTROL SETTINGS",
            buttonStyle))
        {
            showControlSettings = true;
        }

        if (GUI.Button(
            new Rect(x + 30, y + 320, width - 60, 35),
            "BACK",
            buttonStyle))
        {
            showSettings = false;
        }
    }

    // --------------------------------------------------
    // CONTROL SETTINGS
    // --------------------------------------------------

    private void DrawControlSettings(float x, float y, float width)
    {
        GUI.Label(
            new Rect(x + 20, y + 85, width - 40, 35),
            "TURRET CONTROL METHOD",
            statsStyle
        );

        if (GUI.Button(
            new Rect(x + 30, y + 130, width - 60, 45),
            !MouseTurretControlEnabled
                ? "✓ Keyboard Aiming (J/L, I/K)"
                : "Keyboard Aiming (J/L, I/K)",
            buttonStyle))
        {
            MouseTurretControlEnabled = false;
        }

        if (GUI.Button(
            new Rect(x + 30, y + 185, width - 60, 45),
            MouseTurretControlEnabled
                ? "✓ Mouse Aiming"
                : "Mouse Aiming",
            buttonStyle))
        {
            MouseTurretControlEnabled = true;
        }

        GUI.Label(
            new Rect(x + 30, y + 245, width - 60, 55),
            MouseTurretControlEnabled
                ? "Move mouse to aim; left-click to fire."
                : "J/L rotate, I/K elevate, Space fires.",
            statsStyle
        );

        if (GUI.Button(
            new Rect(x + 50, y + 340, width - 100, 35),
            "BACK",
            buttonStyle))
        {
            showControlSettings = false;
        }
    }

    // --------------------------------------------------
    // TOP-RIGHT BATTLE HUD
    // --------------------------------------------------

    private void DrawBattleHUD()
    {
        float margin = 18f;
        float panelWidth = Mathf.Min(390f, Screen.width - margin * 2f);
        float x = Screen.width - panelWidth - margin;
        float y = margin;
        float inset = 12f;
        bool multiplayer = IsMultiplayerMode;
        float layoutOffset = multiplayer ? 42f : 0f;

        GUI.Box(
            new Rect(x, y, panelWidth, multiplayer ? 280f : 238f),
            "",
            hudPanelStyle
        );
        float countWidth = (panelWidth - inset * 2f - 8f) / 2f;
        Rect totalCard = new Rect(x + inset, y + 12f, countWidth, 48f);
        Rect destroyedCard = new Rect(totalCard.xMax + 8f, totalCard.y, countWidth, 48f);
        if (multiplayer)
        {
            Rect enemyDestroyedCard = new Rect(
                x + inset, y + 12f, panelWidth - inset * 2f, 48f);
            int enemyTanksDestroyed = LocalPlayerEnemyTanksDestroyed;
            GUI.Box(enemyDestroyedCard, "", hudBarBackgroundStyle);
            GUI.Label(
                new Rect(enemyDestroyedCard.x + 8f,
                    enemyDestroyedCard.y + 3f,
                    enemyDestroyedCard.width - 16f, 16f),
                "ENEMY TANKS DESTROYED",
                statsStyle);
            GUI.Label(
                new Rect(enemyDestroyedCard.x + 8f,
                    enemyDestroyedCard.y + 19f,
                    enemyDestroyedCard.width - 16f, 25f),
                enemyTanksDestroyed.ToString(),
                hudValueStyle);

            GetMultiplayerTeamTankCounts(
                out int blueTanksRemaining,
                out int redTanksRemaining
            );
            Rect blueTeamCard = new Rect(
                x + inset, y + 66f, countWidth, 36f);
            Rect redTeamCard = new Rect(
                blueTeamCard.xMax + 8f, blueTeamCard.y,
                countWidth, 36f);
            GUI.Box(blueTeamCard, "", hudBarBackgroundStyle);
            GUI.Box(redTeamCard, "", hudBarBackgroundStyle);
            GUI.Label(
                new Rect(blueTeamCard.x + 6f, blueTeamCard.y + 1f,
                    countWidth - 12f, 15f),
                "BLUE TANKS LEFT",
                statsStyle);
            GUI.Label(
                new Rect(redTeamCard.x + 6f, redTeamCard.y + 1f,
                    countWidth - 12f, 15f),
                "RED TANKS LEFT",
                statsStyle);
            Color previousGuiColor = GUI.color;
            GUI.color = new Color(0.35f, 0.65f, 1f, 1f);
            GUI.Label(
                new Rect(blueTeamCard.x + 6f, blueTeamCard.y + 15f,
                    countWidth - 12f, 20f),
                blueTanksRemaining.ToString(),
                hudValueStyle);
            GUI.color = new Color(1f, 0.4f, 0.35f, 1f);
            GUI.Label(
                new Rect(redTeamCard.x + 6f, redTeamCard.y + 15f,
                    countWidth - 12f, 20f),
                redTanksRemaining.ToString(),
                hudValueStyle);
            GUI.color = previousGuiColor;
        }
        else
        {
            GUI.Box(totalCard, "", hudBarBackgroundStyle);
            GUI.Box(destroyedCard, "", hudBarBackgroundStyle);
            GUI.Label(new Rect(totalCard.x + 8f, totalCard.y + 3f, countWidth - 16f, 16f), "AI TANKS", statsStyle);
            GUI.Label(new Rect(totalCard.x + 8f, totalCard.y + 19f, countWidth - 16f, 25f), totalAITanks.ToString(), hudValueStyle);
            GUI.Label(new Rect(destroyedCard.x + 8f, destroyedCard.y + 3f, countWidth - 16f, 16f), "DESTROYED", statsStyle);
            GUI.Label(new Rect(destroyedCard.x + 8f, destroyedCard.y + 19f, countWidth - 16f, 25f), destroyedTanks.ToString(), hudValueStyle);
        }

        GUI.Label(
            new Rect(x + inset, y + 66f + layoutOffset,
                panelWidth - inset * 2f, 18f),
            "WEAPON COOLDOWN",
            statsStyle);
        float meterY = y + 120f + layoutOffset;
        float leftCenterX = x + panelWidth * 0.30f;
        float rightCenterX = x + panelWidth * 0.70f;

        string missileValue;
        float missileProgress;
        if (multiplayer)
        {
            missileValue = playerAntiDetectLauncher == null
                ? "N/A"
                : playerAntiDetectLauncher.AmmoRemaining <= 0
                    ? "EMPTY"
                    : playerAntiDetectLauncher.IsReloading
                        ? playerAntiDetectLauncher.AmmoRemaining + " | " +
                          playerAntiDetectLauncher.RemainingReload
                              .ToString("0.0") + "s"
                        : playerAntiDetectLauncher.AmmoRemaining + " READY";
            missileProgress = playerAntiDetectLauncher != null
                ? playerAntiDetectLauncher.ReloadProgress
                : 1f;
        }
        else
        {
            missileValue = playerMissileLauncher == null
                ? "N/A"
                : playerMissileLauncher.IsReloading
                    ? playerMissileLauncher.RemainingReload.ToString("0.0") + "s"
                    : playerMissileLauncher.AmmoRemaining <= 0
                        ? "EMPTY"
                        : "READY";
            missileProgress = playerMissileLauncher != null
                ? playerMissileLauncher.ReloadProgress
                : 1f;
        }

        string shellValue = playerTankFire == null
            ? "N/A"
            : playerTankFire.IsReloading
                ? playerTankFire.RemainingReload.ToString("0.0") + "s"
                : "READY";
        float shellProgress = playerTankFire != null
            ? playerTankFire.ReloadProgress
            : 1f;

        DrawRadialMeter(new Vector2(leftCenterX, meterY), 31f, missileProgress, new Color(0.96f, 0.30f, 0.25f, 1f), "MISSILE", missileValue);
        DrawRadialMeter(new Vector2(rightCenterX, meterY), 31f, shellProgress, new Color(0.12f, 0.75f, 0.96f, 1f), "SHELL", shellValue);

        float speed = playerMovement != null ? Mathf.Abs(playerMovement.currentSpeed) : 0f;
        float maxSpeed = playerMovement != null ? Mathf.Max(0.1f, playerMovement.maxSpeed) : 1f;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        int speedKph = Mathf.RoundToInt(speed * 3.6f);

        Rect speedCard = new Rect(
            x + inset, y + 177f + layoutOffset,
            panelWidth - inset * 2f, 49f);
        GUI.Box(speedCard, "", hudBarBackgroundStyle);
        GUI.Label(new Rect(speedCard.x + 8f, speedCard.y + 3f, speedCard.width - 16f, 17f), "TANK SPEED", statsStyle);
        GUI.Label(new Rect(speedCard.x + speedCard.width - 88f, speedCard.y + 2f, 80f, 19f), speedKph + " km/h", hudValueStyle);

        Rect speedBar = new Rect(speedCard.x + 8f, speedCard.y + 27f, speedCard.width - 16f, 13f);
        GUI.Box(speedBar, "", hudBarBackgroundStyle);
        GUI.Box(new Rect(speedBar.x + 2f, speedBar.y + 2f, (speedBar.width - 4f) * speedRatio, speedBar.height - 4f), "", hudFillBlueStyle);

        float health = playerHealth != null && playerHealth.maxHealth > 0f
            ? Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.maxHealth)
            : 0f;
        float healthWidth = Mathf.Min(500f, Screen.width - margin * 2f);
        float healthX = (Screen.width - healthWidth) * 0.5f;
        float healthY = Screen.height - 62f;
        Rect healthCard = new Rect(healthX, healthY, healthWidth, 44f);
        GUI.Box(healthCard, "", hudPanelStyle);

        string healthValue = playerHealth != null
            ? Mathf.CeilToInt(playerHealth.CurrentHealth) + " / " + Mathf.CeilToInt(playerHealth.maxHealth)
            : "N/A";
        GUI.Label(new Rect(healthCard.x + 12f, healthCard.y + 3f, 180f, 16f), "PLAYER HEALTH", statsStyle);
        GUI.Label(new Rect(healthCard.x + healthCard.width - 108f, healthCard.y + 2f, 96f, 18f), healthValue, hudValueStyle);

        Rect healthBar = new Rect(healthCard.x + 12f, healthCard.y + 23f, healthCard.width - 24f, 12f);
        GUI.Box(healthBar, "", hudBarBackgroundStyle);
        GUI.Box(new Rect(healthBar.x + 2f, healthBar.y + 2f, (healthBar.width - 4f) * health, healthBar.height - 4f), "", hudBarFillStyle);
    }

    private void GetMultiplayerTeamTankCounts(
        out int blueTanksRemaining,
        out int redTanksRemaining)
    {
        blueTanksRemaining = 0;
        redTanksRemaining = 0;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
            return;

        foreach (NetworkObject networkObject in
                 manager.SpawnManager.SpawnedObjects.Values)
        {
            NetworkTankPlayer player =
                networkObject.GetComponent<NetworkTankPlayer>();
            TankHealth health =
                networkObject.GetComponent<TankHealth>();
            if (player == null || health == null ||
                !player.HasTeam || health.IsDestroyed ||
                health.CurrentHealth <= 0f)
            {
                continue;
            }

            if (player.Team == TankTeam.Blue)
                blueTanksRemaining++;
            else if (player.Team == TankTeam.Red)
                redTanksRemaining++;
        }
    }

    private void DrawRadialMeter(
        Vector2 center,
        float radius,
        float fill,
        Color accent,
        string label,
        string valueText)
    {
        float value = Mathf.Clamp01(fill);
        float bgRadius = radius + 2f;

        GUI.DrawTexture(
            new Rect(
                center.x - bgRadius,
                center.y - bgRadius,
                bgRadius * 2f,
                bgRadius * 2f),
            GetRingTexture(128, 1f, new Color(0.15f, 0.18f, 0.24f, 0.9f))
        );

        GUI.DrawTexture(
            new Rect(
                center.x - radius,
                center.y - radius,
                radius * 2f,
                radius * 2f),
            GetRingTexture(128, value, accent)
        );
        GUI.Label(
            new Rect(center.x - 34f, center.y - 10f, 68f, 20f),
            valueText,
            hudMeterValueStyle
        );

        GUI.Label(
            new Rect(center.x - 50f, center.y + radius + 8f, 100f, 18f),
            label,
            statsStyle
        );
    }

    private Texture2D GetRingTexture(
        int size,
        float fillAmount,
        Color color)
    {
        int fillStep = Mathf.RoundToInt(Mathf.Clamp01(fillAmount) * 40f);
        float steppedFill = fillStep / 40f;
        string key = size + ":" + fillStep + ":" +
            Mathf.RoundToInt(color.r * 255f) + ":" +
            Mathf.RoundToInt(color.g * 255f) + ":" +
            Mathf.RoundToInt(color.b * 255f);

        if (!ringTextures.TryGetValue(key, out Texture2D cachedTexture))
        {
            cachedTexture = CreateRingTexture(size, steppedFill, color);
            ringTextures[key] = cachedTexture;
        }

        return cachedTexture;
    }

    private Texture2D CreateRingTexture(
        int size,
        float fillAmount,
        Color ringColor)
    {
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false
        );

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f;
        float innerRadius = radius * 0.7f;
        float totalAngle = Mathf.Clamp01(fillAmount) * 360f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius;
                float dy = y - radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);

                if (dist <= radius && dist >= innerRadius)
                {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float normalized = (angle + 450f) % 360f;

                    if (normalized <= totalAngle)
                    {
                        pixels[y * size + x] = ringColor;
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void DrawBar(
        float x,
        float y,
        float width,
        float height,
        float value,
        string label)
    {
        GUI.Box(new Rect(x, y, width, height), "", hudBarBackgroundStyle);

        float fillWidth = Mathf.Clamp01(value) * (width - 4f);
        GUI.Box(new Rect(x + 2f, y + 2f, fillWidth, height - 4f), "", hudBarFillStyle);

        GUI.Label(
            new Rect(x, y - 20f, width, 18f),
            label,
            statsStyle
        );
    }

    // --------------------------------------------------
    // FINISH MENU
    // --------------------------------------------------

    private void DrawGameOverMenu()
    {
        bool multiplayer = IsMultiplayerMode;
        float width = 380f;
        float height = multiplayer ? 260f : 320f;

        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "");

        GUI.Label(
            new Rect(x + 20, y + 20, width - 40, 50),
            multiplayer && multiplayerWinningTeam != TankTeam.Unassigned
                ? multiplayerWinningTeam + " Team Win!!"
                : victory ? "VICTORY!" : multiplayer ? "DEFEAT" : "GAME OVER",
            titleStyle
        );

        string destroyedCount = multiplayer
            ? LocalPlayerEnemyTanksDestroyed.ToString()
            : destroyedTanks + " / " + totalAITanks;
        GUI.Label(
            new Rect(x + 20, y + 85, width - 40, 30),
            multiplayer
                ? "TANKS DESTROYED: " + destroyedCount
                : "Destroyed AI Tanks: " + destroyedCount,
            statsStyle
        );

        if (GUI.Button(
            new Rect(x + 50, y + 135, width - 100, 40),
            "PLAY AGAIN",
            buttonStyle))
        {
            PlayAgain();
        }

        if (GUI.Button(
            new Rect(x + 50, y + 190, width - 100, 40),
            multiplayer ? "EXIT" : "MAIN MENU",
            buttonStyle))
        {
            if (multiplayer)
                QuitGame();
            else
                ReturnToMainMenu();
        }

        if (!multiplayer && GUI.Button(
            new Rect(x + 50, y + 245, width - 100, 35),
            "EXIT",
            buttonStyle))
            QuitGame();
    }

    // --------------------------------------------------
    // GUI STYLES
    // --------------------------------------------------

    private void CreateStyles()
    {
        if (statsStyle != null)
            return;

        statsStyle = new GUIStyle(GUI.skin.label);
        statsStyle.fontSize = 15;
        statsStyle.fontStyle = FontStyle.Bold;
        statsStyle.normal.textColor = new Color(0.86f, 0.93f, 1f, 1f);
        statsStyle.alignment = TextAnchor.MiddleLeft;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleLeft;
        titleStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 17;
        buttonStyle.fontStyle = FontStyle.Bold;

        hudPanelStyle = new GUIStyle(GUI.skin.box);
        hudPanelStyle.normal.background = CreateSolidTexture(
            new Color(0f, 0f, 0f, 0.1f)
        );
        hudPanelStyle.border = new RectOffset(18, 18, 18, 18);
        hudPanelStyle.padding = new RectOffset(8, 8, 8, 8);

        hudValueStyle = new GUIStyle(GUI.skin.label);
        hudValueStyle.fontSize = 20;
        hudValueStyle.fontStyle = FontStyle.Bold;
        hudValueStyle.normal.textColor = Color.white;
        hudValueStyle.alignment = TextAnchor.MiddleCenter;

        hudMeterValueStyle = new GUIStyle(hudValueStyle);
        hudMeterValueStyle.fontSize = 14;

        hudBarBackgroundStyle = new GUIStyle(GUI.skin.box);
        hudBarBackgroundStyle.normal.background = CreateSolidTexture(
            new Color(0f, 0f, 0f, 0.1f)
        );
        hudBarBackgroundStyle.border = new RectOffset(4, 4, 4, 4);

        hudBarFillStyle = new GUIStyle(GUI.skin.box);
        hudBarFillStyle.normal.background = CreateSolidTexture(
            new Color(0.25f, 0.9f, 0.48f, 1f)
        );
        hudBarFillStyle.border = new RectOffset(4, 4, 4, 4);

        hudFillBlueStyle = new GUIStyle(GUI.skin.box);
        hudFillBlueStyle.normal.background = CreateSolidTexture(
            new Color(0.12f, 0.75f, 0.96f, 1f)
        );
        hudFillBlueStyle.border = new RectOffset(4, 4, 4, 4);

        hudDangerPanelStyle = new GUIStyle(GUI.skin.box);
        hudDangerPanelStyle.normal.background = CreateSolidTexture(
            new Color(0.16f, 0.08f, 0.10f, 1f)
        );
        hudDangerPanelStyle.border = new RectOffset(10, 10, 10, 10);

        hudDangerFillStyle = new GUIStyle(GUI.skin.box);
        hudDangerFillStyle.normal.background = CreateSolidTexture(
            new Color(0.96f, 0.30f, 0.25f, 1f)
        );
        hudDangerFillStyle.border = new RectOffset(4, 4, 4, 4);

        hudBadgeStyle = new GUIStyle(GUI.skin.button);
        hudBadgeStyle.alignment = TextAnchor.MiddleCenter;
        hudBadgeStyle.normal.textColor = new Color(0.08f, 0.9f, 0.6f, 1f);
        hudBadgeStyle.fontSize = 12;
        hudBadgeStyle.fontStyle = FontStyle.Bold;
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (lanRoomDiscovery != null)
        {
            lanRoomDiscovery.StopSearching();
            lanRoomDiscovery.StopAdvertising();
        }

        if (lanNetworkManager != null)
        {
            lanNetworkManager.OnClientConnectedCallback -= OnLanClientConnected;
            lanNetworkManager.OnClientDisconnectCallback -= OnLanClientDisconnected;

            if (lanNetworkManager.IsListening)
                lanNetworkManager.Shutdown();
        }

        if (Instance == this)
            Instance = null;

        Time.timeScale = 1f;
    }
}
