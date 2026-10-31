
using System.Collections;
using System.Collections.Generic;
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
    private bool finishMenuVisible;

    private bool showPauseMenu;
    private bool showSettings;
    private bool showControlSettings;
    private bool showMultiplayerMenu;
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
    private TankHealth scenePlayerHealth;
    private TankMovement scenePlayerMovement;
    private Camera missileViewCamera;
    private bool missileViewVisible;
    private bool missileViewBlack;
    private LanRoomDiscovery lanRoomDiscovery;
    private NetworkManager lanNetworkManager;
    private string roomNameInput = "Abrams Room";
    private string roomStatusMessage = "";
    private Vector2 roomListScroll;

    private const ushort LanGamePort = 7777;
    private const int LanMaxPlayers = 4;

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

    public Transform MultiplayerSpawnAnchor =>
        scenePlayerHealth != null
            ? scenePlayerHealth.transform
            : playerHealth != null
                ? playerHealth.transform
                : null;

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
        finishMenuVisible = false;

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
            manager.AddNetworkPrefab(prefabTankFire.shellPrefab);

        manager.OnClientConnectedCallback += OnLanClientConnected;
        manager.OnClientDisconnectCallback += OnLanClientDisconnected;
        return manager;
    }

    private void ApproveLanClient(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = lanNetworkManager == null ||
            lanNetworkManager.ConnectedClients.Count < LanMaxPlayers;
        response.CreatePlayerObject = response.Approved;

        if (!response.Approved || MultiplayerSpawnAnchor == null)
            return;

        Transform spawnAnchor = MultiplayerSpawnAnchor;
        response.Position = spawnAnchor.position +
            spawnAnchor.right * ((float)request.ClientNetworkId * 7f);
        response.Rotation = Quaternion.Euler(
            0f,
            spawnAnchor.eulerAngles.y,
            0f
        );
    }

    private void OnLanClientConnected(ulong clientId)
    {
        if (lanNetworkManager == null ||
            clientId != lanNetworkManager.LocalClientId)
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

    private void OnLanClientDisconnected(ulong clientId)
    {
        if (lanNetworkManager == null ||
            clientId != lanNetworkManager.LocalClientId ||
            lanNetworkManager.IsHost)
            return;

        NetworkManager disconnectedManager = lanNetworkManager;
        lanNetworkManager = null;
        disconnectedManager.OnClientConnectedCallback -= OnLanClientConnected;
        disconnectedManager.OnClientDisconnectCallback -= OnLanClientDisconnected;
        disconnectedManager.Shutdown();
        Destroy(disconnectedManager.gameObject);

        showMainMenu = true;
        showMultiplayerMenu = true;
        showJoinRoomMenu = true;
        roomStatusMessage = "Disconnected from the room.";
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
        if (!missileViewVisible || missileViewCamera == null ||
            missileViewCamera.targetTexture == null)
            return;

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
        Rect frameRect = new Rect(
            Screen.width - scaledWidth - scaledBorder * 2f - screenMargin,
            Screen.height - scaledHeight - scaledBorder * 2f - screenMargin,
            scaledWidth + scaledBorder * 2f,
            scaledHeight + scaledBorder * 2f
        );
        Rect viewRect = new Rect(
            frameRect.x + scaledBorder,
            frameRect.y + scaledBorder,
            scaledWidth,
            scaledHeight
        );

        Color previousColor = GUI.color;
        GUI.color = new Color(0.18f, 0.19f, 0.21f, 1f);
        GUI.DrawTexture(frameRect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.DrawTexture(
            viewRect,
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

        GUI.Box(new Rect(x, y, panelWidth, 238f), "", hudPanelStyle);
        float countWidth = (panelWidth - inset * 2f - 8f) / 2f;
        Rect totalCard = new Rect(x + inset, y + 12f, countWidth, 48f);
        Rect destroyedCard = new Rect(totalCard.xMax + 8f, totalCard.y, countWidth, 48f);
        GUI.Box(totalCard, "", hudBarBackgroundStyle);
        GUI.Box(destroyedCard, "", hudBarBackgroundStyle);
        GUI.Label(new Rect(totalCard.x + 8f, totalCard.y + 3f, countWidth - 16f, 16f), "AI TANKS", statsStyle);
        GUI.Label(new Rect(totalCard.x + 8f, totalCard.y + 19f, countWidth - 16f, 25f), totalAITanks.ToString(), hudValueStyle);
        GUI.Label(new Rect(destroyedCard.x + 8f, destroyedCard.y + 3f, countWidth - 16f, 16f), "DESTROYED", statsStyle);
        GUI.Label(new Rect(destroyedCard.x + 8f, destroyedCard.y + 19f, countWidth - 16f, 25f), destroyedTanks.ToString(), hudValueStyle);

        GUI.Label(new Rect(x + inset, y + 66f, panelWidth - inset * 2f, 18f), "WEAPON COOLDOWN", statsStyle);
        float meterY = y + 120f;
        float leftCenterX = x + panelWidth * 0.30f;
        float rightCenterX = x + panelWidth * 0.70f;

        string missileValue = playerMissileLauncher == null
            ? "N/A"
            : playerMissileLauncher.IsReloading
                ? playerMissileLauncher.RemainingReload.ToString("0.0") + "s"
                : playerMissileLauncher.AmmoRemaining <= 0
                    ? "EMPTY"
                    : "READY";
        float missileProgress = playerMissileLauncher != null
            ? playerMissileLauncher.ReloadProgress
            : 1f;

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

        Rect speedCard = new Rect(x + inset, y + 177f, panelWidth - inset * 2f, 49f);
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
        float width = 380f;
        float height = 320f;

        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "");

        GUI.Label(
            new Rect(x + 20, y + 20, width - 40, 50),
            victory ? "VICTORY!" : "GAME OVER",
            titleStyle
        );

        GUI.Label(
            new Rect(x + 20, y + 85, width - 40, 30),
            "Destroyed AI Tanks: " + destroyedTanks +
            " / " + totalAITanks,
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
            "MAIN MENU",
            buttonStyle))
        {
            ReturnToMainMenu();
        }

        if (GUI.Button(
            new Rect(x + 50, y + 245, width - 100, 35),
            "EXIT",
            buttonStyle))
        {
            QuitGame();
        }
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
