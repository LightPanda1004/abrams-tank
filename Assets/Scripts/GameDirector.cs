
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    [Header("Battle Statistics")]
    public int totalAITanks = 0;
    public int destroyedTanks = 0;

    [Header("Player")]
    public TankHealth playerHealth;

    [Header("Turret Controls")]
    [Tooltip("True = mouse control, false = keyboard control.")]
    public bool MouseTurretControlEnabled = true;

    [Header("Startup")]
    public bool showMainMenu = true;

    private readonly HashSet<int> registeredTanks = new HashSet<int>();
    private readonly HashSet<int> destroyedTankIds = new HashSet<int>();

    private bool gameOver;
    private bool victory;
    private bool showPauseMenu;
    private bool showSettings;
    private bool showControlSettings;

    private bool isFullscreen;
    private int selectedResolution = 1;

    private readonly Vector2Int[] resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440)
    };

    private GUIStyle statsStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;

    public bool IsGameOver => gameOver;
    public bool IsPaused => showPauseMenu || showMainMenu;

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
        Cursor.visible = true;
        Cursor.lockState = visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;
    }

    public void RegisterAITank(TankHealth tank)
    {
        if (tank == null || tank.isPlayerTank)
            return;

        int id = tank.gameObject.GetInstanceID();

        if (registeredTanks.Add(id))
        {
            totalAITanks++;
            Debug.Log("AI tanks registered: " + totalAITanks);
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

        int id = tank.gameObject.GetInstanceID();

        if (!registeredTanks.Contains(id))
            return;

        if (!destroyedTankIds.Add(id))
            return;

        destroyedTanks++;

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

        Time.timeScale = 0f;
        SetCursor(true);

        Debug.Log(playerWon ? "VICTORY!" : "GAME OVER");
    }

    private void StartGame()
    {
        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;

        Time.timeScale = 1f;
        SetCursor(false);
    }

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

        // The scene reloads; enable the startup menu in the Inspector
        // if you want it to appear again after reloading.
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

    private void OnGUI()
    {
        CreateStyles();

        if (showMainMenu)
        {
            DrawMenuPanel("LEOPARD BATTLE", false);
            return;
        }

        if (showPauseMenu)
        {
            DrawMenuPanel("GAME PAUSED", true);
            return;
        }

        DrawBattleStatistics();

        if (gameOver)
            DrawGameOverMenu();
    }

    private void DrawMenuPanel(string title, bool isPauseMenu)
    {
        float width = 380f;
        float height;

        if (showControlSettings)
            height = 410f;
        else if (showSettings)
            height = 400f;
        else
            height = isPauseMenu ? 360f : 310f;

        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "");

        GUI.Label(
            new Rect(x + 20, y + 15, width - 40, 60),
            title,
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

        if (!isPauseMenu)
        {
            if (GUI.Button(
                new Rect(x + 50, y + 100, width - 100, 45),
                "START",
                buttonStyle))
            {
                StartGame();
            }

            if (GUI.Button(
                new Rect(x + 50, y + 160, width - 100, 45),
                "SETTINGS",
                buttonStyle))
            {
                showSettings = true;
            }

            if (GUI.Button(
                new Rect(x + 50, y + 220, width - 100, 45),
                "EXIT",
                buttonStyle))
            {
                QuitGame();
            }
        }
        else
        {
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
    }

    private void DrawSettings(float x, float y, float width)
    {
        GUI.Label(
            new Rect(x + 30, y + 80, width - 60, 30),
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
            new Rect(x + 30, y + 110, width - 60, 75),
            selectedResolution,
            options,
            1
        );

        isFullscreen = GUI.Toggle(
            new Rect(x + 30, y + 190, width - 60, 30),
            isFullscreen,
            " Fullscreen"
        );

        if (GUI.Button(
            new Rect(x + 30, y + 225, width - 60, 35),
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
            new Rect(x + 30, y + 270, width - 60, 35),
            "CONTROL SETTINGS",
            buttonStyle))
        {
            showControlSettings = true;
        }

        if (GUI.Button(
            new Rect(x + 30, y + 315, width - 60, 35),
            "BACK",
            buttonStyle))
        {
            showSettings = false;
        }
    }

    private void DrawControlSettings(float x, float y, float width)
    {
        GUI.Label(
            new Rect(x + 20, y + 80, width - 40, 35),
            "TURRET CONTROL METHOD",
            statsStyle
        );

        if (GUI.Button(
            new Rect(x + 30, y + 125, width - 60, 45),
            !MouseTurretControlEnabled
                ? "✓ Keyboard Aiming (J/L, I/K)"
                : "Keyboard Aiming (J/L, I/K)",
            buttonStyle))
        {
            MouseTurretControlEnabled = false;
        }

        if (GUI.Button(
            new Rect(x + 30, y + 180, width - 60, 45),
            MouseTurretControlEnabled
                ? "✓ Mouse Aiming"
                : "Mouse Aiming",
            buttonStyle))
        {
            MouseTurretControlEnabled = true;
        }

        GUI.Label(
            new Rect(x + 30, y + 235, width - 60, 55),
            MouseTurretControlEnabled
                ? "Move mouse to aim; left-click to fire."
                : "J/L rotate, I/K elevate, Space fires.",
            statsStyle
        );

        if (GUI.Button(
            new Rect(x + 50, y + 330, width - 100, 35),
            "BACK",
            buttonStyle))
        {
            showControlSettings = false;
        }
    }

    private void DrawBattleStatistics()
    {
        GUI.Box(
            new Rect(Screen.width - 260, 15, 245, 80),
            ""
        );

        GUI.Label(
            new Rect(Screen.width - 245, 25, 220, 28),
            "Total AI Tanks: " + totalAITanks,
            statsStyle
        );

        GUI.Label(
            new Rect(Screen.width - 245, 55, 220, 28),
            "Destroyed Tanks: " + destroyedTanks,
            statsStyle
        );
    }

    private void DrawGameOverMenu()
    {
        float width = 360f;
        float height = 300f;

        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "");

        GUI.Label(
            new Rect(x + 20, y + 20, width - 40, 50),
            victory ? "VICTORY!" : "GAME OVER",
            titleStyle
        );

        GUI.Label(
            new Rect(x + 20, y + 80, width - 40, 30),
            "Destroyed AI Tanks: " + destroyedTanks +
            " / " + totalAITanks,
            statsStyle
        );

        if (GUI.Button(
            new Rect(x + 50, y + 125, width - 100, 40),
            "PLAY AGAIN",
            buttonStyle))
        {
            PlayAgain();
        }

        if (GUI.Button(
            new Rect(x + 50, y + 175, width - 100, 40),
            "MAIN MENU",
            buttonStyle))
        {
            ReturnToMainMenu();
        }

        if (GUI.Button(
            new Rect(x + 50, y + 225, width - 100, 35),
            "EXIT",
            buttonStyle))
        {
            QuitGame();
        }
    }

    private void CreateStyles()
    {
        if (statsStyle != null)
            return;

        statsStyle = new GUIStyle(GUI.skin.label);
        statsStyle.fontSize = 18;
        statsStyle.fontStyle = FontStyle.Bold;
        statsStyle.normal.textColor = Color.white;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 17;
        buttonStyle.fontStyle = FontStyle.Bold;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Time.timeScale = 1f;
    }
}
