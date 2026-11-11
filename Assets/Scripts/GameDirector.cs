
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    private static bool startSinglePlayOnSceneLoad;

    [Header("Battle Statistics")]
    public int totalAITanks = 0;
    public int destroyedTanks = 0;

    [Header("Player")]
    public TankHealth playerHealth;
    public TankMovement playerMovement;

    [Header("Turret Controls")]
    public bool MouseTurretControlEnabled = true;

    [Header("Startup")]
    public bool showMainMenu = true;

    [Header("Game Over")]
    public float finishMenuDelay = 2f;

    private readonly HashSet<int> registeredTanks = new HashSet<int>();
    private readonly HashSet<int> destroyedTankIds = new HashSet<int>();

    private bool gameOver;
    private bool victory;
    private bool finishMenuVisible;

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
    private string onlineStatusMessage = "";

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
    private readonly List<Texture2D> generatedGuiTextures =
        new List<Texture2D>();

    private TankFire playerTankFire;
    private AntiDroneLauncher playerMissileLauncher;
    private TankHealth scenePlayerHealth;
    private TankMovement scenePlayerMovement;
    private TankCameraFollow sceneCameraFollow;
    private Camera missileViewCamera;
    private bool missileViewVisible;
    private bool missileViewBlack;
    private GUIStyle statsStyle;
    private GUIStyle menuPanelStyle;
    private GUIStyle menuTitleStyle;
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
    private bool gameplayStatusVisibilityInitialized;
    private bool gameplayStatusBarsVisible;
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
        public string address;
        public string nonce;
        public string signature;
        public string name;
        public string playerName;
        public string team;
    }

    public bool IsGameOver => gameOver;
    public bool IsPaused => showPauseMenu || showMainMenu || finishMenuVisible;

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

        UpdateGameplayStatusBarsVisibility();

        if (startSinglePlay)
        {
            StartGame();
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
