using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using Mirror;

public partial class GameDirector
{
    private void InitializeSceneMenuUI()
    {
        InitializeMenuButtonSprites();

        if (gamePauseCanvas == null)
            gamePauseCanvas = FindSceneCanvas("GamePauseCanvas");
        if (victoryMenuCanvas == null)
            victoryMenuCanvas = FindSceneCanvas("VictoryMenu");
        if (defeatMenuCanvas == null)
            defeatMenuCanvas = FindSceneCanvas("DefeatMenu");

        if (leopardBattleMenuCanvas == null ||
            multiplayerMenuCanvas == null ||
            settingsMenuCanvas == null ||
            joinRoomMenuCanvas == null)
        {
            Debug.LogError(
                "GameDirector: Assign the LeopardBattleMenu, MultiPlayMenu, " +
                "SettingMenu, and JoinRoomMenu canvases in the scene.",
                this
            );
            return;
        }

        ConfigureSceneMenuCanvases();
        BindMenuButton(gamePauseCanvas, "ResumeBtn", ResumeGame);
        BindMenuButton(gamePauseCanvas, "SettingsBtn", OpenSettingsMenu);
        BindMenuButton(gamePauseCanvas, "ExitBtn", QuitGame);
        BindMenuButton(gamePauseCanvas, "MainMenuBtn", ReturnToMainMenu);
        BindMenuButton(victoryMenuCanvas, "PlayAgainBtn", PlayAgain);
        BindMenuButton(victoryMenuCanvas, "MainMenuBtn", ReturnToMainMenu);
        BindMenuButton(victoryMenuCanvas, "ExitBtn", QuitGame);
        BindMenuButton(defeatMenuCanvas, "PlayAgainBtn", PlayAgain);
        BindMenuButton(defeatMenuCanvas, "MainMenuBtn", ReturnToMainMenu);
        BindMenuButton(defeatMenuCanvas, "ExitBtn", QuitGame);

        if (victoryMenuCanvas != null)
        {
            victoryDestroyedCountLabel = CreateSceneText(
                victoryMenuCanvas.transform,
                "DestroyedEnemyTanksCount",
                new Vector2(0f, 60f),
                new Vector2(760f, 40f),
                24
            );
            victoryDestroyedCountLabel.fontStyle = FontStyle.Bold;
        }
        if (defeatMenuCanvas != null)
        {
            defeatDestroyedCountLabel = CreateSceneText(
                defeatMenuCanvas.transform,
                "DestroyedEnemyTanksCount",
                new Vector2(0f, 60f),
                new Vector2(760f, 40f),
                24
            );
            defeatDestroyedCountLabel.fontStyle = FontStyle.Bold;
        }

        BindMenuButton(
            leopardBattleMenuCanvas,
            "SinglePlay",
            () => StartGame(GameMode.SinglePlay)
        );
        BindMenuButton(
            leopardBattleMenuCanvas,
            "MultiPlayer",
            OpenOnlineMenu,
            "ONLINE BATTLE"
        );
        BindMenuButton(
            leopardBattleMenuCanvas,
            "Settings",
            OpenSettingsMenu
        );
        BindMenuButton(
            leopardBattleMenuCanvas,
            "Exit",
            QuitGame
        );

        BindMenuButton(
            settingsMenuCanvas,
            "1280",
            () => SelectResolution(0)
        );
        BindMenuButton(
            settingsMenuCanvas,
            "1920",
            () => SelectResolution(1)
        );
        BindMenuButton(
            settingsMenuCanvas,
            "2560",
            () => SelectResolution(2)
        );
        BindMenuButton(
            settingsMenuCanvas,
            "FullScreenBtn",
            ToggleFullscreen
        );
        BindMenuButton(
            settingsMenuCanvas,
            "ApplyBtn",
            ApplyDisplaySettings
        );
        BindMenuButton(
            settingsMenuCanvas,
            "ControlSettingBtn",
            ToggleTurretControl
        );
        BindMenuButton(
            settingsMenuCanvas,
            "BackBtn",
            CloseSettingsMenu
        );
        settingsStatusLabel = CreateSceneText(
            settingsMenuCanvas.transform,
            "SettingsStatus",
            new Vector2(0f, -282f),
            new Vector2(720f, 26f),
            16
        );

        if (menuButtonCanvas != null)
            EnsureMenuButtonCanvas();
    }

