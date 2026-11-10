using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mirror;

public partial class GameDirector
{
    private void OnGUI()
    {
        CreateStyles();

        if (IsReownWalletModalOpen())
            return;

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

        if (!finishMenuVisible)
            DrawBattleHUD();

        if (finishMenuVisible)
            DrawGameOverMenu();

        DrawMissileView();
    }

    private Texture2D LoadMenuTexture(string resourcePath)
    {
        if (menuTextures.TryGetValue(
            resourcePath,
            out Texture2D cachedTexture))
        {
            return cachedTexture;
        }

        Texture2D texture = Resources.Load<Texture2D>(
            "LeopardBattle/" + resourcePath
        );
        if (texture == null)
        {
            Debug.LogWarning(
                "GameDirector: Could not load UI texture at " +
                "Resources/LeopardBattle/" + resourcePath,
                this
            );
        }

        menuTextures.Add(resourcePath, texture);
        return texture;
    }

    private void DrawMenuTexture(
        string resourcePath,
        Rect rect,
        ScaleMode scaleMode = ScaleMode.StretchToFill)
    {
        Texture2D texture = LoadMenuTexture(resourcePath);
        if (texture == null)
            return;

        GUI.DrawTexture(rect, texture, scaleMode, true);
    }

    private bool DrawMenuButton(
        Rect rect,
        string label,
        GUIStyle style = null)
    {
        return GUI.Button(rect, label, style ?? buttonStyle);
    }

    private string GetMenuButtonSpriteGroup(string label)
    {
        if (ContainsIgnoreCase(label, "SINGLE PLAY") ||
            ContainsIgnoreCase(label, "SINGLE PLAYER"))
            return "SinglePlay";
        if (ContainsIgnoreCase(label, "MULTIPLAYER") ||
            ContainsIgnoreCase(label, "BLUE TEAM") ||
            ContainsIgnoreCase(label, "RED TEAM"))
            return "Multiplayer";
        if (ContainsIgnoreCase(label, "CONTROL SETTINGS"))
            return "ControlSettings";
        if (ContainsIgnoreCase(label, "SETTINGS") ||
            ContainsIgnoreCase(label, "RESOLUTION"))
            return "Settings";
        if (ContainsIgnoreCase(label, "CONTINUE"))
            return "Multiplayer";
        if (ContainsIgnoreCase(label, "RESUME") ||
            ContainsIgnoreCase(label, "PLAY AGAIN"))
            return "SinglePlay";
        if (ContainsIgnoreCase(label, "EXIT"))
            return "Exit";
        if (ContainsIgnoreCase(label, "CREATE"))
            return "CreateGame";
        if (ContainsIgnoreCase(label, "JOIN") ||
            ContainsIgnoreCase(label, "ROOM"))
            return "JoinGame";
        if (ContainsIgnoreCase(label, "REFRESH"))
            return "Refresh";
        if (ContainsIgnoreCase(label, "APPLY"))
            return "Apply";
        if (ContainsIgnoreCase(label, "BACK") ||
            ContainsIgnoreCase(label, "MAIN MENU"))
            return "Back";

        return "Back";
    }

