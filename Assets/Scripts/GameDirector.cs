
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UI;

public partial class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    private static bool startSinglePlayOnSceneLoad;

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

    [SerializeField]
    private Sprite multiplayerMenuPanelSprite;

    [SerializeField]
    private Sprite startMenuPanelSprite;

    [SerializeField]
    private Sprite pauseMenuPanelSprite;

    [SerializeField]
    private Sprite settingsMenuPanelSprite;

    [SerializeField]
    private Sprite[] menuButtonSprites;

    [SerializeField]
    private Image menuBackgroundImage;

    [SerializeField]
    private Texture2D menuBackgroundTexture;

    [Header("Game Over")]
    public float finishMenuDelay = 2f;

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
    private readonly Dictionary<string, Texture2D> menuTextures =
        new Dictionary<string, Texture2D>();

    private TankFire playerTankFire;
    private AntiDroneLauncher playerMissileLauncher;
    private AntiDetectDronelauncher playerAntiDetectLauncher;
    private NetworkTankPlayer localNetworkTankPlayer;
    private int localPlayerEnemyTanksDestroyed;
    private bool resultCountCacheInitialized;
    private bool cachedResultCountIsMultiplayer;
    private int cachedResultDestroyedCount;
    private int cachedResultTotalCount;
    private TankHealth scenePlayerHealth;
    private TankMovement scenePlayerMovement;
    private TankCameraFollow sceneCameraFollow;
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
    private readonly List<RoomListEntry> displayedRoomList =
        new List<RoomListEntry>();
    private bool joinRoomListNeedsRefresh = true;

    private const ushort LanGamePort = 7777;
    private const int LanMaxPlayers = 4;
    private const uint LegacyShellPrefabHashBeforeLastUpdate = 1732461695u;
    private const uint LegacyShellPrefabHashBeforePrefabRefresh = 2144552063u;
    private const float MultiplayerSpawnMinDistance = 15f;
    private const float MultiplayerSpawnAreaEdgePadding = 8f;
    private const float MultiplayerSpawnBuildingPadding = 8f;
    private const float MultiplayerSpawnMaximumBuildingPadding = 20f;
    private const float MenuPanelAspect = 1.5f;
    private const float MenuPanelMaximumWidth = 760f;
    private const float MultiplayerMenuPanelAspect = 628f / 443f;
    private const float MultiplayerMenuPanelMaximumWidth = 1100f;
    private const float MultiplayerMenuLayoutHeight = 600f;
    private const float MenuButtonWidth = 400f;
    private const float MenuButtonHeight = 70f;

    private sealed class MenuButtonView
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Button button;
        public Image image;
        public bool wasClicked;
    }

    private sealed class MenuButtonSpriteSet
    {
        public Sprite normal;
        public Sprite hover;
        public Sprite pressed;
    }

    private struct RoomListEntry
    {
        public string roomName;
        public string address;
        public int gamePort;
        public int playerCount;
        public int maxPlayers;

        public bool Matches(LanRoomInfo room)
        {
            return string.Equals(
                    roomName,
                    room.roomName,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    address,
                    room.address,
                    StringComparison.Ordinal
                ) &&
                gamePort == room.gamePort &&
                playerCount == room.playerCount &&
                maxPlayers == room.maxPlayers;
        }

        public static RoomListEntry From(LanRoomInfo room)
        {
            return new RoomListEntry
            {
                roomName = room.roomName,
                address = room.address,
                gamePort = room.gamePort,
                playerCount = room.playerCount,
                maxPlayers = room.maxPlayers
            };
        }
    }

    private GUIStyle statsStyle;
    private GUIStyle titleStyle;
    private GUIStyle menuSubtitleStyle;
    private GUIStyle menuCardTitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle pauseMenuButtonStyle;
    private GUIStyle selectedButtonStyle;
    private GUIStyle menuInputStyle;
    private GUIStyle menuToggleStyle;
    private GUIStyle hudPanelStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle hudMeterValueStyle;
    private GUIStyle hudBarBackgroundStyle;
    private GUIStyle hudBarFillStyle;
    private GUIStyle hudFillBlueStyle;
    private GUIStyle hudDangerPanelStyle;
    private GUIStyle hudDangerFillStyle;
    private GUIStyle hudBadgeStyle;
    [SerializeField]
    private Canvas menuPanelCanvas;

    [SerializeField]
    private Image menuPanelImage;

    [SerializeField]
    private RectTransform menuPanelRectTransform;

    private bool menuPanelMissingSpriteWarningLogged;
    private bool menuBackgroundMissingSpriteWarningLogged;

    [SerializeField]
    private Canvas menuButtonCanvas;

    [SerializeField]
    private Button[] menuSceneButtons;

    [SerializeField]
    private Canvas leopardBattleMenuCanvas;

    [SerializeField]
    private Canvas multiplayerMenuCanvas;

    [SerializeField]
    private Canvas settingsMenuCanvas;

    [SerializeField]
    private Canvas joinRoomMenuCanvas;

    [SerializeField]
    private Canvas gamePauseCanvas;

    [SerializeField]
    private Canvas victoryMenuCanvas;

    [SerializeField]
    private Canvas defeatMenuCanvas;

    private InputField playerNameInputField;
    private InputField roomNameInputField;
    private Button createGameSceneButton;
    private Button joinGameSceneButton;
    private Text multiplayerStatusLabel;
    private Text settingsStatusLabel;
    private Text joinRoomStatusLabel;
    private Text joinRoomEmptyLabel;
    private Text victoryDestroyedCountLabel;
    private Text defeatDestroyedCountLabel;
    private ScrollRect joinRoomScrollRect;
    private RectTransform joinRoomContent;
    private readonly List<Button> joinRoomButtons = new List<Button>();
    private readonly List<Text> joinRoomButtonLabels = new List<Text>();
    private bool menuButtonMissingSpriteWarningLogged;
    private bool menuButtonCapacityWarningLogged;
    private bool menuButtonSpritesInitialized;
    private bool menuBackgroundTextureWarningLogged;
    private bool gameplayStatusVisibilityInitialized;
    private bool gameplayStatusBarsVisible;
    private readonly Dictionary<string, MenuButtonSpriteSet>
        menuButtonSpriteSets =
            new Dictionary<string, MenuButtonSpriteSet>(
                System.StringComparer.OrdinalIgnoreCase
            );
    private readonly List<MenuButtonView> menuButtonViews =
        new List<MenuButtonView>();
    private int menuButtonDrawIndex;

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
        RestoreSceneCameraFollow();
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
        bool startSinglePlay = startSinglePlayOnSceneLoad;
        startSinglePlayOnSceneLoad = false;
        if (startSinglePlay)
            showMainMenu = false;

        isFullscreen = Screen.fullScreen;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].x == Screen.width &&
                resolutions[i].y == Screen.height)
            {
                selectedResolution = i;
                break;
            }
        }

        if (showMainMenu)
        {
            showMultiplayerMenu = false;
            showJoinRoomMenu = false;
            showSettings = false;
            showControlSettings = false;
        }

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
        sceneCameraFollow = FindObjectOfType<TankCameraFollow>(true);

        InitializeSceneMenuUI();
        UpdateSceneMenuCanvasVisibility();

        if (startSinglePlay)
        {
            StartGame(GameMode.SinglePlay);
        }
        else if (showMainMenu)
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

}
