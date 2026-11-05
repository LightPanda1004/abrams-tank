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
    private void OnGUI()
    {
        CreateStyles();
        menuButtonDrawIndex = 0;

        if (showMainMenu)
        {
            if (showControlSettings)
            {
                HideMenuPanelCanvas();
                HideLegacyMenuPanelGraphics();
                Rect panel = GetMenuPanelRect();
                DrawControlSettings(panel.x, panel.y, panel.width);
                HideUnusedMenuButtons();
            }
            else
            {
                HideMenuButtons();
            }
            DrawMissileView();
            return;
        }

        if (showPauseMenu)
        {
            if (gamePauseCanvas != null)
            {
                HideMenuPanelCanvas();
                HideLegacyMenuPanelGraphics();
                HideMenuButtons();
                DrawMissileView();
                return;
            }

            if (showSettings || showControlSettings)
            {
                HideMenuPanelCanvas();
                if (showControlSettings)
                {
                    HideLegacyMenuPanelGraphics();
                    Rect panel = GetMenuPanelRect();
                    DrawControlSettings(panel.x, panel.y, panel.width);
                    HideUnusedMenuButtons();
                }
                else
                {
                    HideMenuButtons();
                }
            }
            else
            {
                DrawPauseMenu();
                HideUnusedMenuButtons();
            }
            DrawMissileView();
            return;
        }

        HideMenuPanelCanvas();
        if (!finishMenuVisible)
            DrawBattleHUD();

        if (finishMenuVisible)
        {
            Canvas resultCanvas = victory
                ? victoryMenuCanvas
                : defeatMenuCanvas;
            if (resultCanvas == null)
            {
                DrawGameOverMenu();
            }
            else
            {
                HideLegacyMenuPanelGraphics();
                HideMenuButtons();
            }
            HideUnusedMenuButtons();
        }
        else
        {
            HideMenuButtons();
        }

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
        GUIStyle style = null,
        string spriteGroupName = null)
    {
        EnsureMenuButtonCanvas();
        if (menuButtonCanvas == null)
            return false;

        spriteGroupName = string.IsNullOrEmpty(spriteGroupName)
            ? GetMenuButtonSpriteGroup(label)
            : spriteGroupName;
        if (!menuButtonSpriteSets.TryGetValue(
            spriteGroupName,
            out MenuButtonSpriteSet spriteSet))
        {
            if (!menuButtonMissingSpriteWarningLogged)
            {
                Debug.LogWarning(
                    "GameDirector: ButtonGroup is missing sprites for " +
                    spriteGroupName + ".",
                    this
                );
                menuButtonMissingSpriteWarningLogged = true;
            }
            return false;
        }

        if (spriteSet.normal == null ||
            spriteSet.hover == null ||
            spriteSet.pressed == null)
        {
            return false;
        }

        int index = menuButtonDrawIndex++;
        MenuButtonView view = GetMenuButtonView(index);
        if (view == null)
            return false;

        menuButtonCanvas.enabled = true;
        view.gameObject.SetActive(true);
        bool selectedTeamButton =
            style == selectedButtonStyle &&
            (spriteGroupName.Equals(
                "BlueTeam",
                System.StringComparison.OrdinalIgnoreCase
            ) ||
            spriteGroupName.Equals(
                "RedTeam",
                System.StringComparison.OrdinalIgnoreCase
            ));
        Sprite normalSprite = selectedTeamButton
            ? spriteSet.hover
            : spriteSet.normal;
        if (view.image.sprite != normalSprite)
            view.image.sprite = normalSprite;

        SpriteState spriteState = view.button.spriteState;
        spriteState.highlightedSprite = spriteSet.hover;
        spriteState.selectedSprite = spriteSet.hover;
        spriteState.pressedSprite = spriteSet.pressed;
        view.button.spriteState = spriteState;

        Matrix4x4 matrix = GUI.matrix;
        Vector3 screenCenter = matrix.MultiplyPoint3x4(rect.center);
        RectTransform canvasRect = (RectTransform)menuButtonCanvas.transform;
        float canvasScaleX = canvasRect.rect.width /
            Mathf.Max(Screen.width, 1f);
        float canvasScaleY = canvasRect.rect.height /
            Mathf.Max(Screen.height, 1f);

        view.rectTransform.anchorMin = new Vector2(0f, 1f);
        view.rectTransform.anchorMax = new Vector2(0f, 1f);
        view.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        view.rectTransform.anchoredPosition = new Vector2(
            screenCenter.x * canvasScaleX,
            -screenCenter.y * canvasScaleY
        );
        view.rectTransform.sizeDelta = new Vector2(
            MenuButtonWidth * canvasScaleX,
            MenuButtonHeight * canvasScaleY
        );

        view.button.interactable = GUI.enabled;

        ColorBlock colors = view.button.colors;
        colors.normalColor = style == selectedButtonStyle
            ? new Color(0.45f, 0.82f, 1f, 1f)
            : Color.white;
        colors.highlightedColor = new Color(0.63f, 0.9f, 1f, 1f);
        colors.pressedColor = new Color(0.5f, 0.72f, 0.86f, 1f);
        colors.disabledColor = new Color(0.55f, 0.6f, 0.64f, 0.55f);
        view.button.colors = colors;

        bool wasClicked = view.wasClicked;
        view.wasClicked = false;
        return wasClicked;
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

    private Rect GetMenuPanelRect(bool multiplayerMenu = false)
    {
        float aspect = multiplayerMenu
            ? MultiplayerMenuPanelAspect
            : MenuPanelAspect;
        float maximumWidth = multiplayerMenu
            ? MultiplayerMenuPanelMaximumWidth
            : MenuPanelMaximumWidth;
        float width = Mathf.Min(
            maximumWidth,
            Screen.width * 0.9f,
            Screen.height * 0.9f * aspect
        );
        float height = width / aspect;
        return new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
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
        const float layoutWidth = 420f;
        float layoutHeight = showMultiplayerMenu
            ? MultiplayerMenuLayoutHeight
            : showSettings || showControlSettings
                ? 440f
                : 390f;
        Sprite panelSprite = showMultiplayerMenu
            ? multiplayerMenuPanelSprite
            : showSettings || showControlSettings
                ? settingsMenuPanelSprite
                : startMenuPanelSprite;
        Rect panel = GetMenuPanelRect(showMultiplayerMenu);
        float width = panel.width;
        float height = panel.height;
        float x = panel.x;
        float y = panel.y;

        ShowMenuPanelCanvas(
            panel,
            panelSprite
        );

        float scaleX = width / layoutWidth;
        float scaleY = height / layoutHeight;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(
            new Vector3(x * (1f - scaleX), y * (1f - scaleY), 0f),
            Quaternion.identity,
            new Vector3(scaleX, scaleY, 1f)
        );

        if (showMultiplayerMenu)
        {
            DrawMultiplayerMenu(x, y, layoutWidth);
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
            new Rect(x + 50, y + 105, layoutWidth - 100, 48),
            "SINGLE PLAY",
            buttonStyle))
        {
            StartGame(GameMode.SinglePlay);
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 165, layoutWidth - 100, 48),
            "MULTIPLAYER",
            buttonStyle))
        {
            showMultiplayerMenu = true;
            showJoinRoomMenu = false;
            selectedTeam = TankTeam.Unassigned;
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 225, layoutWidth - 100, 48),
            "SETTINGS",
            buttonStyle))
        {
            showSettings = true;
        }

        if (DrawMenuButton(
            new Rect(x + 50, y + 285, layoutWidth - 100, 48),
            "EXIT",
            buttonStyle))
        {
            QuitGame();
        }
        GUI.matrix = previousMatrix;
    }

    private void DrawMultiplayerMenu(float x, float y, float width)
    {
        if (showJoinRoomMenu)
        {
            GUI.Label(
                new Rect(x + 34f, y + 150f, width - 68f, 24f),
                roomStatusMessage,
                statsStyle
            );

            Rect listRect = new Rect(x + 28f, y + 180f, width - 56f, 256f);
            float contentHeight = Mathf.Max(
                256f,
                lanRoomDiscovery != null
                    ? lanRoomDiscovery.Rooms.Count * 80f
                    : 256f
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
                    float rowY = listRect.y + i * 80f -
                        roomListScroll.y;
                    Rect row = new Rect(
                        listRect.x,
                        rowY,
                        listRect.width - 18f,
                        70f
                    );
                    string label = room.roomName + "   " +
                        room.playerCount + "/" + room.maxPlayers +
                        "   " + room.address;

                    bool rowIsVisible = row.y >= listRect.y &&
                        row.yMax <= listRect.yMax;
                    if (rowIsVisible &&
                        DrawMenuButton(
                            row,
                            label,
                            buttonStyle,
                            "JoinGame"
                        ))
                        JoinLanRoom(room);
                }
            }

            GUI.EndScrollView();

            if (lanRoomDiscovery == null ||
                lanRoomDiscovery.Rooms.Count == 0)
            {
                GUI.Label(
                    new Rect(x + 40f, y + 239f, width - 80f, 50f),
                    "No rooms found on this LAN yet.",
                    statsStyle
                );
            }

            if (DrawMenuButton(
                new Rect(x + 50f, y + 446f, width - 100f, 42f),
                "REFRESH",
                buttonStyle))
            {
                if (lanRoomDiscovery != null)
                    lanRoomDiscovery.StartSearching();
                roomStatusMessage = "Searching local network...";
            }
            if (DrawMenuButton(
                new Rect(x + 50f, y + 500f, width - 100f, 42f),
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

        GUI.Label(
            new Rect(x + 40f, y + 150f, width - 80f, 24f),
            "PLAYER NAME",
            statsStyle
        );
        playerNameInput = GUI.TextField(
            new Rect(x + 40f, y + 174f, width - 80f, 34f),
            playerNameInput,
            32,
            menuInputStyle
        );

        GUI.Label(
            new Rect(x + 40f, y + 212f, width - 80f, 24f),
            "SELECT YOUR TEAM",
            statsStyle
        );
        if (DrawMenuButton(
            new Rect(x + 50f, y + 236f, width - 100f, 44f),
            "BLUE TEAM",
            selectedTeam == TankTeam.Blue
                ? selectedButtonStyle
                : buttonStyle,
            "BlueTeam"))
        {
            selectedTeam = TankTeam.Blue;
        }
        if (DrawMenuButton(
            new Rect(x + 50f, y + 287f, width - 100f, 44f),
            "RED TEAM",
            selectedTeam == TankTeam.Red
                ? selectedButtonStyle
                : buttonStyle,
            "RedTeam"))
        {
            selectedTeam = TankTeam.Red;
        }

        GUI.Label(
            new Rect(x + 40f, y + 338f, width - 80f, 24f),
            "ROOM NAME",
            statsStyle
        );
        roomNameInput = GUI.TextField(
            new Rect(x + 40f, y + 362f, width - 80f, 34f),
            roomNameInput,
            24,
            menuInputStyle
        );
        GUI.Label(
            new Rect(x + 40f, y + 398f, width - 80f, 28f),
            roomStatusMessage,
            statsStyle
        );

        bool canUseMultiplayer = selectedTeam != TankTeam.Unassigned &&
            !string.IsNullOrWhiteSpace(playerNameInput);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = canUseMultiplayer;
        if (DrawMenuButton(
            new Rect(x + 50f, y + 430f, width - 100f, 44f),
            "CREATE GAME",
            buttonStyle))
        {
            CreateLanRoom();
        }

        if (DrawMenuButton(
            new Rect(x + 50f, y + 480f, width - 100f, 44f),
            "JOIN ROOM",
            buttonStyle))
        {
            if (canUseMultiplayer)
            {
                showJoinRoomMenu = true;
                roomStatusMessage = "Searching local network...";
                if (lanRoomDiscovery != null)
                    lanRoomDiscovery.StartSearching();
            }
        }
        GUI.enabled = previousEnabled;

        if (DrawMenuButton(
            new Rect(x + 50f, y + 534f, width - 100f, 38f),
            "BACK",
            buttonStyle))
        {
            showMultiplayerMenu = false;
            selectedTeam = TankTeam.Unassigned;
            roomStatusMessage = "";
        }
    }

    // --------------------------------------------------
    // PAUSE MENU
    // --------------------------------------------------

    private void DrawPauseMenu()
    {
        bool showPauseActions = !showSettings && !showControlSettings;
        Sprite panelSprite = showPauseActions
            ? pauseMenuPanelSprite
            : settingsMenuPanelSprite;
        Rect panel = GetMenuPanelRect();
        float width = panel.width;
        float height = panel.height;
        float actionScale = height / 495f;

        float x = panel.x;
        float y = panel.y;

        ShowMenuPanelCanvas(
            panel,
            panelSprite
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
                y + 130f * actionScale,
                width - 130f * actionScale,
                74f * actionScale
            ),
            "RESUME GAME",
            pauseMenuButtonStyle))
        {
            ResumeGame();
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 218f * actionScale,
                width - 130f * actionScale,
                74f * actionScale
            ),
            "PLAY AGAIN",
            pauseMenuButtonStyle))
        {
            PlayAgain();
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 306f * actionScale,
                width - 130f * actionScale,
                74f * actionScale
            ),
            "SETTINGS",
            pauseMenuButtonStyle))
        {
            showSettings = true;
        }

        if (DrawMenuButton(
            new Rect(
                x + 65f * actionScale,
                y + 394f * actionScale,
                width - 130f * actionScale,
                74f * actionScale
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
                    : buttonStyle,
                "Settings"))
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
                : buttonStyle,
            "ControlSettings"))
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
                : buttonStyle,
            "ControlSettings"))
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
        float layoutWidth = 380f;
        float layoutHeight = multiplayer ? 260f : 320f;
        Rect panel = GetMenuPanelRect();
        float width = panel.width;
        float height = panel.height;
        float x = panel.x;
        float y = panel.y;

        ShowMenuPanelCanvas(
            panel,
            pauseMenuPanelSprite
        );

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
            multiplayer ? "EXIT" : "MAIN MENU",
            buttonStyle))
        {
            if (multiplayer)
                QuitGame();
            else
                ReturnToMainMenu();
        }

        if (!multiplayer && DrawMenuButton(
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
        statsStyle.normal.textColor = new Color(0.86f, 0.93f, 1f, 1f);
        statsStyle.alignment = TextAnchor.MiddleLeft;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleLeft;
        titleStyle.normal.textColor = Color.white;

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
        buttonStyle.normal.textColor = Color.white;

        pauseMenuButtonStyle = new GUIStyle(buttonStyle);
        pauseMenuButtonStyle.fontSize = 23;

        selectedButtonStyle = new GUIStyle(buttonStyle);
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
    }}