    private static bool ContainsIgnoreCase(string value, string search)
    {
        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void EnsureMenuButtonCanvas()
    {
        if (menuButtonSpritesInitialized)
            return;

        if (menuButtonCanvas == null)
        {
            Debug.LogError(
                "GameDirector: Assign the menu button Canvas in the scene.",
                this
            );
        }

        menuButtonViews.Clear();
        if (menuSceneButtons != null)
        {
            foreach (Button button in menuSceneButtons)
            {
                if (button == null)
                {
                    Debug.LogError(
                        "GameDirector: Assign every menu button in the " +
                        "scene button list.",
                        this
                    );
                    continue;
                }

                Image image = button.GetComponent<Image>();
                RectTransform rectTransform =
                    button.transform as RectTransform;
                if (image == null || rectTransform == null)
                {
                    Debug.LogError(
                        "GameDirector: Each scene menu button must have an " +
                        "Image and RectTransform.",
                        button
                    );
                    continue;
                }

                MenuButtonView view = new MenuButtonView
                {
                    gameObject = button.gameObject,
                    rectTransform = rectTransform,
                    button = button,
                    image = image
                };
                button.targetGraphic = image;
                button.transition = Selectable.Transition.SpriteSwap;
                button.navigation = new Navigation
                {
                    mode = Navigation.Mode.None
                };
                image.raycastTarget = true;
                button.onClick.AddListener(() => view.wasClicked = true);
                menuButtonViews.Add(view);
            }
        }
        else
        {
            Debug.LogError(
                "GameDirector: Assign scene menu buttons in the Inspector.",
                this
            );
        }

        menuButtonSpriteSets.Clear();
        if (menuButtonSprites != null)
        {
            foreach (Sprite sprite in menuButtonSprites)
            {
                if (sprite == null)
                    continue;

                string spriteName = sprite.name;
                int suffixStart = spriteName.LastIndexOf('_');
                if (suffixStart < 0 || suffixStart == spriteName.Length - 1)
                    continue;

                string state = spriteName.Substring(suffixStart + 1);
                string groupName = spriteName.Substring(0, suffixStart);
                if (groupName.EndsWith(
                    "btn",
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    groupName = groupName.Substring(0, groupName.Length - 3);
                }

                if (!menuButtonSpriteSets.TryGetValue(
                    groupName,
                    out MenuButtonSpriteSet spriteSet))
                {
                    spriteSet = new MenuButtonSpriteSet();
                    menuButtonSpriteSets.Add(groupName, spriteSet);
                }

                if (state.Equals(
                    "normal",
                    System.StringComparison.OrdinalIgnoreCase))
                    spriteSet.normal = sprite;
                else if (state.Equals(
                    "hover",
                    System.StringComparison.OrdinalIgnoreCase))
                    spriteSet.hover = sprite;
                else if (state.Equals(
                    "select",
                    System.StringComparison.OrdinalIgnoreCase))
                    spriteSet.hover = sprite;
                else if (state.Equals(
                    "press",
                    System.StringComparison.OrdinalIgnoreCase))
                    spriteSet.pressed = sprite;
            }
        }

        foreach (KeyValuePair<string, MenuButtonSpriteSet> entry
            in menuButtonSpriteSets)
        {
            if (entry.Value.normal == null ||
                entry.Value.hover == null ||
                entry.Value.pressed == null)
            {
                Debug.LogWarning(
                    "GameDirector: ButtonGroup sprite set " + entry.Key +
                    " does not contain normal, hover, and press sprites.",
                    this
                );
            }
        }

        menuButtonSpritesInitialized = true;
    }

    private MenuButtonView GetMenuButtonView(int index)
    {
        if (index >= menuButtonViews.Count)
        {
            if (!menuButtonCapacityWarningLogged)
            {
                Debug.LogError(
                    "GameDirector: The scene does not contain enough menu " +
                    "buttons for the current menu.",
                    this
                );
                menuButtonCapacityWarningLogged = true;
            }
            return null;
        }

        return menuButtonViews[index];
    }

    private void HideUnusedMenuButtons()
    {
        for (int i = menuButtonDrawIndex; i < menuButtonViews.Count; i++)
            menuButtonViews[i].gameObject.SetActive(false);

        if (menuButtonDrawIndex == 0 && menuButtonCanvas != null)
            menuButtonCanvas.enabled = false;
    }

    private void HideMenuButtons()
    {
        menuButtonDrawIndex = 0;
        HideUnusedMenuButtons();
    }

    private Rect GetMenuPanelRect(float layoutWidth, float layoutHeight)
    {
        float scale = Mathf.Min(
            Mathf.Min(Screen.width * 0.9f / layoutWidth,
                Screen.height * 0.9f / layoutHeight),
            1.4f
        );
        float width = layoutWidth * scale;
        float height = layoutHeight * scale;
        return new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
        );
    }

    private void DrawMenuPanel(Rect panel, string title)
    {
        GUI.Box(panel, GUIContent.none, menuPanelStyle);
        menuTitleStyle.fontSize = Mathf.Clamp(
            Mathf.RoundToInt(panel.width * 0.065f),
            20,
            32
        );
        GUI.Label(
            new Rect(panel.x + 30f, panel.y + 20f, panel.width - 60f, 48f),
            title,
            menuTitleStyle
        );
    }

    private void EnsureMenuPanelCanvas()
    {
        if (menuPanelCanvas != null)
            return;

        if (!menuPanelMissingSpriteWarningLogged)
        {
            Debug.LogError(
                "GameDirector: Assign the menu panel Canvas and Image " +
                "in the scene.",
                this
            );
            menuPanelMissingSpriteWarningLogged = true;
        }
    }

    private void HideMenuPanelCanvas()
    {
        if (menuPanelCanvas != null)
            menuPanelCanvas.enabled = false;
    }

    private void HideLegacyMenuPanelGraphics()
    {
        if (menuPanelImage != null)
            menuPanelImage.enabled = false;
        if (menuBackgroundImage != null)
            menuBackgroundImage.enabled = false;
    }

    private void ShowMenuPanelCanvas(Rect panel, Sprite panelSprite)
    {
        if (panelSprite == null)
        {
            HideMenuPanelCanvas();
            if (!menuPanelMissingSpriteWarningLogged)
            {
                Debug.LogWarning(
                    "GameDirector: Assign a menu panel sprite before " +
                    "displaying the menu.",
                    this
                );
                menuPanelMissingSpriteWarningLogged = true;
            }
            return;
        }

        if (menuBackgroundImage == null || menuBackgroundImage.sprite == null)
        {
            if (!menuBackgroundMissingSpriteWarningLogged)
            {
                Debug.LogWarning(
                    "GameDirector: Assign a menu background sprite in the " +
                    "scene.",
                    this
                );
                menuBackgroundMissingSpriteWarningLogged = true;
            }
        }
        else
        {
            menuBackgroundImage.enabled = true;
            if (menuBackgroundImage.transform.GetSiblingIndex() != 0)
                menuBackgroundImage.transform.SetAsFirstSibling();
        }

        EnsureMenuPanelCanvas();
        if (menuPanelCanvas == null ||
            menuPanelImage == null ||
            menuPanelRectTransform == null)
        {
            return;
        }

        menuPanelCanvas.enabled = true;
        menuPanelImage.enabled = true;
        menuPanelImage.sprite = panelSprite;

        RectTransform canvasRectTransform =
            menuPanelCanvas.GetComponent<RectTransform>();
        float scaleX = canvasRectTransform.rect.width /
            Mathf.Max(Screen.width, 1f);
        float scaleY = canvasRectTransform.rect.height /
            Mathf.Max(Screen.height, 1f);

        menuPanelRectTransform.anchorMin = new Vector2(0f, 1f);
        menuPanelRectTransform.anchorMax = new Vector2(0f, 1f);
        menuPanelRectTransform.pivot = new Vector2(0f, 1f);
        menuPanelRectTransform.anchoredPosition = new Vector2(
            panel.x * scaleX,
            -panel.y * scaleY
        );
        menuPanelRectTransform.sizeDelta = new Vector2(
            panel.width * scaleX,
            panel.height * scaleY
        );
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
        float layoutWidth = showEconomyMenu
            ? 860f
            : showOnlineMenu ? 640f : 420f;
        float layoutHeight = showEconomyMenu
            ? 830f
            : showOnlineMenu
            ? string.IsNullOrEmpty(onlineAuthToken) ? 430f
                :                 string.IsNullOrEmpty(onlineWalletAddress) ? 1100f : 860f
            : showSettings || showControlSettings
                ? 440f
                : 470f;
        Rect panel = GetMenuPanelRect(layoutWidth, layoutHeight);
        float width = panel.width;
        float height = panel.height;
        float x = panel.x;
        float y = panel.y;

        string title = showEconomyMenu
            ? "TCOIN ECONOMY"
            : showOnlineMenu
                ? "ONLINE BATTLE"
                : showControlSettings
                    ? "CONTROL SETTINGS"
                    : showSettings ? "SETTINGS" : "LEOPARD BATTLE";
        DrawMenuPanel(panel, title);

        float scaleX = width / layoutWidth;
        float scaleY = height / layoutHeight;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(
            new Vector3(x * (1f - scaleX), y * (1f - scaleY), 0f),
            Quaternion.identity,
            new Vector3(scaleX, scaleY, 1f)
        );

        if (showEconomyMenu)
        {
            DrawWalletEconomyMenu(x, y, layoutWidth);
            GUI.matrix = previousMatrix;
            return;
        }

        if (showOnlineMenu)
        {
            DrawOnlineMenu(x, y, layoutWidth);
            GUI.matrix = previousMatrix;
            return;
        }

        if (showControlSettings)
        {
            DrawControlSettings(x, y, layoutWidth);
            GUI.matrix = previousMatrix;
            return;
        }

        if (showSettings)
        {
            DrawSettings(x, y, layoutWidth);
            GUI.matrix = previousMatrix;
            return;
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 90, layoutWidth - 100, 48),
            "SINGLE PLAY",
            buttonStyle))
        {
            StartGame(GameMode.SinglePlay);
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 210, layoutWidth - 100, 48),
            "ONLINE BATTLE",
            buttonStyle))
        {
            OpenOnlineMenu();
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 270, layoutWidth - 100, 48),
            "SETTINGS",
            buttonStyle))
        {
            showSettings = true;
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 330, layoutWidth - 100, 48),
            "EXIT",
            buttonStyle))
        {
            QuitGame();
        }
        GUI.matrix = previousMatrix;
    }

    private void DrawOnlineMenu(float x, float y, float width)
    {
        float walletOffset = string.IsNullOrEmpty(onlineWalletAddress)
            ? 340f
            : 160f;
        GUI.Label(
            new Rect(x + 42f, y + 86f, width - 84f, 24f),
            onlineStatusMessage,
            statsStyle
        );
        if (onlineProfile != null)
        {
            GUI.Label(
                new Rect(x + 42f, y + 112f, width - 84f, 50f),
                onlineProfile.username + "   ID " + onlineProfile.playerId +
                    "   LVL " + onlineProfile.level +
                    "   XP " + onlineProfile.experience +
                    "\nBATTLES " + onlineProfile.battlesPlayed +
                    "   W " + onlineProfile.victories +
                    "   L " + onlineProfile.defeats +
                    "   TANKS DESTROYED " + onlineProfile.tanksDestroyed,
                statsStyle
            );
        }
        else if (onlineProfileRequestInProgress)
        {
            GUI.Label(
                new Rect(x + 42f, y + 112f, width - 84f, 24f),
                "Loading player profile...",
                statsStyle
            );
        }

        if (string.IsNullOrEmpty(onlineAuthToken))
        {
            GUI.Label(
                new Rect(x + 42f, y + 126f, width - 84f, 22f),
                "USERNAME",
                statsStyle
            );
            onlineUsernameInput = GUI.TextField(
                new Rect(x + 42f, y + 150f, width - 84f, 38f),
                onlineUsernameInput,
                24,
                menuInputStyle
            );
            GUI.Label(
                new Rect(x + 42f, y + 204f, width - 84f, 22f),
                "PASSWORD",
                statsStyle
            );
            onlinePasswordInput = GUI.PasswordField(
                new Rect(x + 42f, y + 228f, width - 84f, 38f),
                onlinePasswordInput,
                '*',
                128,
                menuInputStyle
            );
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !onlineRequestInProgress &&
                !string.IsNullOrWhiteSpace(onlineUsernameInput) &&
                !string.IsNullOrEmpty(onlinePasswordInput);
            if (DrawMenuButton(
                new Rect(x + 42f, y + 284f, width - 84f, 42f),
                onlineRegistrationMode ? "CREATE ACCOUNT" : "LOG IN",
                buttonStyle))
                SubmitOnlineAuthentication();
            GUI.enabled = previousEnabled;

            if (DrawMenuButton(
                new Rect(x + 42f, y + 336f, width - 84f, 38f),
                onlineRegistrationMode
                    ? "ALREADY HAVE AN ACCOUNT? LOG IN"
                    : "NEW PLAYER? CREATE ACCOUNT",
                buttonStyle))
            {
                onlineRegistrationMode = !onlineRegistrationMode;
                onlineStatusMessage = onlineRegistrationMode
                    ? "Use a 3-24 character username and a 10+ character password with a letter and number."
                    : "";
            }
            if (DrawMenuButton(
                new Rect(x + 42f, y + 382f, width - 84f, 32f),
                "BACK",
                buttonStyle))
            {
                showOnlineMenu = false;
                onlineStatusMessage = "";
            }
            return;
        }

        GUI.Label(
            new Rect(x + 42f, y + 175f, width - 84f, 22f),
            "PLAYER NAME",
            statsStyle
        );
        onlinePlayerNameInput = GUI.TextField(
            new Rect(x + 42f, y + 199f, width - 84f, 36f),
            onlinePlayerNameInput,
            24,
            menuInputStyle
        );
        if (string.IsNullOrEmpty(onlineWalletAddress))
        {
            GUI.Label(
                new Rect(x + 42f, y + 243f, width - 84f, 22f),
                "REWARD WALLET ADDRESS",
                statsStyle
            );
            onlineWalletAddressInput = GUI.TextField(
                new Rect(x + 42f, y + 267f, width - 84f, 36f),
                onlineWalletAddressInput,
                42,
                menuInputStyle
            );

            bool previousWalletControlsEnabled = GUI.enabled;
            float walletButtonWidth = (width - 100f) * 0.5f;
            GUI.enabled = walletKitReady;
            if (DrawMenuButton(
                new Rect(x + 42f, y + 309f, walletButtonWidth, 32f),
                WalletConnectButtonLabel(),
                buttonStyle))
                ConnectOnlineWallet();

            GUI.enabled = walletKitReady;
            if (DrawMenuButton(
                new Rect(x + 58f + walletButtonWidth, y + 309f,
                    walletButtonWidth, 32f),
                WalletSearchButtonLabel(),
                buttonStyle))
                OpenMetaMaskWalletSearch();

            GUI.enabled = previousWalletControlsEnabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            const float manualWalletOffset = 24f;
            GUI.Label(
                new Rect(x + 42f, y + 338f, width - 84f, 22f),
                "Chrome + MetaMask extension works without a phone.",
                statsStyle
            );
#else
            const float manualWalletOffset = 0f;
#endif
            GUI.Label(
                new Rect(
                    x + 42f,
                    y + 345f + manualWalletOffset,
                    width - 84f,
                    22f
                ),
                "Or link another wallet manually:",
                statsStyle
            );
            GUI.enabled = !onlineRequestInProgress &&
                !string.IsNullOrWhiteSpace(onlineWalletAddressInput);
            if (DrawMenuButton(
                new Rect(
                    x + 42f,
                    y + 369f + manualWalletOffset,
                    width - 84f,
                    30f
                ),
                "GET / COPY MANUAL CHALLENGE",
                buttonStyle))
                RequestOnlineWalletChallenge();

            GUI.enabled = previousWalletControlsEnabled;
            GUI.Label(
                new Rect(
                    x + 42f,
                    y + 403f + manualWalletOffset,
                    width - 84f,
                    22f
                ),
                "WALLET SIGNATURE",
                statsStyle
            );
            onlineWalletSignatureInput = GUI.TextField(
                new Rect(
                    x + 42f,
                    y + 427f + manualWalletOffset,
                    width - 84f,
                    36f
                ),
                onlineWalletSignatureInput,
                132,
                menuInputStyle
            );
            GUI.enabled = !onlineRequestInProgress &&
                !string.IsNullOrEmpty(onlineWalletChallengeNonce) &&
                !string.IsNullOrWhiteSpace(onlineWalletSignatureInput);
            if (DrawMenuButton(
                new Rect(
                    x + 42f,
                    y + 469f + manualWalletOffset,
                    width - 84f,
                    34f
                ),
                "VERIFY AND LINK WALLET",
                buttonStyle))
                VerifyOnlineWallet();
            GUI.enabled = previousWalletControlsEnabled;
        }
        else
        {
            GUI.Label(
                new Rect(x + 42f, y + 243f, width - 84f, 22f),
                "VERIFIED REWARD WALLET",
                statsStyle
            );
            GUI.Label(
                new Rect(x + 42f, y + 267f, width - 84f, 38f),
                onlineWalletAddress,
                statsStyle
            );
            GUI.Label(
                new Rect(x + 42f, y + 305f, width - 84f, 22f),
                "This wallet is permanently linked to this account.",
                statsStyle
            );
            bool previousWalletEnabled = GUI.enabled;
            GUI.enabled = !onlineWalletActionInProgress;
            if (DrawMenuButton(
                new Rect(x + 42f, y + 331f, width - 84f, 34f),
                "DISCONNECT WALLET",
                buttonStyle))
                DisconnectWallet();
            GUI.enabled = previousWalletEnabled;
        }

        GUI.Label(
            new Rect(x + 42f, y + 183f + walletOffset,
                width - 84f, 22f),
            "SELECT YOUR TEAM",
            statsStyle
        );
        float teamButtonWidth = (width - 100f) * 0.5f;
        if (DrawMenuButton(
            new Rect(x + 42f, y + 207f + walletOffset,
                teamButtonWidth, 38f),
            "BLUE TEAM",
            selectedTeam == TankTeam.Blue
                ? selectedButtonStyle
                : buttonStyle))
            selectedTeam = TankTeam.Blue;
        if (DrawMenuButton(
            new Rect(x + 58f + teamButtonWidth, y + 207f + walletOffset,
                teamButtonWidth, 38f),
            "RED TEAM",
            selectedTeam == TankTeam.Red
                ? selectedButtonStyle
                : buttonStyle))
            selectedTeam = TankTeam.Red;

        GUI.Label(
            new Rect(x + 42f, y + 253f + walletOffset,
                width - 84f, 22f),
            "ROOM NAME",
            statsStyle
        );
        onlineRoomNameInput = GUI.TextField(
            new Rect(x + 42f, y + 277f + walletOffset,
                width - 84f, 36f),
            onlineRoomNameInput,
            32,
            menuInputStyle
        );

        bool previousControlsEnabled = GUI.enabled;
        GUI.enabled = !onlineRequestInProgress &&
            selectedTeam != TankTeam.Unassigned &&
            !string.IsNullOrWhiteSpace(onlinePlayerNameInput);
        float actionWidth = (width - 100f) * 0.5f;
        if (DrawMenuButton(
            new Rect(x + 42f, y + 322f + walletOffset, actionWidth, 40f),
            "CREATE ROOM",
            buttonStyle))
            CreateOnlineRoom();
        GUI.enabled = !onlineRequestInProgress;
        if (DrawMenuButton(
            new Rect(x + 58f + actionWidth, y + 322f + walletOffset,
                actionWidth, 40f),
            "REFRESH ROOMS",
            buttonStyle))
            StartCoroutine(RefreshOnlineRooms());
        GUI.enabled = previousControlsEnabled;

        Rect listRect = new Rect(
            x + 42f,
            y + 374f + walletOffset,
            width - 84f,
            205f
        );
        float contentHeight = Mathf.Max(205f, onlineRooms.Count * 48f);
        roomListScroll = GUI.BeginScrollView(
            listRect,
            roomListScroll,
            new Rect(0f, 0f, listRect.width - 18f, contentHeight)
        );
        for (int i = 0; i < onlineRooms.Count; i++)
        {
            OnlineRoomInfo room = onlineRooms[i];
            Rect row = new Rect(
                0f,
                i * 48f,
                listRect.width - 18f,
                42f
            );
            string label = room.name + "   " +
                room.player_count + "/" + room.max_players;
            GUI.enabled = !onlineRequestInProgress &&
                room.player_count < room.max_players;
            if (DrawMenuButton(row, label, buttonStyle))
                JoinOnlineRoom(room);
        }
        GUI.enabled = previousControlsEnabled;
        GUI.EndScrollView();
        if (onlineRooms.Count == 0)
        {
            GUI.Label(
                new Rect(x + 52f, y + 438f + walletOffset,
                    width - 104f, 24f),
                "No online rooms are available.",
                statsStyle
            );
        }

        if (DrawMenuButton(
            new Rect(x + 42f, y + 590f + walletOffset,
                width - 84f, 36f),
            "OPEN TCOIN ECONOMY",
            buttonStyle))
        {
            showEconomyMenu = true;
            StartCoroutine(CheckConfiguredContracts());
        }
        if (DrawMenuButton(
            new Rect(x + 42f, y + 634f + walletOffset,
                actionWidth, 36f),
            "SIGN OUT",
            buttonStyle))
        {
            onlineAuthToken = "";
            onlinePasswordInput = "";
            onlineWalletAddress = "";
            onlineWalletAddressInput = "";
            onlineWalletSignatureInput = "";
            onlineWalletChallengeMessage = "";
            onlineWalletChallengeNonce = "";
            onlineProfile = null;
            onlineRooms.Clear();
            onlineStatusMessage = "Signed out.";
        }
        if (DrawMenuButton(
            new Rect(x + 58f + actionWidth, y + 634f + walletOffset,
                actionWidth, 36f),
            "BACK",
            buttonStyle))
        {
            showOnlineMenu = false;
            onlineStatusMessage = "";
        }
    }

    private void DrawWalletEconomyMenu(float x, float y, float width)
    {
        bool previousEnabled = GUI.enabled;
        GUI.Label(
            new Rect(x + 32f, y + 76f, width - 64f, 42f),
            "Ethereum Sepolia | " + connectedWalletAddress,
            statsStyle
        );
        Rect viewRect = new Rect(
            x + 28f,
            y + 122f,
            width - 56f,
            620f
        );
        Rect contentRect = new Rect(
            0f,
            0f,
            width - 90f,
            1080f
        );
        walletEconomyScroll = GUI.BeginScrollView(
            viewRect,
            walletEconomyScroll,
            contentRect
        );

        bool canTransact = walletContractsReady &&
            !onlineWalletActionInProgress;
        if (DrawMenuButton(
            new Rect(0f, 0f, 220f, 34f),
            WalletConnectButtonLabel(),
            buttonStyle))
            ConnectOnlineWallet();
        if (DrawMenuButton(
            new Rect(232f, 0f, 220f, 34f),
            WalletSearchButtonLabel(),
            buttonStyle))
            OpenMetaMaskWalletSearch();
        if (!string.IsNullOrEmpty(connectedWalletAddress) &&
            DrawMenuButton(
            new Rect(464f, 0f, 220f, 34f),
            "DISCONNECT WALLET",
            buttonStyle))
            DisconnectWallet();
        GUI.Label(
            new Rect(0f, 40f, contentRect.width, 44f),
            "Balance: " + tcoinBalanceLabel + "\n" + walletEconomyStatus,
            statsStyle
        );
        GUI.Label(
            new Rect(0f, 88f, contentRect.width, 38f),
            "Wallet controls stay disabled until both configured contracts have bytecode on Ethereum Sepolia and this wallet is linked to your signed-in account.",
            statsStyle
        );

        GUI.Label(new Rect(0f, 132f, 170f, 24f), "BUY TCOIN WITH ETH", statsStyle);
        ethPurchaseAmountInput = GUI.TextField(
            new Rect(180f, 128f, 180f, 34f),
            ethPurchaseAmountInput,
            menuInputStyle
        );
        GUI.enabled = canTransact;
        if (DrawMenuButton(
            new Rect(372f, 128f, 190f, 34f),
            "PURCHASE TCOIN",
            buttonStyle))
            PurchaseTcoin();
        GUI.enabled = true;

        GUI.Label(new Rect(0f, 178f, 170f, 24f), "TRANSFER TCOIN", statsStyle);
        tcoinTransferAddressInput = GUI.TextField(
            new Rect(180f, 174f, 300f, 34f),
            tcoinTransferAddressInput,
            menuInputStyle
        );
        tcoinTransferAmountInput = GUI.TextField(
            new Rect(480f, 174f, 110f, 34f),
            tcoinTransferAmountInput,
            menuInputStyle
        );
        GUI.enabled = canTransact;
        if (DrawMenuButton(
            new Rect(600f, 174f, 110f, 34f),
            "TRANSFER",
            buttonStyle))
            TransferTcoin();
        GUI.enabled = true;

        GUI.Label(
            new Rect(0f, 220f, contentRect.width, 26f),
            "EQUIPMENT INVENTORY (IDs 1-5) — " +
                FormatInventorySummary(),
            statsStyle
        );
        GUI.Label(new Rect(0f, 250f, 160f, 24f), "ITEM ID / AMOUNT", statsStyle);
        equipmentIdInput = GUI.TextField(
            new Rect(170f, 246f, 100f, 34f),
            equipmentIdInput,
            menuInputStyle
        );
        equipmentAmountInput = GUI.TextField(
            new Rect(280f, 246f, 100f, 34f),
            equipmentAmountInput,
            menuInputStyle
        );
        GUI.Label(
            new Rect(0f, 292f, 210f, 24f),
            "APPROVAL AMOUNT (TCOIN)",
            statsStyle
        );
        equipmentApprovalAmountInput = GUI.TextField(
            new Rect(220f, 288f, 160f, 34f),
            equipmentApprovalAmountInput,
            menuInputStyle
        );
        GUI.enabled = canTransact;
        if (DrawMenuButton(
            new Rect(390f, 288f, 210f, 34f),
            "APPROVE SPEND LIMIT",
            buttonStyle))
            ApproveEquipmentSpend();
        if (DrawMenuButton(
            new Rect(0f, 332f, 180f, 34f),
            "BUY EQUIPMENT",
            buttonStyle))
            BuyEquipment();
        if (DrawMenuButton(
            new Rect(192f, 332f, 180f, 34f),
            "UPGRADE ITEM",
            buttonStyle))
            UpgradeEquipment();

        GUI.Label(new Rect(380f, 336f, 110f, 24f), "EQUIP SLOT", statsStyle);
        equipmentSlotInput = GUI.TextField(
            new Rect(470f, 332f, 60f, 34f),
            equipmentSlotInput,
            menuInputStyle
        );
        if (DrawMenuButton(
            new Rect(540f, 332f, 100f, 34f),
            "EQUIP",
            buttonStyle))
            EquipEquipment();
        if (DrawMenuButton(
            new Rect(650f, 332f, 100f, 34f),
            "UNEQUIP",
            buttonStyle))
            UnequipEquipment();

        GUI.Label(new Rect(0f, 382f, 190f, 24f), "LIST ITEM", statsStyle);
        marketplacePriceInput = GUI.TextField(
            new Rect(190f, 378f, 170f, 34f),
            marketplacePriceInput,
            menuInputStyle
        );
        if (DrawMenuButton(
            new Rect(372f, 378f, 190f, 34f),
            "CREATE LISTING",
            buttonStyle))
            ListEquipment();
        GUI.Label(
            new Rect(0f, 420f, contentRect.width, 40f),
            "Listing IDs are assigned on-chain in order. Share the listing ID and TCOIN price with buyers.",
            statsStyle
        );

        GUI.Label(new Rect(0f, 468f, 160f, 24f), "BUY / CANCEL LISTING", statsStyle);
        marketplaceListingIdInput = GUI.TextField(
            new Rect(170f, 464f, 100f, 34f),
            marketplaceListingIdInput,
            menuInputStyle
        );
        marketplaceBuyAmountInput = GUI.TextField(
            new Rect(280f, 464f, 100f, 34f),
            marketplaceBuyAmountInput,
            menuInputStyle
        );
        if (DrawMenuButton(
            new Rect(390f, 464f, 190f, 34f),
            "BUY LISTING",
            buttonStyle))
            BuyListedEquipment();
        if (DrawMenuButton(
            new Rect(570f, 464f, 170f, 34f),
            "CANCEL MY LISTING",
            buttonStyle))
            CancelEquipmentListing();
        GUI.enabled = true;

        GUI.Label(
            new Rect(0f, 520f, contentRect.width, 48f),
            "Marketplace purchases require an approved TCOIN allowance. Set an exact approval above before buying equipment or listings. All transactions require wallet confirmation and Ethereum Sepolia ETH for gas.",
            statsStyle
        );
        GUI.EndScrollView();
        GUI.enabled = previousEnabled;

        if (DrawMenuButton(
            new Rect(x + 32f, y + 756f, width - 64f, 40f),
            "BACK TO ONLINE LOBBY",
            buttonStyle))
        {
            showEconomyMenu = false;
        }
    }

    private string FormatInventorySummary()
    {
        string[] labels = { "Weapon", "Armor", "Engine", "Heavy", "Reactive" };
        string summary = "";
        for (int i = 0; i < equipmentInventory.Length; i++)
        {
            if (i > 0)
                summary += "   ";
            summary += labels[i] + " " +
                equipmentInventory[i].ToString() +
                (equipmentLevels[i] > 0
                    ? " (Lv " + equipmentLevels[i] + ")"
                    : "");
        }
        return summary;
    }

    // --------------------------------------------------
    // PAUSE MENU
    // --------------------------------------------------

    private void DrawPauseMenu()
    {
        bool showPauseActions = !showSettings && !showControlSettings;
        float layoutHeight = showPauseActions ? 560f : 440f;
        Rect panel = GetMenuPanelRect(420f, layoutHeight);
        float width = panel.width;
        float height = panel.height;
        float actionScale = height / 560f;

        float x = panel.x;
        float y = panel.y;

        DrawMenuPanel(
            panel,
            showPauseActions
                ? "PAUSED"
                : showControlSettings
                    ? "CONTROL SETTINGS"
                    : "SETTINGS"
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

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 95f * actionScale,
                width - 130f * actionScale,
                66f * actionScale
            ),
            "RESUME GAME",
            pauseMenuButtonStyle))
        {
            ResumeGame();
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 183f * actionScale,
                width - 130f * actionScale,
                66f * actionScale
            ),
            "PLAY AGAIN",
            pauseMenuButtonStyle))
        {
            PlayAgain();
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 271f * actionScale,
                width - 130f * actionScale,
                66f * actionScale
            ),
            "SETTINGS",
            pauseMenuButtonStyle))
        {
            showSettings = true;
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 359f * actionScale,
                width - 130f * actionScale,
                66f * actionScale
            ),
            "MAIN MENU",
            pauseMenuButtonStyle))
        {
            ReturnToMainMenu();
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 447f * actionScale,
                width - 130f * actionScale,
                66f * actionScale
            ),
            "EXIT",
            pauseMenuButtonStyle))
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

        selectedResolution = Mathf.Clamp(
            selectedResolution, 0, resolutions.Length - 1
        );

        string[] resolutionOptions =
        {
            "1280 x 720",
            "1920 x 1080",
            "2560 x 1440"
        };
        for (int i = 0; i < resolutionOptions.Length; i++)
        {
            if (DrawMenuButton(
                new Rect(x + 30f, y + 110f + i * 44f, width - 60f, 40f),
                resolutionOptions[i],
                i == selectedResolution
                    ? selectedButtonStyle
                    : buttonStyle))
            {
                selectedResolution = i;
            }
        }

        isFullscreen = GUI.Toggle(
            new Rect(x + 30, y + 250, width - 60, 30),
            isFullscreen,
            " Fullscreen",
            menuToggleStyle
        );

        if (DrawMenuButton(
            new Rect(x + 30, y + 286, width - 60, 35),
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

        if (DrawMenuButton(
            new Rect(x + 30, y + 332, width - 60, 35),
            "CONTROL SETTINGS",
            buttonStyle))
        {
            showControlSettings = true;
        }

        if (DrawMenuButton(
            new Rect(x + 30, y + 378, width - 60, 35),
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

        if (DrawMenuButton(
            new Rect(x + 30, y + 130, width - 60, 45),
            !MouseTurretControlEnabled
                ? "✓ Keyboard Aiming (J/L, I/K)"
                : "Keyboard Aiming (J/L, I/K)",
            !MouseTurretControlEnabled
                ? selectedButtonStyle
                : buttonStyle))
        {
            MouseTurretControlEnabled = false;
        }

        if (DrawMenuButton(
            new Rect(x + 30, y + 185, width - 60, 45),
            MouseTurretControlEnabled
                ? "✓ Mouse Aiming"
                : "Mouse Aiming",
            MouseTurretControlEnabled
                ? selectedButtonStyle
                : buttonStyle))
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

        if (DrawMenuButton(
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
        float scale = Mathf.Max(
            0.25f,
            Mathf.Min(Screen.width / 1920f, Screen.height / 1080f)
        );
        Matrix4x4 previousMatrix = GUI.matrix;
        try
        {
            GUI.matrix = previousMatrix *
                Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            DrawBattleHUDLayout(
                Screen.width / scale,
                Screen.height / scale
            );
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawBattleHUDLayout(
        float screenWidth,
        float screenHeight)
    {
        float margin = 18f;
        float panelWidth = Mathf.Min(390f, screenWidth - margin * 2f);
        float x = screenWidth - panelWidth - margin;
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
        Rect destroyedCard = new Rect(
            totalCard.xMax + 8f, totalCard.y, countWidth, 48f);
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

        DrawRadialMeter(
            new Vector2(leftCenterX, meterY), 31f, missileProgress,
            new Color(0.96f, 0.30f, 0.25f, 1f), "MISSILE", missileValue);
        DrawRadialMeter(
            new Vector2(rightCenterX, meterY), 31f, shellProgress,
            new Color(0.12f, 0.75f, 0.96f, 1f), "SHELL", shellValue);

        float speed = playerMovement != null
            ? Mathf.Abs(playerMovement.currentSpeed)
            : 0f;
        float maxSpeed = playerMovement != null
            ? Mathf.Max(0.1f, playerMovement.maxSpeed)
            : 1f;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        int speedKph = Mathf.RoundToInt(speed * 3.6f);

        Rect speedCard = new Rect(
            x + inset, y + 177f + layoutOffset,
            panelWidth - inset * 2f, 49f);
        GUI.Box(speedCard, "", hudBarBackgroundStyle);
        GUI.Label(new Rect(speedCard.x + 8f, speedCard.y + 3f, speedCard.width - 16f, 17f), "TANK SPEED", statsStyle);
        GUI.Label(new Rect(speedCard.x + speedCard.width - 88f, speedCard.y + 2f, 80f, 19f), speedKph + " km/h", hudValueStyle);

        Rect speedBar = new Rect(
            speedCard.x + 8f, speedCard.y + 27f,
            speedCard.width - 16f, 13f);
        GUI.Box(speedBar, "", hudBarBackgroundStyle);
        GUI.Box(
            new Rect(speedBar.x + 2f, speedBar.y + 2f,
                (speedBar.width - 4f) * speedRatio,
                speedBar.height - 4f),
            "",
            hudFillBlueStyle);

        float health = playerHealth != null && playerHealth.maxHealth > 0f
            ? Mathf.Clamp01(
                playerHealth.CurrentHealth / playerHealth.maxHealth)
            : 0f;
        float healthWidth = Mathf.Min(500f, screenWidth - margin * 2f);
        float healthX = (screenWidth - healthWidth) * 0.5f;
        float healthY = screenHeight - 62f;
        Rect healthCard = new Rect(healthX, healthY, healthWidth, 44f);
        GUI.Box(healthCard, "", hudPanelStyle);

        string healthValue = playerHealth != null
            ? Mathf.CeilToInt(playerHealth.CurrentHealth) + " / " +
              Mathf.CeilToInt(playerHealth.maxHealth)
            : "N/A";
        GUI.Label(
            new Rect(healthCard.x + 12f, healthCard.y + 3f, 180f, 16f),
            "PLAYER HEALTH",
            statsStyle);
        GUI.Label(
            new Rect(healthCard.x + healthCard.width - 108f,
                healthCard.y + 2f, 96f, 18f),
            healthValue,
            hudValueStyle);

        Rect healthBar = new Rect(
            healthCard.x + 12f, healthCard.y + 23f,
            healthCard.width - 24f, 12f);
        GUI.Box(healthBar, "", hudBarBackgroundStyle);
        GUI.Box(
            new Rect(healthBar.x + 2f, healthBar.y + 2f,
                (healthBar.width - 4f) * health,
                healthBar.height - 4f),
            "",
            hudBarFillStyle);
    }

    private void GetMultiplayerTeamTankCounts(
        out int blueTanksRemaining,
        out int redTanksRemaining)
    {
        blueTanksRemaining = 0;
        redTanksRemaining = 0;

        if (!NetworkServer.active)
            return;

        foreach (NetworkIdentity networkIdentity in
                 NetworkServer.spawned.Values)
        {
            NetworkTankPlayer player =
                networkIdentity.GetComponent<NetworkTankPlayer>();
            TankHealth health =
                networkIdentity.GetComponent<TankHealth>();
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
        generatedGuiTextures.Add(texture);
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
        float layoutWidth = 380f;
        float layoutHeight = 320f;
        Rect panel = GetMenuPanelRect(layoutWidth, layoutHeight);
        float width = panel.width;
        float height = panel.height;
        float x = panel.x;
        float y = panel.y;

        DrawMenuPanel(panel, victory ? "VICTORY" : "DEFEAT");

        float scaleX = width / layoutWidth;
        float scaleY = height / layoutHeight;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(
            new Vector3(x * (1f - scaleX), y * (1f - scaleY), 0f),
            Quaternion.identity,
            new Vector3(scaleX, scaleY, 1f)
        );

        string destroyedCount = multiplayer
            ? LocalPlayerEnemyTanksDestroyed.ToString()
            : destroyedTanks + " / " + totalAITanks;
        GUI.Label(
            new Rect(x + 20, y + 85, layoutWidth - 40, 30),
            multiplayer
                ? "TANKS DESTROYED: " + destroyedCount
                : "Destroyed AI Tanks: " + destroyedCount,
            statsStyle
        );

        if (DrawMenuButton(
            new Rect(x + 50, y + 135, layoutWidth - 100, 40),
            "PLAY AGAIN",
            buttonStyle))
        {
            PlayAgain();
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 190, layoutWidth - 100, 40),
            "MAIN MENU",
            buttonStyle))
        {
            ReturnToMainMenu();
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 245, layoutWidth - 100, 35),
            "EXIT",
            buttonStyle))
            QuitGame();

        GUI.matrix = previousMatrix;
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
        statsStyle.normal.textColor = new Color(0.88f, 0.9f, 0.91f, 1f);
        statsStyle.alignment = TextAnchor.MiddleLeft;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleLeft;
        titleStyle.normal.textColor = Color.white;

        menuPanelStyle = new GUIStyle(GUI.skin.box);
        menuPanelStyle.normal.background = CreateMenuSurfaceTexture(
            96,
            96,
            new Color(0.15f, 0.17f, 0.18f, 0.78f),
            new Color(0.07f, 0.08f, 0.09f, 0.86f),
            new Color(0.52f, 0.55f, 0.56f, 0.48f),
            12,
            2
        );
        menuPanelStyle.border = new RectOffset(14, 14, 14, 14);
        menuPanelStyle.padding = new RectOffset(0, 0, 0, 0);

        menuTitleStyle = new GUIStyle(GUI.skin.label);
        menuTitleStyle.fontSize = 27;
        menuTitleStyle.fontStyle = FontStyle.Bold;
        menuTitleStyle.alignment = TextAnchor.MiddleLeft;
        menuTitleStyle.normal.textColor = Color.white;
        menuTitleStyle.clipping = TextClipping.Clip;

        menuSubtitleStyle = new GUIStyle(GUI.skin.label);
        menuSubtitleStyle.fontSize = 13;
        menuSubtitleStyle.fontStyle = FontStyle.Bold;
        menuSubtitleStyle.alignment = TextAnchor.MiddleLeft;
        menuSubtitleStyle.normal.textColor = new Color(0.66f, 0.79f, 0.91f);

        menuCardTitleStyle = new GUIStyle(GUI.skin.label);
        menuCardTitleStyle.fontSize = 19;
        menuCardTitleStyle.fontStyle = FontStyle.Bold;
        menuCardTitleStyle.alignment = TextAnchor.MiddleLeft;
        menuCardTitleStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 17;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        buttonStyle.padding = new RectOffset(12, 12, 8, 8);
        buttonStyle.border = new RectOffset(10, 10, 10, 10);
        buttonStyle.normal.background = CreateMenuSurfaceTexture(
            96,
            48,
            new Color(0.66f, 0.68f, 0.69f, 0.94f),
            new Color(0.39f, 0.41f, 0.42f, 0.94f),
            new Color(0.17f, 0.18f, 0.19f, 0.9f),
            7,
            2
        );
        buttonStyle.hover.background = CreateMenuSurfaceTexture(
            96,
            48,
            new Color(0.78f, 0.80f, 0.81f, 0.97f),
            new Color(0.49f, 0.51f, 0.52f, 0.97f),
            new Color(0.86f, 0.88f, 0.89f, 0.95f),
            7,
            2
        );
        buttonStyle.active.background = CreateMenuSurfaceTexture(
            96,
            48,
            new Color(0.37f, 0.39f, 0.4f, 0.98f),
            new Color(0.26f, 0.28f, 0.29f, 0.98f),
            new Color(0.83f, 0.85f, 0.86f, 0.9f),
            7,
            2
        );
        buttonStyle.focused.background = buttonStyle.hover.background;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        buttonStyle.focused.textColor = Color.white;

        pauseMenuButtonStyle = new GUIStyle(buttonStyle);
        pauseMenuButtonStyle.fontSize = 23;

        selectedButtonStyle = new GUIStyle(buttonStyle);
        selectedButtonStyle.normal.background = buttonStyle.hover.background;
        selectedButtonStyle.normal.textColor = Color.white;

        menuInputStyle = new GUIStyle(GUI.skin.textField);
        menuInputStyle.fontSize = 17;
        menuInputStyle.alignment = TextAnchor.MiddleLeft;
        menuInputStyle.padding = new RectOffset(12, 12, 5, 5);
        menuInputStyle.normal.background = CreateMenuSurfaceTexture(
            64,
            48,
            new Color(0.18f, 0.24f, 0.30f, 0.98f),
            new Color(0.11f, 0.15f, 0.19f, 0.98f),
            new Color(0.57f, 0.69f, 0.77f, 0.94f),
            8,
            2
        );
        menuInputStyle.hover.background = menuInputStyle.normal.background;
        menuInputStyle.focused.background = CreateMenuSurfaceTexture(
            64,
            48,
            new Color(0.22f, 0.30f, 0.37f, 1f),
            new Color(0.13f, 0.19f, 0.24f, 1f),
            new Color(0.82f, 0.90f, 0.95f, 1f),
            8,
            2
        );
        menuInputStyle.active.background = menuInputStyle.focused.background;
        menuInputStyle.normal.textColor = Color.white;
        menuInputStyle.focused.textColor = Color.white;
        menuInputStyle.hover.textColor = Color.white;
        menuInputStyle.active.textColor = Color.white;

        menuToggleStyle = new GUIStyle(GUI.skin.toggle);
        menuToggleStyle.fontSize = 16;
        menuToggleStyle.normal.textColor = Color.white;
        menuToggleStyle.hover.textColor = Color.white;
        menuToggleStyle.onNormal.textColor = Color.white;
        menuToggleStyle.onHover.textColor = Color.white;

        hudPanelStyle = new GUIStyle(GUI.skin.box);
        hudPanelStyle.normal.background = CreateSolidTexture(
            new Color(0.035f, 0.055f, 0.075f, 0.88f)
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
            new Color(0.12f, 0.16f, 0.19f, 0.95f)
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
        generatedGuiTextures.Add(texture);
        return texture;
    }

    private Texture2D CreateMenuSurfaceTexture(
        int width,
        int height,
        Color topColor,
        Color bottomColor,
        Color borderColor,
        int cornerCut,
        int borderWidth)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int left = x;
                int right = width - 1 - x;
                int bottom = y;
                int top = height - 1 - y;
                bool outsideCorner =
                    (left < cornerCut && bottom < cornerCut &&
                        left + bottom < cornerCut - 1) ||
                    (right < cornerCut && bottom < cornerCut &&
                        right + bottom < cornerCut - 1) ||
                    (left < cornerCut && top < cornerCut &&
                        left + top < cornerCut - 1) ||
                    (right < cornerCut && top < cornerCut &&
                        right + top < cornerCut - 1);
                int pixelIndex = y * width + x;
                if (outsideCorner)
                {
                    pixels[pixelIndex] = Color.clear;
                    continue;
                }

                bool isBorder = left < borderWidth ||
                    right < borderWidth ||
                    bottom < borderWidth ||
                    top < borderWidth;
                if (isBorder)
                {
                    bool highlightEdge = top < borderWidth ||
                        left < borderWidth;
                    pixels[pixelIndex] = highlightEdge
                        ? borderColor
                        : Color.Lerp(borderColor, Color.black, 0.34f);
                    continue;
                }

                float verticalBlend = y / (float)(height - 1);
                pixels[pixelIndex] = Color.Lerp(
                    bottomColor,
                    topColor,
                    verticalBlend
                );
            }
        }

        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        );
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        generatedGuiTextures.Add(texture);
        return texture;
    }

    private void OnDestroy()
    {
        foreach (Texture2D texture in generatedGuiTextures)
        {
            if (texture != null)
                Destroy(texture);
        }
        generatedGuiTextures.Clear();

        ShutdownServers();

        if (Instance == this)
            Instance = null;

        Time.timeScale = 1f;
    }}
