using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public partial class GameDirector
{
    private void UpdateGameplayStatusBarsVisibility()
    {
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

    private void SetGameplayStatusBarsVisible(bool visible)
    {
        TankHealthBar[] healthBars =
            FindObjectsOfType<TankHealthBar>(true);
        foreach (TankHealthBar healthBar in healthBars)
        {
            if (healthBar != null)
                healthBar.gameObject.SetActive(visible);
        }

        if (playerTankFire != null)
            playerTankFire.SetStatusBarVisible(visible);
    }

    private void OpenOnlineMenu()
    {
        showOnlineMenu = true;
        onlineStatusMessage = "";
        if (!string.IsNullOrEmpty(onlineAuthToken))
            StartCoroutine(RefreshOnlineProfile());
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
                onlineWalletAddress = result.user.walletAddress ?? "";
                onlinePasswordInput = "";
                onlineStatusMessage = "Signed in as " + result.user.username;
                StartCoroutine(RefreshOnlineProfile());
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

    private IEnumerator SendOnlineRequest(
        string path,
        string method,
        string json,
        Action<string> onSuccess,
        Action<string> onFailure,
        string baseUrl = null)
    {
        string serviceUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? onlineServiceUrl
            : baseUrl;
        using (UnityWebRequest request = new UnityWebRequest(
            serviceUrl.TrimEnd('/') + path,
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

            if (!string.IsNullOrEmpty(onlineAuthToken))
            {
                request.SetRequestHeader(
                    "Authorization",
                    "Bearer " + onlineAuthToken
                );
            }

            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
                yield break;
            }

            string message = "Online service request failed (" +
                (long)request.responseCode + ").";
            if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
            {
                try
                {
                    OnlineErrorResponse serviceError =
                        JsonUtility.FromJson<OnlineErrorResponse>(
                            request.downloadHandler.text
                        );
                    if (serviceError != null &&
                        !string.IsNullOrWhiteSpace(serviceError.error))
                        message = serviceError.error;
                    if (serviceError != null &&
                        !string.IsNullOrWhiteSpace(serviceError.requestId))
                    {
                        message += " (request ID: " +
                            serviceError.requestId + ").";
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

    private void Update()
    {
        UpdateGameplayStatusBarsVisibility();
        SyncConnectedWallet();
        UpdateOnlineRoom();

        if (missileViewCamera != null &&
            Input.GetKeyDown(KeyCode.C))
        {
            missileViewVisible = !missileViewVisible;
            missileViewCamera.enabled =
                missileViewVisible && !missileViewBlack;
        }

        if (onlineRoomActive)
            return;

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