    private Canvas FindSceneCanvas(string objectName)
    {
        GameObject canvasObject = GameObject.Find(objectName);
        if (canvasObject == null)
        {
            Debug.LogError(
                "GameDirector: Could not find scene canvas " + objectName +
                ".",
                this
            );
            return null;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError(
                "GameDirector: " + objectName +
                " does not have a Canvas component.",
                canvasObject
            );
        }
        return canvas;
    }

    private void InitializeMenuButtonSprites()
    {
        menuButtonSpriteSets.Clear();
        if (menuButtonSprites == null)
            return;

        foreach (Sprite sprite in menuButtonSprites)
        {
            if (sprite == null)
                continue;

            int suffixStart = sprite.name.LastIndexOf('_');
            if (suffixStart < 0 || suffixStart == sprite.name.Length - 1)
                continue;

            string groupName = sprite.name.Substring(0, suffixStart);
            string state = sprite.name.Substring(suffixStart + 1);
            if (groupName.EndsWith(
                "btn",
                StringComparison.OrdinalIgnoreCase))
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

            if (state.Equals("normal", StringComparison.OrdinalIgnoreCase))
                spriteSet.normal = sprite;
            else if (state.Equals("hover", StringComparison.OrdinalIgnoreCase) ||
                     state.Equals("select", StringComparison.OrdinalIgnoreCase))
                spriteSet.hover = sprite;
            else if (state.Equals("press", StringComparison.OrdinalIgnoreCase))
                spriteSet.pressed = sprite;
        }

    }

    private void BindMenuButton(
        Canvas canvas,
        string objectName,
        Action action,
        string label = null)
    {
        if (canvas == null)
        {
            Debug.LogError(
                "GameDirector: Cannot bind " + objectName +
                " because its canvas is missing.",
                this
            );
            return;
        }

        Button button = FindMenuButton(canvas, objectName);
        if (button == null)
        {
            Debug.LogError(
                "GameDirector: Could not find button " + objectName +
                " under " + canvas.name + ".",
                canvas
            );
            return;
        }

        if (!string.IsNullOrEmpty(label))
        {
            Text buttonLabel = button.GetComponentInChildren<Text>(true);
            if (buttonLabel != null)
                buttonLabel.text = label;
        }

        button.onClick.AddListener(() => action());
    }

    private Button FindMenuButton(Canvas canvas, string objectName)
    {
        Transform buttonTransform = canvas.transform.Find(objectName);
        if (buttonTransform == null)
        {
            Debug.LogError(
                "GameDirector: Could not find " + objectName +
                " under " + canvas.name + ".",
                canvas
            );
            return null;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError(
                "GameDirector: " + objectName +
                " does not have a Button component.",
                buttonTransform
            );
        }
        return button;
    }

