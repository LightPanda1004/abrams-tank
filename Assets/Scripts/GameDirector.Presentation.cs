using UnityEngine;

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

        DrawOnlineRoomHud();

        if (finishMenuVisible)
            DrawGameOverMenu();

        DrawMissileView();
    }

    private bool DrawMenuButton(
        Rect rect,
        string label,
        GUIStyle style = null)
    {
        Color previousColor = GUI.color;
        bool isHovered = Event.current.type == EventType.Repaint &&
            rect.Contains(Event.current.mousePosition);
        GUI.color = style == selectedButtonStyle
            ? new Color(1f, 0.91f, 0.67f, 1f)
            : isHovered
                ? new Color(0.88f, 0.91f, 0.73f, 1f)
                : Color.white;
        try
        {
            return GUI.Button(rect, label, style ?? buttonStyle);
        }
        finally
        {
            GUI.color = previousColor;
        }
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

    private void DrawMenuPanel(
        Rect panel,
        string title,
        float layoutScale = 1f)
    {
        GUI.Box(panel, GUIContent.none, menuPanelStyle);
        if (title == "LEOPARD BATTLE" && menuTitleLogo != null)
        {
            GUI.DrawTexture(
                new Rect(
                    panel.x + 36f * layoutScale,
                    panel.y + 76f * layoutScale,
                    panel.width - 72f * layoutScale,
                    112f * layoutScale
                ),
                menuTitleLogo,
                ScaleMode.ScaleToFit,
                true
            );
        }
        else
        {
            menuTitleStyle.fontSize = Mathf.Clamp(
                Mathf.RoundToInt(25f * layoutScale),
                14,
                29
            );
            GUI.Label(
                new Rect(
                    panel.x + 30f * layoutScale,
                    panel.y + 17f * layoutScale,
                    panel.width - 60f * layoutScale,
                    40f * layoutScale
                ),
                title,
                menuTitleStyle
            );
        }
        Color previousColor = GUI.color;
        GUI.color = new Color(0.95f, 0.65f, 0.24f, 0.9f);
        GUI.DrawTexture(
            new Rect(
                panel.x + 24f * layoutScale,
                panel.y + 64f * layoutScale,
                panel.width - 48f * layoutScale,
                Mathf.Max(1f, 2f * layoutScale)
            ),
            Texture2D.whiteTexture
        );
        GUI.color = previousColor;
    }

    private void DrawMenuBackdrop()
    {
        Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.color = Color.white;
        GUI.DrawTexture(
            screen,
            menuBackgroundTexture != null
                ? menuBackgroundTexture
                : Texture2D.blackTexture,
            ScaleMode.ScaleAndCrop
        );

        GUI.color = new Color(0.025f, 0.03f, 0.025f, 0.34f);
        GUI.DrawTexture(screen, Texture2D.whiteTexture);
        GUI.color = Color.white;
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
        float layoutWidth = showRoomMenu
            ? 640f
            : showEconomyMenu
            ? 860f
            : showOnlineMenu ? 640f : 500f;
        float layoutHeight = showRoomMenu
            ? 720f
            : showEconomyMenu
            ? 830f
            : showOnlineMenu
            ? string.IsNullOrEmpty(onlineAuthToken) ? 430f
                : string.IsNullOrEmpty(onlineWalletAddress) ? 600f : 470f
            : showSettings || showControlSettings ? 440f : 560f;
        Rect panel = GetMenuPanelRect(layoutWidth, layoutHeight);
        if (!showRoomMenu && !showEconomyMenu && !showOnlineMenu &&
            !showSettings && !showControlSettings && Screen.width >= 960)
        {
            panel.x = Mathf.Max(24f, Screen.width * 0.055f);
        }
        float width = panel.width;
        float height = panel.height;
        float x = panel.x;
        float y = panel.y;

        DrawMenuBackdrop();
        string title = showRoomMenu
            ? "ONLINE ROOMS"
            : showEconomyMenu
            ? "TCOIN ECONOMY"
            : showOnlineMenu
                ? "ACCOUNT & WALLET"
            : showControlSettings
                ? "CONTROL SETTINGS"
                : showSettings ? "SETTINGS" : "LEOPARD BATTLE";
        DrawMenuPanel(panel, title, width / layoutWidth);

        float scaleX = width / layoutWidth;
        float scaleY = height / layoutHeight;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(
            new Vector3(x * (1f - scaleX), y * (1f - scaleY), 0f),
            Quaternion.identity,
            new Vector3(scaleX, scaleY, 1f)
        );

        if (showRoomMenu)
        {
            DrawOnlineRoomMenu(x, y, layoutWidth);
            GUI.matrix = previousMatrix;
            return;
        }

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
            new Rect(x + 40, y + 196, layoutWidth - 80, 44),
            "SINGLE PLAYER",
            buttonStyle))
        {
            StartGame();
        }
        GUI.Label(
            new Rect(x + 58, y + 241, layoutWidth - 116, 20),
            "SOLO CAMPAIGN   /   FIGHT AI TANKS",
            menuSubtitleStyle
        );

        if (DrawMenuButton(
            new Rect(x + 40, y + 275, layoutWidth - 80, 44),
            "ONLINE PLAY",
            buttonStyle))
        {
            OpenOnlineMenu();
        }
        GUI.Label(
            new Rect(x + 58, y + 320, layoutWidth - 116, 20),
            "JOIN A ROOM   /   FIGHT WITH YOUR TEAM",
            menuSubtitleStyle
        );

        if (DrawMenuButton(
            new Rect(x + 40, y + 354, layoutWidth - 80, 44),
            "SETTINGS",
            buttonStyle))
        {
            showSettings = true;
        }
        GUI.Label(
            new Rect(x + 58, y + 399, layoutWidth - 116, 20),
            "DISPLAY   /   CONTROLS",
            menuSubtitleStyle
        );

