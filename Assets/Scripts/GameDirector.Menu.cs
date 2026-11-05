using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UI;

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
            OpenMultiplayerMenu
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

        playerNameInputField = EnsureInputField(
            multiplayerMenuCanvas,
            "PlayerNameInput",
            playerNameInput,
            32
        );
        roomNameInputField = EnsureInputField(
            multiplayerMenuCanvas,
            "RoomNameInput",
            roomNameInput,
            24
        );
        if (playerNameInputField != null)
        {
            playerNameInputField.onValueChanged.AddListener(
                value => playerNameInput = value
            );
        }
        if (roomNameInputField != null)
        {
            roomNameInputField.onValueChanged.AddListener(
                value => roomNameInput = value
            );
        }

        multiplayerStatusLabel = CreateSceneText(
            multiplayerMenuCanvas.transform,
            "MultiplayerStatus",
            new Vector2(0f, -360f),
            new Vector2(900f, 30f),
            18
        );
        BindMenuButton(
            multiplayerMenuCanvas,
            "BlueTeamBtn",
            () => SetSelectedTeam(TankTeam.Blue)
        );
        BindMenuButton(
            multiplayerMenuCanvas,
            "RedTeamBtn",
            () => SetSelectedTeam(TankTeam.Red)
        );
        createGameSceneButton = FindMenuButton(
            multiplayerMenuCanvas,
            "CreateGameBtn"
        );
        if (createGameSceneButton != null)
            createGameSceneButton.onClick.AddListener(OnCreateGameClicked);
        joinGameSceneButton = FindMenuButton(
            multiplayerMenuCanvas,
            "JoinGameBtn"
        );
        if (joinGameSceneButton != null)
            joinGameSceneButton.onClick.AddListener(OpenJoinRoomMenu);
        BindMenuButton(
            multiplayerMenuCanvas,
            "BackBtn",
            CloseMultiplayerMenu
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

        joinRoomStatusLabel = CreateSceneText(
            joinRoomMenuCanvas.transform,
            "JoinRoomStatus",
            new Vector2(0f, 155f),
            new Vector2(760f, 28f),
            18
        );
        joinRoomEmptyLabel = CreateSceneText(
            joinRoomMenuCanvas.transform,
            "JoinRoomEmpty",
            new Vector2(0f, 15f),
            new Vector2(760f, 36f),
            20
        );
        EnsureJoinRoomList();
        BindMenuButton(
            joinRoomMenuCanvas,
            "RefreshBtn",
            RefreshRooms
        );
        BindMenuButton(
            joinRoomMenuCanvas,
            "BackBtn",
            CloseJoinRoomMenu
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
        Action action)
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

    private InputField EnsureInputField(
        Canvas canvas,
        string objectName,
        string initialValue,
        int characterLimit)
    {
        Transform inputTransform = canvas.transform.Find(objectName);
        if (inputTransform == null)
        {
            Debug.LogError(
                "GameDirector: Could not find input object " + objectName +
                " under " + canvas.name + ".",
                canvas
            );
            return null;
        }

        InputField input = inputTransform.GetComponent<InputField>();
        if (input == null)
            input = inputTransform.gameObject.AddComponent<InputField>();

        Text text = inputTransform.GetComponentInChildren<Text>(true);
        if (text == null)
        {
            GameObject textObject = new GameObject(
                objectName + " Value",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            textObject.transform.SetParent(inputTransform, false);
            RectTransform textRect =
                (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);
            text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
        }

        text.fontSize = 16;
        input.textComponent = text;
        input.targetGraphic = inputTransform.GetComponent<Image>();
        input.characterLimit = characterLimit;
        input.lineType = InputField.LineType.SingleLine;
        input.caretWidth = 0;
        input.customCaretColor = true;
        input.caretColor = Color.clear;
        input.text = initialValue;
        return input;
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

        CanvasScaler multiplayerScaler =
            multiplayerMenuCanvas.GetComponent<CanvasScaler>();
        if (multiplayerScaler != null)
        {
            multiplayerScaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            multiplayerScaler.referenceResolution =
                new Vector2(1920f, 1080f);
            multiplayerScaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            multiplayerScaler.matchWidthOrHeight = 0.5f;
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

    private void EnsureJoinRoomList()
    {
        Transform viewportTransform =
            joinRoomMenuCanvas.transform.Find("RoomListViewport");
        if (viewportTransform == null)
        {
            GameObject viewportObject = new GameObject(
                "RoomListViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask),
                typeof(ScrollRect)
            );
            viewportObject.transform.SetParent(
                joinRoomMenuCanvas.transform,
                false
            );
            viewportTransform = viewportObject.transform;
            RectTransform viewportRect =
                (RectTransform)viewportTransform;
            viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = new Vector2(0f, 8f);
            viewportRect.sizeDelta = new Vector2(820f, 190f);

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            Mask mask = viewportObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObject = new GameObject(
                "RoomListContent",
                typeof(RectTransform)
            );
            contentObject.transform.SetParent(viewportTransform, false);
            joinRoomContent = (RectTransform)contentObject.transform;
            joinRoomContent.anchorMin = new Vector2(0f, 1f);
            joinRoomContent.anchorMax = new Vector2(1f, 1f);
            joinRoomContent.pivot = new Vector2(0.5f, 1f);
            joinRoomContent.sizeDelta = new Vector2(0f, 190f);

            joinRoomScrollRect =
                viewportObject.GetComponent<ScrollRect>();
            joinRoomScrollRect.viewport = viewportRect;
            joinRoomScrollRect.content = joinRoomContent;
            joinRoomScrollRect.horizontal = false;
            joinRoomScrollRect.vertical = true;
            joinRoomScrollRect.movementType =
                ScrollRect.MovementType.Clamped;
        }
        else
        {
            joinRoomScrollRect =
                viewportTransform.GetComponent<ScrollRect>();
            Transform contentTransform =
                viewportTransform.Find("RoomListContent");
            if (contentTransform != null)
                joinRoomContent = contentTransform as RectTransform;
        }
    }

    private void UpdateSceneMenuCanvasVisibility()
    {
        bool mainMenu = showMainMenu && !showMultiplayerMenu &&
            !showSettings && !showControlSettings;
        bool multiplayerMenu = showMainMenu && showMultiplayerMenu &&
            !showJoinRoomMenu;
        bool settingsMenu = (showMainMenu || showPauseMenu) && showSettings;
        bool joinMenu = showMainMenu && showMultiplayerMenu &&
            showJoinRoomMenu;

        SetCanvasEnabled(leopardBattleMenuCanvas, mainMenu);
        SetCanvasEnabled(multiplayerMenuCanvas, multiplayerMenu);
        SetCanvasEnabled(settingsMenuCanvas, settingsMenu);
        SetCanvasEnabled(joinRoomMenuCanvas, joinMenu);

        bool showScenePauseMenu = showPauseMenu &&
            !showSettings && !showControlSettings;
        bool showVictoryMenu = !showMainMenu && finishMenuVisible &&
            victory;
        bool showDefeatMenu = !showMainMenu && finishMenuVisible &&
            !victory;
        bool showGameplayStatusBars = !showMainMenu &&
            !showPauseMenu && !finishMenuVisible;

        if (!gameplayStatusVisibilityInitialized ||
            gameplayStatusBarsVisible != showGameplayStatusBars)
        {
            gameplayStatusVisibilityInitialized = true;
            gameplayStatusBarsVisible = showGameplayStatusBars;
            SetGameplayStatusBarsVisible(showGameplayStatusBars);
        }

        SetCanvasEnabled(gamePauseCanvas, showScenePauseMenu);
        SetCanvasEnabled(victoryMenuCanvas, showVictoryMenu);
        SetCanvasEnabled(defeatMenuCanvas, showDefeatMenu);

        bool resultCanvasMissing = victory
            ? victoryMenuCanvas == null
            : defeatMenuCanvas == null;
        bool useLegacyMenuCanvas =
            (showPauseMenu && gamePauseCanvas == null) ||
            (!showMainMenu && finishMenuVisible && resultCanvasMissing);
        SetCanvasEnabled(menuPanelCanvas, useLegacyMenuCanvas);
    }

    private static void SetCanvasEnabled(Canvas canvas, bool enabled)
    {
        if (canvas != null && canvas.enabled != enabled)
            canvas.enabled = enabled;
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

    private void OpenMultiplayerMenu()
    {
        showMainMenu = true;
        showMultiplayerMenu = true;
        showJoinRoomMenu = false;
        showSettings = false;
        showControlSettings = false;
        selectedTeam = TankTeam.Unassigned;
        roomStatusMessage = "";
    }

    private void CloseMultiplayerMenu()
    {
        showMultiplayerMenu = false;
        showJoinRoomMenu = false;
        selectedTeam = TankTeam.Unassigned;
        roomStatusMessage = "";
    }

    private void OpenSettingsMenu()
    {
        showSettings = true;
        showControlSettings = false;
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

    private void SetSelectedTeam(TankTeam team)
    {
        selectedTeam = team;
        SetTeamButtonSprite("BlueTeamBtn", team == TankTeam.Blue);
        SetTeamButtonSprite("RedTeamBtn", team == TankTeam.Red);
    }

    private void SetTeamButtonSprite(string buttonName, bool selected)
    {
        Transform buttonTransform =
            multiplayerMenuCanvas.transform.Find(buttonName);
        if (buttonTransform == null)
            return;

        Image image = buttonTransform.GetComponent<Image>();
        if (image == null)
            return;

        string spriteName = buttonName == "BlueTeamBtn"
            ? "BlueTeambtn_"
            : "RedTeambtn_";
        Sprite sprite = FindMenuSprite(
            spriteName + (selected ? "select" : "normal")
        );
        if (sprite != null)
            image.sprite = sprite;
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

    private void OnCreateGameClicked()
    {
        CopyMultiplayerInputValues();
        CreateLanRoom();
        UpdateSceneMenuCanvasVisibility();
    }

    private void OpenJoinRoomMenu()
    {
        CopyMultiplayerInputValues();
        if (selectedTeam == TankTeam.Unassigned)
        {
            roomStatusMessage = "Select a team before joining a room.";
            return;
        }
        if (string.IsNullOrWhiteSpace(playerNameInput))
        {
            roomStatusMessage = "Enter a player name before joining.";
            return;
        }

        showJoinRoomMenu = true;
        roomStatusMessage = "Searching local network...";
        RefreshRooms();
        UpdateSceneMenuCanvasVisibility();
    }

    private void CopyMultiplayerInputValues()
    {
        if (playerNameInputField != null)
            playerNameInput = playerNameInputField.text;
        if (roomNameInputField != null)
            roomNameInput = roomNameInputField.text;
    }

    private void RefreshRooms()
    {
        if (lanRoomDiscovery == null)
        {
            roomStatusMessage = "LAN room discovery is unavailable.";
            return;
        }

        lanRoomDiscovery.StartSearching();
        roomStatusMessage = "Searching local network...";
        joinRoomListNeedsRefresh = true;
        RefreshJoinRoomList();
    }

    private void CloseJoinRoomMenu()
    {
        showJoinRoomMenu = false;
        roomStatusMessage = "";
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StopSearching();
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

    private void RefreshJoinRoomList()
    {
        if (joinRoomContent == null || lanRoomDiscovery == null)
            return;

        IReadOnlyList<LanRoomInfo> rooms = lanRoomDiscovery.Rooms;
        bool roomListChanged =
            joinRoomListNeedsRefresh ||
            displayedRoomList.Count != rooms.Count;
        for (int i = 0; !roomListChanged && i < rooms.Count; i++)
        {
            roomListChanged = !displayedRoomList[i].Matches(rooms[i]);
        }

        if (roomListChanged)
        {
            RebuildJoinRoomButtons(rooms);
            displayedRoomList.Clear();
            for (int i = 0; i < rooms.Count; i++)
                displayedRoomList.Add(RoomListEntry.From(rooms[i]));
            joinRoomListNeedsRefresh = false;
        }

        if (joinRoomStatusLabel != null &&
            !string.Equals(
                joinRoomStatusLabel.text,
                roomStatusMessage,
                StringComparison.Ordinal))
            joinRoomStatusLabel.text = roomStatusMessage;
        if (joinRoomEmptyLabel != null)
        {
            string emptyMessage = rooms.Count == 0
                ? "No rooms found yet."
                : "";
            if (joinRoomEmptyLabel.text != emptyMessage)
                joinRoomEmptyLabel.text = emptyMessage;
        }
    }

    private void RebuildJoinRoomButtons(IReadOnlyList<LanRoomInfo> rooms)
    {
        foreach (Button button in joinRoomButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }
        joinRoomButtons.Clear();
        joinRoomButtonLabels.Clear();

        float contentHeight = Mathf.Max(190f, rooms.Count * 58f);
        joinRoomContent.sizeDelta = new Vector2(0f, contentHeight);
        for (int i = 0; i < rooms.Count; i++)
        {
            int roomIndex = i;
            GameObject buttonObject = new GameObject(
                "Room " + i,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(joinRoomContent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -i * 58f);
            rect.sizeDelta = new Vector2(760f, 50f);

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = FindMenuSprite("GameRoomListBtn_normal");
            image.raycastTarget = true;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite =
                FindMenuSprite("GameRoomListBtn_hover");
            spriteState.selectedSprite =
                FindMenuSprite("GameRoomListBtn_hover");
            spriteState.pressedSprite =
                FindMenuSprite("GameRoomListBtn_press");
            button.spriteState = spriteState;
            button.onClick.AddListener(
                () => JoinLanRoomAtIndex(roomIndex)
            );

            Text label = CreateSceneText(
                buttonObject.transform,
                "Label",
                Vector2.zero,
                new Vector2(720f, 42f),
                20
            );
            joinRoomButtons.Add(button);
            joinRoomButtonLabels.Add(label);
        }
        UpdateJoinRoomButtonLabels(rooms);
        if (joinRoomScrollRect != null)
            joinRoomScrollRect.verticalNormalizedPosition = 1f;
    }

    private void UpdateJoinRoomButtonLabels(
        IReadOnlyList<LanRoomInfo> rooms)
    {
        int count = Mathf.Min(joinRoomButtonLabels.Count, rooms.Count);
        for (int i = 0; i < count; i++)
        {
            LanRoomInfo room = rooms[i];
            joinRoomButtonLabels[i].text =
                room.roomName + "  " + room.playerCount + "/" +
                room.maxPlayers + "  " + room.address;
        }
    }

    private void JoinLanRoomAtIndex(int roomIndex)
    {
        if (lanRoomDiscovery == null ||
            roomIndex < 0 ||
            roomIndex >= lanRoomDiscovery.Rooms.Count)
        {
            roomStatusMessage = "That room is no longer available.";
            return;
        }

        CopyMultiplayerInputValues();
        JoinLanRoom(lanRoomDiscovery.Rooms[roomIndex]);
        UpdateSceneMenuCanvasVisibility();
    }

    private void Update()
    {
        UpdateSceneMenuCanvasVisibility();
        UpdateResultCountLabels();
        if (showMainMenu && showJoinRoomMenu)
            RefreshJoinRoomList();
        if (multiplayerStatusLabel != null &&
            !string.Equals(
                multiplayerStatusLabel.text,
                roomStatusMessage,
                StringComparison.Ordinal))
            multiplayerStatusLabel.text = roomStatusMessage;
        if (createGameSceneButton != null)
        {
            bool interactable =
                selectedTeam != TankTeam.Unassigned &&
                !string.IsNullOrWhiteSpace(playerNameInput);
            if (createGameSceneButton.interactable != interactable)
                createGameSceneButton.interactable = interactable;
        }
        if (joinGameSceneButton != null)
        {
            bool interactable =
                selectedTeam != TankTeam.Unassigned &&
                !string.IsNullOrWhiteSpace(playerNameInput);
            if (joinGameSceneButton.interactable != interactable)
                joinGameSceneButton.interactable = interactable;
        }

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
