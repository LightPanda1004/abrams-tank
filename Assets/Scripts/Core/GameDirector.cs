
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;

public partial class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    private static bool startSinglePlayOnSceneLoad;

    public enum GameMode
    {
        SinglePlay,
        Online
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
    private bool showOnlineMenu;
    private bool showEconomyMenu;
    private bool onlineRegistrationMode;
    private bool onlineRequestInProgress;

    [Header("Online Service")]
    [SerializeField]
    private string onlineServiceUrl = "http://localhost:3000";

    [SerializeField]
    private GameObject reownAppKitPrefab;

    [SerializeField]
    private string reownProjectId = "ca5c7f02f2b05d31ebfa25a0a397d00e";

    [SerializeField]
    private string tankCoinContractAddress =
        "0x4fc5efd55b1c845e2e88b8431C0d018a6E5331FC";

    [SerializeField]
    private string tankGameContractAddress =
        "0xed892a064b18F60b823634E4eaD74851FA6F29b6";

    private string onlineAuthToken = "";
    private string onlineUsernameInput = "";
    private string onlinePasswordInput = "";
    private string onlineWalletAddress = "";
    private string onlineWalletAddressInput = "";
    private string onlineWalletSignatureInput = "";
    private string onlineWalletChallengeMessage = "";
    private string onlineWalletChallengeNonce = "";
    private OnlineProfileResponse onlineProfile;
    private bool onlineProfileRequestInProgress;
    private string onlinePlayerNameInput = "Player";
    private string onlineRoomNameInput = "Abrams Room";
    private string onlineStatusMessage = "";
    private readonly List<OnlineRoomInfo> onlineRooms =
        new List<OnlineRoomInfo>();

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
    private readonly List<Texture2D> generatedGuiTextures =
        new List<Texture2D>();

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
    private OnlineGameServer onlineGameServer;
    private readonly Dictionary<int, TankTeam> approvedClientTeams =
        new Dictionary<int, TankTeam>();
    private readonly Dictionary<int, string> approvedClientNames =
        new Dictionary<int, string>();
    private readonly Dictionary<int, string> approvedClientUserIds =
        new Dictionary<int, string>();
    private readonly Dictionary<int, string> approvedClientWalletAddresses =
        new Dictionary<int, string>();
    private readonly Dictionary<int, string> onlineMatchUserIds =
        new Dictionary<int, string>();
    private readonly Dictionary<int, TankTeam> onlineMatchTeams =
        new Dictionary<int, TankTeam>();
    private readonly Dictionary<int, int> onlineMatchKills =
        new Dictionary<int, int>();
    private readonly HashSet<int> pendingOnlineClients =
        new HashSet<int>();
    private readonly Dictionary<int, Vector3> approvedClientSpawnPositions =
        new Dictionary<int, Vector3>();
    private TankTeam selectedTeam = TankTeam.Unassigned;
    private Vector2 roomListScroll;

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

    private GUIStyle statsStyle;
    private GUIStyle titleStyle;
    private GUIStyle menuPanelStyle;
    private GUIStyle menuTitleStyle;
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

    private Text settingsStatusLabel;
    private Text victoryDestroyedCountLabel;
    private Text defeatDestroyedCountLabel;
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
    private string onlineKillRewardWei;

    [Serializable]
    private sealed class OnlineUserInfo
    {
        public string id = "";
        public string username = "";
        public string walletAddress = "";
    }

    [Serializable]
    private sealed class OnlineAuthResponse
    {
        public string token = "";
        public OnlineUserInfo user = null;
        public string error = "";
    }

    [Serializable]
    private sealed class OnlineErrorResponse
    {
        public string error = "";
        public string requestId = "";
    }

    [Serializable]
    private sealed class OnlineRoomInfo
    {
        public string id = "";
        public string name = "";
        public string status = "";
        public int max_players = 0;
        public int player_count = 0;
        public int other_player_count = 0;
        public bool server_ready;
        public string host = "";
        public int port = 0;
        public string ticket = "";
        public int team;
        public string player_name = "";
    }

    [Serializable]
    private sealed class OnlineRoomListResponse
    {
        public OnlineRoomInfo[] rooms = null;
        public string error = "";
    }

    [Serializable]
    private sealed class OnlineRoomResponse
    {
        public OnlineRoomInfo room = null;
        public string error = "";
    }

    [Serializable]
    private sealed class OnlineServerAssignmentResponse
    {
        public OnlineServerAssignment room = null;
    }

    [Serializable]
    private sealed class OnlineServerAssignment
    {
        public string id = "";
        public string name = "";
        public string status = "";
        public string start_mode = "";
    }

    [Serializable]
    private sealed class OnlineTicketResponse
    {
        public string roomId = "";
        public string userId = "";
        public int team = 0;
        public string playerName = "";
        public string playerAddress = "";
        public string error = "";
    }

    [Serializable]
    private sealed class OnlineWalletChallengeResponse
    {
        public string nonce = "";
        public string message = "";
        public string error = "";
    }

    [Serializable]
    private sealed class OnlineWalletResponse
    {
        public string walletAddress = "";
        public string error = "";
    }

    [Serializable]
    private sealed class OnlinePlayerMatchResult
    {
        public string userId = "";
        public int team;
        public int kills;
    }

    [Serializable]
    private sealed class OnlineMatchResultsRequest
    {
        public int winningTeam = -1;
        public OnlinePlayerMatchResult[] players = null;
    }

    [Serializable]
    private sealed class OnlineProfileResponse
    {
        public string playerId = "";
        public string username = "";
        public string walletAddress = "";
        public int level;
        public string experience = "0";
        public string battlesPlayed = "0";
        public string victories = "0";
        public string defeats = "0";
        public string tanksDestroyed = "0";
        public string error = "";
    }

    [Serializable]
    private sealed class OnlineRequestData
    {
        public string username;
        public string password;
        public string name;
        public string team;
        public string playerName;
        public string id;
        public string host;
        public string ticket;
        public int port;
        public int maxPlayers;
        public string serverId;
        public string roomId;
        public string accessToken;
        public string userId;
        public string[] userIds;
        public string address;
        public string nonce;
        public string signature;
        public string playerAddress;
        public string amountWei;
        public string reason;
        public string battleId;
    }

    public bool IsGameOver => gameOver;
    public bool IsPaused => showPauseMenu || showMainMenu || finishMenuVisible;

    public bool IsMultiplayerMode =>
        currentGameMode != GameMode.SinglePlay;

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
        StartWalletIntegration();

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
            showSettings = false;
            showControlSettings = false;
        }

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

    public void DisconnectWallet()
    {
        onlineWalletAddress = "";
        onlineWalletAddressInput = "";
        onlineWalletSignatureInput = "";
        onlineWalletChallengeMessage = "";
        onlineWalletChallengeNonce = "";
        connectedWalletAddress = "";
        walletContractsReady = false;
        tcoinBalanceLabel = "Not connected";
        walletEconomyStatus = "";
        onlineStatusMessage = "Wallet disconnected.";
    }

}