#if !UNITY_WEBGL || UNITY_EDITOR
        if (DrawMenuButton(
            new Rect(x + 40, y + 433, layoutWidth - 80, 44),
            "EXIT GAME",
            buttonStyle))
        {
            QuitGame();
        }
#endif
        GUI.Label(
            new Rect(x + 40, y + 500, layoutWidth - 80, 22),
            "WASD / ARROWS  MOVE       MOUSE  AIM       CLICK  FIRE",
            menuSubtitleStyle
        );
        GUI.matrix = previousMatrix;
    }

    private void DrawOnlineMenu(float x, float y, float width)
    {
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

        if (string.IsNullOrEmpty(onlineWalletAddress))
        {
            GUI.Label(
                new Rect(x + 42f, y + 175f, width - 84f, 22f),
                "REWARD WALLET ADDRESS",
                statsStyle
            );
            onlineWalletAddressInput = GUI.TextField(
                new Rect(x + 42f, y + 199f, width - 84f, 36f),
                onlineWalletAddressInput,
                42,
                menuInputStyle
            );

            bool previousWalletControlsEnabled = GUI.enabled;
            float walletButtonWidth = (width - 100f) * 0.5f;
            GUI.enabled = walletKitReady;
            if (DrawMenuButton(
                new Rect(x + 42f, y + 241f, walletButtonWidth, 32f),
                WalletConnectButtonLabel(),
                buttonStyle))
                ConnectOnlineWallet();

            GUI.enabled = walletKitReady;
            if (DrawMenuButton(
                new Rect(x + 58f + walletButtonWidth, y + 241f,
                    walletButtonWidth, 32f),
                WalletSearchButtonLabel(),
                buttonStyle))
                OpenMetaMaskWalletSearch();

            GUI.enabled = previousWalletControlsEnabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            const float manualWalletOffset = 24f;
            GUI.Label(
                new Rect(x + 42f, y + 270f, width - 84f, 22f),
                "Chrome + MetaMask extension works without a phone.",
                statsStyle
            );
#else
            const float manualWalletOffset = 0f;
#endif
            GUI.Label(
                new Rect(
                    x + 42f,
                    y + 277f + manualWalletOffset,
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
                    y + 301f + manualWalletOffset,
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
                    y + 335f + manualWalletOffset,
                    width - 84f,
                    22f
                ),
                "WALLET SIGNATURE",
                statsStyle
            );
            onlineWalletSignatureInput = GUI.TextField(
                new Rect(
                    x + 42f,
                    y + 359f + manualWalletOffset,
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
                    y + 401f + manualWalletOffset,
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
                new Rect(x + 42f, y + 175f, width - 84f, 22f),
                "VERIFIED REWARD WALLET",
                statsStyle
            );
            GUI.Label(
                new Rect(x + 42f, y + 199f, width - 84f, 38f),
                onlineWalletAddress,
                statsStyle
            );
            GUI.Label(
                new Rect(x + 42f, y + 237f, width - 84f, 22f),
                "This wallet is permanently linked to this account.",
                statsStyle
            );
        }

        if (DrawMenuButton(
            new Rect(x + 42f, y +
                (string.IsNullOrEmpty(onlineWalletAddress) ? 454f : 285f),
                width - 84f, 36f),
            "OPEN TCOIN ECONOMY",
            buttonStyle))
        {
            showEconomyMenu = true;
            StartCoroutine(CheckConfiguredContracts());
        }
        float actionWidth = (width - 100f) * 0.5f;
        float accountActionsY =
            string.IsNullOrEmpty(onlineWalletAddress) ? 498f : 329f;
        if (DrawMenuButton(
            new Rect(x + 42f, y + accountActionsY,
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
            onlineStatusMessage = "Signed out.";
        }
        if (DrawMenuButton(
            new Rect(x + 58f + actionWidth, y + accountActionsY,
                actionWidth, 36f),
            "BACK",
            buttonStyle))
        {
            showOnlineMenu = false;
            onlineStatusMessage = "";
        }

        if (DrawMenuButton(
            new Rect(x + 42f, y + accountActionsY + 44f,
                width - 84f, 36f),
            "PLAY ONLINE",
            buttonStyle))
        {
            OpenOnlineRooms();
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

    private void DrawPauseMenu()
    {
        DrawMenuBackdrop();
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
                    : "SETTINGS",
            width / 420f
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

#if !UNITY_WEBGL || UNITY_EDITOR
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
#endif
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

        GUI.Box(
            new Rect(x, y, panelWidth, 238f),
            "",
            hudPanelStyle
        );
        float countWidth = (panelWidth - inset * 2f - 8f) / 2f;
        Rect totalCard = new Rect(x + inset, y + 12f, countWidth, 48f);
        Rect destroyedCard = new Rect(
            totalCard.xMax + 8f, totalCard.y, countWidth, 48f);
        GUI.Box(totalCard, "", hudBarBackgroundStyle);
        GUI.Box(destroyedCard, "", hudBarBackgroundStyle);
        GUI.Label(new Rect(totalCard.x + 8f, totalCard.y + 3f, countWidth - 16f, 16f), "AI TANKS", statsStyle);
        GUI.Label(new Rect(totalCard.x + 8f, totalCard.y + 19f, countWidth - 16f, 25f), totalAITanks.ToString(), hudValueStyle);
        GUI.Label(new Rect(destroyedCard.x + 8f, destroyedCard.y + 3f, countWidth - 16f, 16f), "DESTROYED", statsStyle);
        GUI.Label(new Rect(destroyedCard.x + 8f, destroyedCard.y + 19f, countWidth - 16f, 25f), destroyedTanks.ToString(), hudValueStyle);

        GUI.Label(
            new Rect(x + inset, y + 66f,
                panelWidth - inset * 2f, 18f),
            "WEAPON COOLDOWN",
            statsStyle);
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

        DrawWeaponMeter(
            new Vector2(leftCenterX, meterY), missileProgress,
            new Color(0.72f, 0.31f, 0.23f, 1f), "MISSILE", missileValue);
        DrawWeaponMeter(
            new Vector2(rightCenterX, meterY), shellProgress,
            new Color(0.72f, 0.61f, 0.36f, 1f), "MAIN GUN", shellValue);

        float speed = playerMovement != null
            ? Mathf.Abs(playerMovement.currentSpeed)
            : 0f;
        float maxSpeed = playerMovement != null
            ? Mathf.Max(0.1f, playerMovement.maxSpeed)
            : 1f;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        int speedKph = Mathf.RoundToInt(speed * 3.6f);

        Rect speedCard = new Rect(
            x + inset, y + 177f,
            panelWidth - inset * 2f, 49f);
        GUI.Box(speedCard, "", hudBarBackgroundStyle);
        GUI.Label(new Rect(speedCard.x + 8f, speedCard.y + 3f, speedCard.width - 16f, 17f), "TANK SPEED", statsStyle);
        GUI.Label(new Rect(speedCard.x + speedCard.width - 88f, speedCard.y + 2f, 80f, 19f), speedKph + " km/h", hudValueStyle);

        Rect speedBar = new Rect(
            speedCard.x + 8f, speedCard.y + 27f,
            speedCard.width - 16f, 13f);
        DrawTacticalBar(speedBar, speedRatio, new Color(0.59f, 0.64f, 0.39f));

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
        Color healthColor = health <= 0.25f
            ? new Color(0.72f, 0.28f, 0.2f)
            : health <= 0.5f
                ? new Color(0.78f, 0.56f, 0.25f)
                : new Color(0.58f, 0.66f, 0.4f);
        DrawTacticalBar(healthBar, health, healthColor);
    }

    private void DrawWeaponMeter(
        Vector2 center,
        float fill,
        Color accent,
        string label,
        string valueText)
    {
        Rect card = new Rect(center.x - 64f, center.y - 32f, 128f, 72f);
        GUI.Box(card, "", hudBarBackgroundStyle);
        GUI.Label(
            new Rect(card.x + 8f, card.y + 4f, card.width - 16f, 18f),
            label,
            statsStyle
        );
        GUI.Label(
            new Rect(card.x + 8f, card.y + 21f, card.width - 16f, 20f),
            valueText,
            hudMeterValueStyle
        );
        DrawTacticalBar(
            new Rect(card.x + 8f, card.y + 49f, card.width - 16f, 11f),
            fill,
            accent
        );
    }

    private void DrawTacticalBar(Rect rect, float value, Color fillColor)
    {
        GUI.Box(rect, "", hudBarBackgroundStyle);
        float innerWidth = Mathf.Max(0f, rect.width - 4f);
        float fillWidth = innerWidth * Mathf.Clamp01(value);
        if (fillWidth <= 0f)
            return;

        Color previousColor = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(
            new Rect(rect.x + 2f, rect.y + 2f, fillWidth, rect.height - 4f),
            Texture2D.whiteTexture
        );
        GUI.color = previousColor;

        const int segmentCount = 12;
        float segmentWidth = innerWidth / segmentCount;
        for (int i = 1; i < segmentCount; i++)
        {
            float markerX = rect.x + 2f + i * segmentWidth - 1f;
            if (markerX < rect.x + 2f + fillWidth)
            {
                GUI.DrawTexture(
                    new Rect(markerX, rect.y + 2f, 2f, rect.height - 4f),
                    Texture2D.blackTexture
                );
            }
        }
    }

    // --------------------------------------------------
    // FINISH MENU
    // --------------------------------------------------

    private void DrawGameOverMenu()
    {
        DrawMenuBackdrop();
        float layoutWidth = 380f;
        float layoutHeight = 320f;
        Rect panel = GetMenuPanelRect(layoutWidth, layoutHeight);
        float width = panel.width;
        float height = panel.height;
        float x = panel.x;
        float y = panel.y;

        DrawMenuPanel(
            panel,
            victory ? "VICTORY" : "DEFEAT",
            width / layoutWidth
        );

        float scaleX = width / layoutWidth;
        float scaleY = height / layoutHeight;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(
            new Vector3(x * (1f - scaleX), y * (1f - scaleY), 0f),
            Quaternion.identity,
            new Vector3(scaleX, scaleY, 1f)
        );

        string destroyedCount = destroyedTanks + " / " + totalAITanks;
        GUI.Label(
            new Rect(x + 20, y + 85, layoutWidth - 40, 30),
            "Destroyed AI Tanks: " + destroyedCount,
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

#if !UNITY_WEBGL || UNITY_EDITOR
        if (DrawMenuButton(
            new Rect(x + 50, y + 245, layoutWidth - 100, 35),
            "EXIT",
            buttonStyle))
            QuitGame();
#endif

        GUI.matrix = previousMatrix;
    }

    // --------------------------------------------------
    // GUI STYLES
    // --------------------------------------------------

    private void CreateStyles()
    {
        if (statsStyle != null)
            return;

        menuFont = Resources.Load<Font>("MenuUI/BarlowCondensed-Bold");
        menuBackgroundTexture =
            Resources.Load<Texture2D>("MenuUI/BattlefieldBackground");
        menuTitleLogo = Resources.Load<Texture2D>("MenuUI/TitleLogo");
        if (menuBackgroundTexture == null || menuTitleLogo == null)
        {
            Debug.LogWarning(
                "The menu background or title logo could not be loaded from " +
                "Assets/Resources/MenuUI; default UI fallbacks will be used.",
                this
            );
        }
        if (menuFont == null)
        {
            Debug.LogWarning(
                "Barlow Condensed could not be loaded from Resources/MenuUI; " +
                "the default Unity font will be used.",
                this
            );
        }

        statsStyle = new GUIStyle(GUI.skin.label);
        statsStyle.fontSize = 15;
        statsStyle.fontStyle = FontStyle.Bold;
        statsStyle.normal.textColor = new Color(0.88f, 0.9f, 0.91f, 1f);
        statsStyle.alignment = TextAnchor.MiddleLeft;
        if (menuFont != null)
            statsStyle.font = menuFont;

        menuPanelStyle = new GUIStyle(GUI.skin.box);
        menuPanelStyle.normal.background = CreateMenuSurfaceTexture(
            96,
            96,
            new Color(0.11f, 0.12f, 0.10f, 0.97f),
            new Color(0.045f, 0.05f, 0.045f, 0.98f),
            new Color(0.62f, 0.53f, 0.32f, 0.82f),
            12,
            2
        );
        menuPanelStyle.border = new RectOffset(16, 16, 16, 16);
        menuPanelStyle.padding = new RectOffset(0, 0, 0, 0);

        menuTitleStyle = new GUIStyle(GUI.skin.label);
        menuTitleStyle.fontSize = 25;
        menuTitleStyle.fontStyle = FontStyle.Bold;
        menuTitleStyle.alignment = TextAnchor.MiddleLeft;
        menuTitleStyle.normal.textColor = new Color(0.91f, 0.89f, 0.81f);
        menuTitleStyle.clipping = TextClipping.Clip;
        if (menuFont != null)
            menuTitleStyle.font = menuFont;

        menuSubtitleStyle = new GUIStyle(GUI.skin.label);
        menuSubtitleStyle.fontSize = 15;
        menuSubtitleStyle.alignment = TextAnchor.MiddleLeft;
        menuSubtitleStyle.normal.textColor = new Color(0.72f, 0.71f, 0.64f);
        menuSubtitleStyle.clipping = TextClipping.Clip;
        if (menuFont != null)
            menuSubtitleStyle.font = menuFont;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 18;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleLeft;
        buttonStyle.padding = new RectOffset(22, 16, 8, 8);
        buttonStyle.border = new RectOffset(8, 8, 8, 8);
        Texture2D buttonBackground = CreateTacticalButtonTexture();
        buttonStyle.normal.background = buttonBackground;
        buttonStyle.hover.background = buttonBackground;
        buttonStyle.active.background = buttonBackground;
        buttonStyle.focused.background = buttonStyle.hover.background;
        buttonStyle.normal.textColor = new Color(0.91f, 0.89f, 0.81f);
        buttonStyle.hover.textColor = new Color(1f, 0.97f, 0.86f);
        buttonStyle.active.textColor = new Color(1f, 0.94f, 0.78f);
        buttonStyle.focused.textColor = new Color(1f, 0.97f, 0.86f);
        if (menuFont != null)
            buttonStyle.font = menuFont;

        pauseMenuButtonStyle = new GUIStyle(buttonStyle);
        pauseMenuButtonStyle.fontSize = 23;

        selectedButtonStyle = new GUIStyle(buttonStyle);
        selectedButtonStyle.normal.textColor = new Color(1f, 0.9f, 0.67f);

        menuInputStyle = new GUIStyle(GUI.skin.textField);
        menuInputStyle.fontSize = 17;
        menuInputStyle.alignment = TextAnchor.MiddleLeft;
        menuInputStyle.padding = new RectOffset(12, 12, 5, 5);
        menuInputStyle.normal.background = CreateMenuSurfaceTexture(
            64,
            48,
            new Color(0.19f, 0.21f, 0.17f, 0.98f),
            new Color(0.10f, 0.12f, 0.10f, 0.98f),
            new Color(0.62f, 0.56f, 0.39f, 0.94f),
            8,
            2
        );
        menuInputStyle.hover.background = menuInputStyle.normal.background;
        menuInputStyle.focused.background = CreateMenuSurfaceTexture(
            64,
            48,
            new Color(0.25f, 0.27f, 0.21f, 1f),
            new Color(0.13f, 0.15f, 0.12f, 1f),
            new Color(0.88f, 0.74f, 0.45f, 1f),
            8,
            2
        );
        menuInputStyle.active.background = menuInputStyle.focused.background;
        menuInputStyle.normal.textColor = Color.white;
        menuInputStyle.focused.textColor = Color.white;
        menuInputStyle.hover.textColor = Color.white;
        menuInputStyle.active.textColor = Color.white;
        if (menuFont != null)
            menuInputStyle.font = menuFont;

        menuToggleStyle = new GUIStyle(GUI.skin.toggle);
        menuToggleStyle.fontSize = 16;
        menuToggleStyle.normal.textColor = Color.white;
        menuToggleStyle.hover.textColor = Color.white;
        menuToggleStyle.onNormal.textColor = Color.white;
        menuToggleStyle.onHover.textColor = Color.white;
        if (menuFont != null)
            menuToggleStyle.font = menuFont;

        hudPanelStyle = new GUIStyle(GUI.skin.box);
        hudPanelStyle.normal.background = CreateSolidTexture(
            new Color(0.055f, 0.065f, 0.05f, 0.94f)
        );
        hudPanelStyle.border = new RectOffset(18, 18, 18, 18);
        hudPanelStyle.padding = new RectOffset(8, 8, 8, 8);

        hudValueStyle = new GUIStyle(GUI.skin.label);
        hudValueStyle.fontSize = 20;
        hudValueStyle.fontStyle = FontStyle.Bold;
        hudValueStyle.normal.textColor = new Color(0.9f, 0.88f, 0.79f);
        hudValueStyle.alignment = TextAnchor.MiddleCenter;
        if (menuFont != null)
            hudValueStyle.font = menuFont;

        hudMeterValueStyle = new GUIStyle(hudValueStyle);
        hudMeterValueStyle.fontSize = 14;

        hudBarBackgroundStyle = new GUIStyle(GUI.skin.box);
        hudBarBackgroundStyle.normal.background = CreateMenuSurfaceTexture(
            128,
            32,
            new Color(0.16f, 0.18f, 0.14f, 0.98f),
            new Color(0.07f, 0.09f, 0.07f, 0.99f),
            new Color(0.39f, 0.41f, 0.32f, 0.92f),
            3,
            2
        );
        hudBarBackgroundStyle.border = new RectOffset(7, 7, 7, 7);

    }

    private Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        generatedGuiTextures.Add(texture);
        return texture;
    }

    private Texture2D CreateTacticalButtonTexture()
    {
        const int width = 192;
        const int height = 64;
        Color[] pixels = new Color[width * height];
        Color edgeColor = new Color(0.52f, 0.54f, 0.43f, 1f);
        Color topColor = new Color(0.27f, 0.30f, 0.24f, 1f);
        Color bottomColor = new Color(0.12f, 0.15f, 0.13f, 1f);
        Color accentColor = new Color(0.72f, 0.57f, 0.31f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int left = x;
                int right = width - 1 - x;
                int bottom = y;
                int top = height - 1 - y;
                bool clippedCorner =
                    (left < 8 && bottom < 8 && left + bottom < 7) ||
                    (right < 8 && bottom < 8 && right + bottom < 7) ||
                    (left < 8 && top < 8 && left + top < 7) ||
                    (right < 8 && top < 8 && right + top < 7);
                int pixelIndex = y * width + x;
                if (clippedCorner)
                {
                    pixels[pixelIndex] = Color.clear;
                    continue;
                }

                bool outerEdge = x < 2 || right < 2 || y < 2 || top < 2;
                bool innerEdge = x == 4 || right == 4 || y == 4 || top == 4;
                if (outerEdge)
                {
                    pixels[pixelIndex] = edgeColor;
                }
                else if (innerEdge)
                {
                    pixels[pixelIndex] =
                        new Color(0.07f, 0.085f, 0.07f, 0.95f);
                }
                else if (x >= 8 && x < 14)
                {
                    pixels[pixelIndex] = accentColor;
                }
                else if (y == height - 6)
                {
                    pixels[pixelIndex] =
                        new Color(0.47f, 0.48f, 0.37f, 0.55f);
                }
                else
                {
                    float blend = y / (float)(height - 1);
                    Color baseColor = Color.Lerp(
                        bottomColor,
                        topColor,
                        blend
                    );
                    bool textureLine = (y % 6 == 0) && x > 16;
                    pixels[pixelIndex] = textureLine
                        ? Color.Lerp(baseColor, Color.white, 0.035f)
                        : baseColor;
                }
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
        OnDestroyOnlineRoom();

        foreach (Texture2D texture in generatedGuiTextures)
        {
            if (texture != null)
                Destroy(texture);
        }
        generatedGuiTextures.Clear();

        if (Instance == this)
            Instance = null;

        Time.timeScale = 1f;
    }
}