    private void ConfigureSceneMenuCanvases()
    {
        Canvas[] canvases =
        {
            leopardBattleMenuCanvas,
            multiplayerMenuCanvas,
            settingsMenuCanvas,
            joinRoomMenuCanvas,
            gamePauseCanvas,
            victoryMenuCanvas,
            defeatMenuCanvas
        };

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
                continue;

            RectTransform canvasTransform =
                canvas.GetComponent<RectTransform>();
            if (canvasTransform != null)
                canvasTransform.localScale = Vector3.one;

            GraphicRaycaster raycaster =
                canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            EnsureCanvasBackground(canvas);

            Button[] buttons =
                canvas.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
                ConfigureSceneMenuButton(button);
        }

    }

    private void EnsureCanvasBackground(Canvas canvas)
    {
        if (menuBackgroundTexture == null)
        {
            if (!menuBackgroundTextureWarningLogged)
            {
                Debug.LogError(
                    "GameDirector: Assign background.png to the menu " +
                    "background texture.",
                    this
                );
                menuBackgroundTextureWarningLogged = true;
            }
            return;
        }

        Transform backgroundTransform =
            canvas.transform.Find("BackgroundImage");
        GameObject backgroundObject = backgroundTransform != null
            ? backgroundTransform.gameObject
            : new GameObject(
                "BackgroundImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );

        if (backgroundTransform == null)
            backgroundObject.transform.SetParent(canvas.transform, false);

        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        RawImage background = backgroundObject.GetComponent<RawImage>();
        if (background == null)
            background = backgroundObject.AddComponent<RawImage>();
        background.texture = menuBackgroundTexture;
        background.color = Color.white;
        background.raycastTarget = false;
        backgroundTransform = backgroundObject.transform;
        if (backgroundTransform.GetSiblingIndex() != 0)
            backgroundTransform.SetAsFirstSibling();
    }

    private void ConfigureSceneMenuButton(Button button)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image == null)
            image = button.targetGraphic as Image;

        if (image == null)
        {
            Debug.LogError(
                "GameDirector: Button " + button.name +
                " needs an Image for its hover state.",
                button
            );
            return;
        }

        image.raycastTarget = true;
        button.targetGraphic = image;
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.68f, 0.9f, 1f, 1f);
        colors.pressedColor = new Color(0.48f, 0.76f, 0.94f, 1f);
        colors.selectedColor = new Color(0.82f, 0.94f, 1f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (button.name == "BlueTeamBtn" ||
            button.name == "RedTeamBtn")
        {
            button.transition = Selectable.Transition.ColorTint;
            return;
        }

        SpriteState spriteState = button.spriteState;
        if (spriteState.highlightedSprite == null)
        {
            string spriteGroup = button.name.EndsWith(
                "Btn",
                StringComparison.OrdinalIgnoreCase
            )
                ? button.name.Substring(0, button.name.Length - 3) + "btn"
                : button.name + "btn";
            spriteState.highlightedSprite =
                FindMenuSprite(spriteGroup + "_hover");
            spriteState.selectedSprite =
                spriteState.highlightedSprite;
        }

        if (spriteState.pressedSprite == null)
        {
            string spriteGroup = button.name.EndsWith(
                "Btn",
                StringComparison.OrdinalIgnoreCase
            )
                ? button.name.Substring(0, button.name.Length - 3) + "btn"
                : button.name + "btn";
            spriteState.pressedSprite =
                FindMenuSprite(spriteGroup + "_press");
        }

        button.spriteState = spriteState;
        if (spriteState.highlightedSprite != null)
        {
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = Color.white;
            button.colors = colors;
            button.transition = Selectable.Transition.SpriteSwap;
        }
        else
        {
            button.transition = Selectable.Transition.ColorTint;
        }
    }

    private Text CreateSceneText(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        int fontSize)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.GetComponent<Text>();

        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text)
        );
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private void UpdateSceneMenuCanvasVisibility()
    {
        SetCanvasEnabled(leopardBattleMenuCanvas, false);
        SetCanvasEnabled(multiplayerMenuCanvas, false);
        SetCanvasEnabled(settingsMenuCanvas, false);
        SetCanvasEnabled(joinRoomMenuCanvas, false);
        SetCanvasEnabled(gamePauseCanvas, false);
        SetCanvasEnabled(victoryMenuCanvas, false);
        SetCanvasEnabled(defeatMenuCanvas, false);
        SetCanvasEnabled(menuPanelCanvas, false);
        SetCanvasEnabled(menuButtonCanvas, false);
        HideLegacyMenuPanelGraphics();

        bool showGameplayStatusBars = !showMainMenu &&
            !showPauseMenu && !finishMenuVisible;

        if (!gameplayStatusVisibilityInitialized ||
            gameplayStatusBarsVisible != showGameplayStatusBars)
        {
            gameplayStatusVisibilityInitialized = true;
            gameplayStatusBarsVisible = showGameplayStatusBars;
            SetGameplayStatusBarsVisible(showGameplayStatusBars);
        }

    }

    private static void SetCanvasEnabled(Canvas canvas, bool enabled)
    {
        if (canvas == null)
            return;

        if (enabled)
        {
            if (!canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);
            if (!canvas.enabled)
                canvas.enabled = true;
            return;
        }

        if (canvas.enabled)
            canvas.enabled = false;
        if (canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(false);
    }

    private void SetGameplayStatusBarsVisible(bool visible)
    {
        TankHealthBar[] healthBars =
            FindObjectsOfType<TankHealthBar>(true);
        foreach (TankHealthBar healthBar in healthBars)
        {
            if (healthBar != null)
                healthBar.gameObject.SetActive(visible);
        }

        NetworkTankPlayer[] networkPlayers =
            FindObjectsOfType<NetworkTankPlayer>(true);
        foreach (NetworkTankPlayer networkPlayer in networkPlayers)
        {
            if (networkPlayer != null)
                networkPlayer.SetHealthBarVisible(visible);
        }

        if (playerTankFire != null)
            playerTankFire.SetStatusBarVisible(visible);
    }

    private void UpdateResultCountLabels()
    {
        bool isMultiplayer = IsMultiplayerMode;
        int destroyedCount = isMultiplayer
            ? LocalPlayerEnemyTanksDestroyed
            : destroyedTanks;
        int totalCount = totalAITanks;
        if (resultCountCacheInitialized &&
            cachedResultCountIsMultiplayer == isMultiplayer &&
            cachedResultDestroyedCount == destroyedCount &&
            cachedResultTotalCount == totalCount)
        {
            return;
        }

        string countLabel = isMultiplayer
            ? "Destroyed Enemy Tanks: " + destroyedCount
            : "Destroyed AI Tanks: " + destroyedCount +
                " / " + totalCount;

        if (victoryDestroyedCountLabel != null)
            victoryDestroyedCountLabel.text = countLabel;
        if (defeatDestroyedCountLabel != null)
            defeatDestroyedCountLabel.text = countLabel;

        resultCountCacheInitialized = true;
        cachedResultCountIsMultiplayer = isMultiplayer;
        cachedResultDestroyedCount = destroyedCount;
        cachedResultTotalCount = totalCount;
    }

    private void OpenSettingsMenu()
    {
        showSettings = true;
        showControlSettings = false;
    }

    private void OpenOnlineMenu()
    {
        showOnlineMenu = true;
        showEconomyMenu = false;
        onlineStatusMessage = "";
        selectedTeam = TankTeam.Unassigned;
        if (!string.IsNullOrEmpty(onlineAuthToken))
            StartCoroutine(RefreshOnlineProfile());
        if (!string.IsNullOrEmpty(onlineAuthToken))
            StartCoroutine(RefreshOnlineRooms());
    }

    private void SubmitOnlineAuthentication()
    {
        if (onlineRequestInProgress)
            return;

        string endpoint = onlineRegistrationMode
            ? "/api/auth/register"
            : "/api/auth/login";
        OnlineRequestData data = new OnlineRequestData
        {
            username = onlineUsernameInput.Trim(),
            password = onlinePasswordInput
        };
        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            endpoint,
            "POST",
            JsonUtility.ToJson(data),
            body =>
            {
                onlineRequestInProgress = false;
                OnlineAuthResponse result =
                    JsonUtility.FromJson<OnlineAuthResponse>(body);
                if (result == null || string.IsNullOrEmpty(result.token) ||
                    result.user == null)
                {
                    onlineStatusMessage =
                        "The server returned an invalid sign-in response.";
                    return;
                }

                onlineAuthToken = result.token;
                onlineUsernameInput = result.user.username;
                onlinePlayerNameInput = result.user.username;
                onlineWalletAddress = result.user.walletAddress ?? "";
                onlinePasswordInput = "";
                onlineStatusMessage = "Signed in as " + result.user.username;
                StartCoroutine(RefreshOnlineProfile());
                StartCoroutine(RefreshOnlineRooms());
                SyncConnectedWallet();
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private void RequestOnlineWalletChallenge()
    {
        if (onlineRequestInProgress || string.IsNullOrEmpty(onlineAuthToken))
            return;

        string address = onlineWalletAddressInput.Trim();
        if (string.IsNullOrEmpty(address))
        {
            onlineStatusMessage = "Enter your wallet address first.";
            return;
        }

        OnlineRequestData data = new OnlineRequestData
        {
            address = address
        };
        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            "/api/wallet/challenge",
            "POST",
            JsonUtility.ToJson(data),
            body =>
            {
                onlineRequestInProgress = false;
                OnlineWalletChallengeResponse result =
                    JsonUtility.FromJson<OnlineWalletChallengeResponse>(body);
                if (result == null ||
                    string.IsNullOrWhiteSpace(result.nonce) ||
                    string.IsNullOrWhiteSpace(result.message))
                {
                    onlineStatusMessage =
                        "The server returned an invalid wallet challenge.";
                    return;
                }

                onlineWalletChallengeNonce = result.nonce;
                onlineWalletChallengeMessage = result.message;
                GUIUtility.systemCopyBuffer = result.message;
                onlineStatusMessage =
                    "Challenge copied. Sign it with your wallet, then paste the signature.";
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private void CopyOnlineWalletChallenge()
    {
        if (string.IsNullOrEmpty(onlineWalletChallengeMessage))
        {
            onlineStatusMessage = "Request a wallet challenge first.";
            return;
        }

        GUIUtility.systemCopyBuffer = onlineWalletChallengeMessage;
        onlineStatusMessage = "Wallet challenge copied to clipboard.";
    }

    private void VerifyOnlineWallet()
    {
        if (onlineRequestInProgress || string.IsNullOrEmpty(onlineAuthToken))
            return;
        if (string.IsNullOrEmpty(onlineWalletChallengeNonce) ||
            string.IsNullOrWhiteSpace(onlineWalletSignatureInput))
        {
            onlineStatusMessage =
                "Request a challenge and paste your wallet signature.";
            return;
        }

        OnlineRequestData data = new OnlineRequestData
        {
            address = onlineWalletAddressInput.Trim(),
            nonce = onlineWalletChallengeNonce,
            signature = onlineWalletSignatureInput.Trim()
        };
        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            "/api/wallet/verify",
            "POST",
            JsonUtility.ToJson(data),
            body =>
            {
                onlineRequestInProgress = false;
                OnlineWalletResponse result =
                    JsonUtility.FromJson<OnlineWalletResponse>(body);
                if (result == null ||
                    string.IsNullOrWhiteSpace(result.walletAddress))
                {
                    onlineStatusMessage =
                        "The server returned an invalid wallet response.";
                    return;
                }

                onlineWalletAddress = result.walletAddress;
                onlineWalletAddressInput = "";
                onlineWalletSignatureInput = "";
                onlineWalletChallengeNonce = "";
                onlineWalletChallengeMessage = "";
                onlineStatusMessage = "Reward wallet linked.";
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private IEnumerator RefreshOnlineRooms()
    {
        if (string.IsNullOrEmpty(onlineAuthToken) ||
            onlineRequestInProgress)
            yield break;

        onlineRequestInProgress = true;
        yield return SendOnlineRequest(
            "/api/rooms",
            "GET",
            "",
            body =>
            {
                onlineRequestInProgress = false;
                OnlineRoomListResponse result =
                    JsonUtility.FromJson<OnlineRoomListResponse>(body);
                onlineRooms.Clear();
                if (result != null && result.rooms != null)
                    onlineRooms.AddRange(result.rooms);
                onlineStatusMessage = result == null
                    ? "The server returned an invalid room list."
                    : "Rooms refreshed.";
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        );
    }

    private IEnumerator RefreshOnlineProfile()
    {
        if (string.IsNullOrEmpty(onlineAuthToken) ||
            onlineProfileRequestInProgress)
            yield break;

        string requestToken = onlineAuthToken;
        onlineProfileRequestInProgress = true;
        yield return SendOnlineRequest(
            "/api/profile",
            "GET",
            "",
            body =>
            {
                onlineProfileRequestInProgress = false;
                if (onlineAuthToken != requestToken)
                    return;
                OnlineProfileResponse result =
                    JsonUtility.FromJson<OnlineProfileResponse>(body);
                if (result == null ||
                    string.IsNullOrWhiteSpace(result.playerId))
                {
                    onlineStatusMessage =
                        "The server returned an invalid player profile.";
                    return;
                }
                onlineProfile = result;
                onlineWalletAddress = result.walletAddress ?? "";
            },
            error =>
            {
                onlineProfileRequestInProgress = false;
                onlineStatusMessage =
                    "Could not load player profile: " + error;
            }
        );
    }

    private void CreateOnlineRoom()
    {
        if (onlineRequestInProgress)
            return;
        if (selectedTeam == TankTeam.Unassigned)
        {
            onlineStatusMessage = "Select a team before creating a room.";
            return;
        }

        OnlineRequestData data = new OnlineRequestData
        {
            name = string.IsNullOrWhiteSpace(onlineRoomNameInput)
                ? "Abrams Room"
                : onlineRoomNameInput.Trim(),
            team = selectedTeam == TankTeam.Blue ? "blue" : "red",
            playerName = string.IsNullOrWhiteSpace(onlinePlayerNameInput)
                ? onlineUsernameInput
                : onlinePlayerNameInput.Trim()
        };
        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            "/api/rooms",
            "POST",
            JsonUtility.ToJson(data),
            body =>
            {
                onlineRequestInProgress = false;
                OnlineRoomResponse result =
                    JsonUtility.FromJson<OnlineRoomResponse>(body);
                if (result == null || result.room == null)
                {
                    onlineStatusMessage = "The server returned an invalid room.";
                    return;
                }
                StartHostForOnlineRoom(result.room);
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private void JoinOnlineRoom(OnlineRoomInfo room)
    {
        if (onlineRequestInProgress || room == null)
            return;
        if (selectedTeam == TankTeam.Unassigned)
        {
            onlineStatusMessage = "Select a team before joining a room.";
            return;
        }

        OnlineRequestData data = new OnlineRequestData
        {
            team = selectedTeam == TankTeam.Blue ? "blue" : "red",
            playerName = string.IsNullOrWhiteSpace(onlinePlayerNameInput)
                ? onlineUsernameInput
                : onlinePlayerNameInput.Trim()
        };
        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            "/api/rooms/" + UnityWebRequest.EscapeURL(room.id) + "/join",
            "POST",
            JsonUtility.ToJson(data),
            body =>
            {
                onlineRequestInProgress = false;
                OnlineRoomResponse result =
                    JsonUtility.FromJson<OnlineRoomResponse>(body);
                if (result == null || result.room == null)
                {
                    onlineStatusMessage = "The server returned an invalid room.";
                    return;
                }
                ConnectToGameRoom(result.room);
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private void ConnectToGameRoom(OnlineRoomInfo room)
    {
        if (room == null ||
            string.IsNullOrWhiteSpace(room.ticket) ||
            multiplayerTankPrefab == null)
        {
            onlineStatusMessage =
                "The room has no valid game server endpoint.";
            return;
        }

        if (string.IsNullOrWhiteSpace(room.host) || room.port < 1)
        {
            onlineStatusMessage =
                "The room has no valid host endpoint.";
            return;
        }

        PrepareMultiplayerScene(GameMode.Online);
        showOnlineMenu = false;
        onlineStatusMessage = "Connecting to " + room.name + "...";
        onlineGameServer = CreateOnlineGameServer();
        onlineGameServer.Configure(
            multiplayerTankPrefab,
            4,
            Encoding.UTF8.GetBytes(room.ticket));
        onlineGameServer.SetConnectionApproval(null);

        bool useEncryption = IsHttpsEndpoint(onlineServiceUrl);

        if (!onlineGameServer.StartClient(
                room.host,
                (ushort)room.port,
                useEncryption,
                room.host))
        {
            onlineStatusMessage =
                "Could not connect to that game server.";
            ShutdownServers();
            RestoreScenePlayerAndAI();
            showMainMenu = true;
            showOnlineMenu = true;
        }
    }

    private IEnumerator SendOnlineRequest(
        string path,
        string method,
        string json,
        Action<string> onSuccess,
        Action<string> onFailure,
        string baseUrl = null,
        string authToken = null)
    {
        string serviceUrl = baseUrl == null
            ? onlineServiceUrl
            : baseUrl;
        if (!Uri.TryCreate(
                serviceUrl,
                UriKind.Absolute,
                out Uri serviceEndpoint) ||
            (serviceEndpoint.Scheme != Uri.UriSchemeHttp &&
             serviceEndpoint.Scheme != Uri.UriSchemeHttps))
        {
            onFailure?.Invoke(
                "The service URL must be a valid HTTP or HTTPS address."
            );
            yield break;
        }
        using (UnityWebRequest request = new UnityWebRequest(
            serviceEndpoint.ToString().TrimEnd('/') + path,
            method
        ))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            if (method != "GET")
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(json ?? "{}")
                );
                request.SetRequestHeader(
                    "Content-Type",
                    "application/json"
                );
            }
            request.timeout = 12;

            if (authToken == null)
            {
                if (!string.IsNullOrEmpty(onlineAuthToken))
                {
                    request.SetRequestHeader(
                        "Authorization",
                        "Bearer " + onlineAuthToken
                    );
                }
            }
            else if (!string.IsNullOrEmpty(authToken))
            {
                request.SetRequestHeader(
                    "Authorization",
                    "Bearer " + authToken
                );
            }

            yield return request.SendWebRequest();
            string message;
            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
                yield break;
            }

            if (request.responseCode == 0 ||
                request.result == UnityWebRequest.Result.ConnectionError)
            {
                message =
                    "Cannot connect to the game service at " +
                    serviceEndpoint.Authority + ".";
                Debug.LogWarning(
                    method + " " + serviceEndpoint + path +
                    " failed without an HTTP response: " +
                    request.error,
                    this
                );
                onFailure?.Invoke(message);
                yield break;
            }

            message =
                "Online service request failed (" +
                (long)request.responseCode + ").";
            if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
            {
                try
                {
                    OnlineErrorResponse roomError =
                        JsonUtility.FromJson<OnlineErrorResponse>(
                            request.downloadHandler.text
                        );
                    if (roomError != null &&
                        !string.IsNullOrWhiteSpace(roomError.error))
                        message = roomError.error;
                    if (roomError != null &&
                        !string.IsNullOrWhiteSpace(roomError.requestId))
                    {
                        message += " (request ID: " +
                            roomError.requestId + ").";
                    }
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning(
                        "Online service returned a non-JSON error response " +
                        "for " + method + " " + path + ": " +
                        exception.Message,
                        this
                    );
                }
            }
            if (request.responseCode >= 500)
            {
                message += " Check the online service log for this request.";
            }
            onFailure?.Invoke(message);
        }
    }

    private void SelectResolution(int index)
    {
        selectedResolution = Mathf.Clamp(index, 0, resolutions.Length - 1);
        UpdateSettingsStatus(
            "Selected " + resolutions[selectedResolution].x + " x " +
            resolutions[selectedResolution].y + "."
        );
    }

    private void ToggleFullscreen()
    {
        isFullscreen = !isFullscreen;
        UpdateSettingsStatus(
            "Fullscreen " + (isFullscreen ? "enabled." : "disabled.")
        );
    }

    private void ToggleTurretControl()
    {
        MouseTurretControlEnabled = !MouseTurretControlEnabled;
        UpdateSettingsStatus(
            "Turret aiming: " +
            (MouseTurretControlEnabled ? "mouse." : "keyboard.")
        );
    }

    private void UpdateSettingsStatus(string message)
    {
        if (settingsStatusLabel != null)
            settingsStatusLabel.text = message;
    }

    private void CloseSettingsMenu()
    {
        showSettings = false;
        showControlSettings = false;
    }

    private Sprite FindMenuSprite(string spriteName)
    {
        if (menuButtonSprites == null)
            return null;

        foreach (Sprite sprite in menuButtonSprites)
        {
            if (sprite != null &&
                sprite.name.Equals(
                    spriteName,
                    StringComparison.OrdinalIgnoreCase
                ))
                return sprite;
        }
        return null;
    }

    private void ApplyDisplaySettings()
    {
        selectedResolution = Mathf.Clamp(
            selectedResolution,
            0,
            resolutions.Length - 1
        );
        Vector2Int resolution = resolutions[selectedResolution];
        Screen.SetResolution(
            resolution.x,
            resolution.y,
            isFullscreen
        );
        UpdateSettingsStatus(
            "Applied " + resolution.x + " x " + resolution.y +
            (isFullscreen ? " fullscreen." : " windowed.")
        );
    }

    private void Update()
    {
        UpdateSceneMenuCanvasVisibility();
        UpdateResultCountLabels();
        SyncConnectedWallet();
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

}
