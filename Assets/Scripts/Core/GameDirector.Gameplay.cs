using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using Mirror;

public partial class GameDirector
{
    public void RegisterMissileViewCamera(Camera camera)
    {
        if (camera == null)
            return;

        if (missileViewCamera != null && missileViewCamera != camera)
            missileViewCamera.enabled = false;

        missileViewCamera = camera;
        missileViewVisible = false;
        missileViewBlack = false;
        missileViewCamera.enabled = false;

        AudioListener listener = missileViewCamera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    public void BeginMissileViewImpact(Camera camera, float explosionViewDuration)
    {
        if (camera == null)
            return;

        camera.transform.SetParent(null, true);
        StartCoroutine(MissileViewImpactRoutine(camera, explosionViewDuration));
    }

    public void ClearMissileViewCamera(Camera camera)
    {
        if (missileViewCamera != camera)
            return;

        missileViewCamera = null;
        missileViewVisible = false;
        missileViewBlack = false;
    }

    private IEnumerator MissileViewImpactRoutine(
        Camera camera,
        float explosionViewDuration)
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, explosionViewDuration)
        );

        if (missileViewCamera == camera)
        {
            missileViewBlack = true;
            camera.enabled = false;
        }

        yield return new WaitForSecondsRealtime(1f);

        if (missileViewCamera == camera)
            ClearMissileViewCamera(camera);

        if (camera != null)
            Destroy(camera.gameObject);
    }

    // --------------------------------------------------
    // AI REGISTRATION AND DESTRUCTION
    // --------------------------------------------------

    public void RegisterAITank(TankHealth tank)
    {
        if (tank == null || tank.isPlayerTank)
            return;

        if (IsMultiplayerMode)
            return;

        int id = tank.gameObject.GetInstanceID();

        if (registeredTanks.Add(id))
        {
            totalAITanks++;

            Debug.Log("AI tanks registered: " + totalAITanks);
        }
    }

    private void RefreshAITankCounts()
    {
        registeredTanks.Clear();
        destroyedTankIds.Clear();
        destroyedTanks = 0;
        totalAITanks = 0;

        TankHealth[] allTanks = FindObjectsOfType<TankHealth>(true);

        foreach (TankHealth tank in allTanks)
        {
            if (tank == null || tank.isPlayerTank)
                continue;

            int id = tank.gameObject.GetInstanceID();

            if (registeredTanks.Add(id))
            {
                totalAITanks++;
            }
        }
    }

    public void NotifyDestroyed(TankHealth tank)
    {
        if (tank == null)
            return;

        if (tank.isPlayerTank)
        {
            if (IsMultiplayerMode)
                return;

            NotifyPlayerDestroyed();
            return;
        }

        if (IsMultiplayerMode)
            return;

        int id = tank.gameObject.GetInstanceID();

        if (!registeredTanks.Contains(id))
            return;

        if (!destroyedTankIds.Add(id))
            return;

        destroyedTanks = destroyedTankIds.Count;

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

    public void NotifyMultiplayerTankDestroyed(
        NetworkTankPlayer destroyedPlayer)
    {
        if (!IsMultiplayerMode || multiplayerMatchEnded ||
            destroyedPlayer == null || !NetworkServer.active)
        {
            return;
        }

        bool blueHasSurvivors = false;
        bool redHasSurvivors = false;
        foreach (NetworkIdentity networkIdentity in
                 NetworkServer.spawned.Values)
        {
            NetworkTankPlayer player =
                networkIdentity.GetComponent<NetworkTankPlayer>();
            TankHealth health =
                networkIdentity.GetComponent<TankHealth>();
            if (player == null || health == null ||
                health.IsDestroyed || health.CurrentHealth <= 0f)
            {
                continue;
            }

            if (player.Team == TankTeam.Blue)
                blueHasSurvivors = true;
            else if (player.Team == TankTeam.Red)
                redHasSurvivors = true;
        }

        TankTeam losingTeam = destroyedPlayer.Team;
        if ((losingTeam == TankTeam.Blue && blueHasSurvivors) ||
            (losingTeam == TankTeam.Red && redHasSurvivors) ||
            (!blueHasSurvivors && !redHasSurvivors))
        {
            return;
        }

        multiplayerMatchEnded = true;
        TankTeam winningTeam = losingTeam == TankTeam.Blue
            ? TankTeam.Red
            : TankTeam.Blue;
        destroyedPlayer.BroadcastMultiplayerMatchEndClientRpc(
            (byte)winningTeam
        );
        EndMultiplayerMatch(winningTeam);
    }

    public void NotifyOnlineTankDestroyed(
        NetworkTankPlayer attackingPlayer,
        NetworkTankPlayer destroyedPlayer)
    {
        if (!IsMultiplayerMode ||
            attackingPlayer == null || destroyedPlayer == null ||
            attackingPlayer.Team == destroyedPlayer.Team)
        {
            return;
        }

        int attackerConnectionId = -1;
        if (attackingPlayer.connectionToClient != null)
            attackerConnectionId = attackingPlayer.connectionToClient.connectionId;

        onlineMatchKills.TryGetValue(
            attackerConnectionId,
            out int currentKills
        );
        onlineMatchKills[attackerConnectionId] =
            currentKills + 1;

        if (!approvedClientWalletAddresses.TryGetValue(
                attackerConnectionId,
                out string playerAddress) ||
            string.IsNullOrWhiteSpace(playerAddress))
        {
            Debug.LogWarning(
                "Tank destruction reward was not submitted because the " +
                "attacking player has no verified wallet.",
                this
            );
            return;
        }

        if (!IsValidOnlineRewardAmount(onlineKillRewardWei))
        {
            Debug.LogError(
                "Tank destruction reward was not submitted because " +
                "ABRAMS_ONLINE_KILL_REWARD_WEI is missing or invalid.",
                this
            );
            return;
        }

        OnlineRequestData reward = new OnlineRequestData
        {
            playerAddress = playerAddress,
            amountWei = onlineKillRewardWei,
            reason = "Enemy tank destroyed",
            battleId = "0x" + Guid.NewGuid().ToString("N") +
                Guid.NewGuid().ToString("N")
        };
        StartCoroutine(SendOnlineRequest(
            "/api/server/rewards",
            "POST",
            JsonUtility.ToJson(reward),
            body => Debug.Log(
                "Tank destruction reward confirmed: " + body,
                this
            ),
            error => Debug.LogError(
                "Could not submit tank destruction reward: " + error,
                this
            ),
            onlineServiceUrl
        ));
    }

    private OnlineMatchResultsRequest CreateOnlineMatchResults()
    {
        List<OnlinePlayerMatchResult> results =
            new List<OnlinePlayerMatchResult>();
        foreach (KeyValuePair<int, string> player in onlineMatchUserIds)
        {
            if (!onlineMatchTeams.TryGetValue(
                    player.Key,
                    out TankTeam team) ||
                team == TankTeam.Unassigned)
            {
                continue;
            }

            onlineMatchKills.TryGetValue(player.Key, out int kills);
            results.Add(new OnlinePlayerMatchResult
            {
                userId = player.Value,
                team = (int)team,
                kills = kills
            });
        }

        return new OnlineMatchResultsRequest
        {
            winningTeam = (int)multiplayerWinningTeam,
            players = results.ToArray()
        };
    }

    private static bool IsValidOnlineRewardAmount(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 78 ||
            value[0] == '0')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character < '0' || character > '9')
                return false;
        }

        return System.Numerics.BigInteger.TryParse(
                value,
                out System.Numerics.BigInteger amount
            ) &&
            amount > System.Numerics.BigInteger.Zero &&
            amount < (System.Numerics.BigInteger.One << 256);
    }

    public void EndMultiplayerMatch(TankTeam winningTeam)
    {
        if (!IsMultiplayerMode || gameOver ||
            (winningTeam != TankTeam.Blue &&
             winningTeam != TankTeam.Red))
        {
            return;
        }

        gameOver = true;
        multiplayerWinningTeam = winningTeam;
        victory = localNetworkTankPlayer != null &&
            localNetworkTankPlayer.Team == winningTeam;
        if (localNetworkTankPlayer != null)
        {
            localPlayerEnemyTanksDestroyed =
                localNetworkTankPlayer.EnemyTanksDestroyed;
        }
        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;
        finishMenuVisible = false;
        SetCursor(true);
        StartCoroutine(ShowFinishMenuAfterDelay());
    }

    // --------------------------------------------------
    // GAME OVER
    // --------------------------------------------------

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
        finishMenuVisible = false;

        SetCursor(true);

        // Keep the world running briefly so the final explosion can play.
        StartCoroutine(ShowFinishMenuAfterDelay());
    }

    private IEnumerator ShowFinishMenuAfterDelay()
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, finishMenuDelay)
        );

        if (!gameOver)
            yield break;

        finishMenuVisible = true;
        UpdateSceneMenuCanvasVisibility();
        Time.timeScale = 0f;

        Debug.Log("Game paused after the finish delay.");
    }

    // --------------------------------------------------
    // GAME START
    // --------------------------------------------------

    private void StartGame(GameMode selectedMode)
    {
        currentGameMode = selectedMode;

        if (selectedMode == GameMode.SinglePlay)
        {
            RestoreScenePlayerAndAI();
        }

        totalAITanks = 0;
        destroyedTanks = 0;

        registeredTanks.Clear();
        destroyedTankIds.Clear();
        RefreshAITankCounts();

        gameOver = false;
        victory = false;
        localPlayerEnemyTanksDestroyed = 0;
        multiplayerWinningTeam = TankTeam.Unassigned;
        finishMenuVisible = false;
        multiplayerMatchEnded = false;

        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;

        // Enable AI for Single Play.
        // Disable AI for Multiplayer.
        bool enableAI = selectedMode == GameMode.SinglePlay;

        if (aiObjectsToDisableForMultiplayer != null)
        {
            foreach (GameObject aiObject in
                     aiObjectsToDisableForMultiplayer)
            {
                if (aiObject != null)
                    aiObject.SetActive(enableAI);
            }
        }

        Time.timeScale = 1f;
        SetCursor(false);

        Debug.Log("Game mode: " + selectedMode);
    }

    private void ShutdownServers()
    {
        if (onlineGameServer != null)
        {
            onlineGameServer.Shutdown();
            Destroy(onlineGameServer.gameObject);
            onlineGameServer = null;
        }
    }

    private OnlineGameServer CreateOnlineGameServer()
    {
        GameObject serverObject = new GameObject("Online Game Server");
        serverObject.transform.SetParent(transform, false);
        OnlineGameServer server =
            serverObject.AddComponent<OnlineGameServer>();
        server.OnClientConnected += OnClientConnected;
        server.OnClientDisconnected += OnClientDisconnected;
        return server;
    }

    private static bool IsHttpsEndpoint(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri endpoint) &&
            endpoint.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private void ApproveClient(NetworkConnection conn, bool autoApprove)
    {
        // Mirror connection approval handled inline via connection data.
        // Ticket validation happens in StartHostForOnlineRoom / ConnectToGameRoom.
    }

    private void StartHostForOnlineRoom(
        OnlineRoomInfo room)
    {
        if (onlineGameServer != null ||
            string.IsNullOrWhiteSpace(room.host) ||
            room.port < 1 ||
            multiplayerTankPrefab == null)
            return;

        PrepareMultiplayerScene(GameMode.Online);
        showMainMenu = false;
        showOnlineMenu = false;
        RemoveSceneAIObjects();
        onlineGameServer = CreateOnlineGameServer();
        onlineGameServer.Configure(multiplayerTankPrefab, 4, null);

        bool useEncryption = IsHttpsEndpoint(onlineServiceUrl);
        if (!onlineGameServer.StartHost(
                room.host,
                (ushort)room.port,
                useEncryption,
                room.host))
        {
            Debug.LogError(
                "Could not start the online host.", this);
            ShutdownServers();
            RestoreScenePlayerAndAI();
            showMainMenu = true;
            showOnlineMenu = true;
            return;
        }

        onlineStatusMessage = "Hosting " + room.name + "...";
    }

    private IEnumerator RemoveOnlineRoomMember(string userId)
    {
        yield break;
    }

    public void AssignIdentityToPlayer(NetworkTankPlayer player)
    {
        if (player == null || !NetworkServer.active)
            return;

        int connectionId = -1;
        if (player.connectionToClient != null)
            connectionId = player.connectionToClient.connectionId;

        TankTeam team;
        if (!approvedClientTeams.TryGetValue(
                connectionId, out team))
        {
            if (connectionId == LocalConnectionId)
                team = selectedTeam;
            else
            {
                Debug.LogError(
                    "No approved team was recorded for network client " +
                    connectionId + ".",
                    player);
                return;
            }
        }

        string playerName;
        if (!approvedClientNames.TryGetValue(
                connectionId, out playerName))
            playerName = "Player";

        player.SetServerIdentity(team, playerName);
    }

    private int LocalConnectionId =>
        NetworkClient.isConnected
            ? NetworkClient.connection.connectionId
            : -1;

    private string SanitizePlayerName(string playerName)
    {
        string trimmedName = string.IsNullOrWhiteSpace(playerName)
            ? "Player"
            : playerName.Trim();
        StringBuilder safeName = new StringBuilder(trimmedName.Length);
        foreach (char character in trimmedName)
        {
            if (char.IsControl(character))
                continue;

            safeName.Append(character);
        }

        string cleanedName = safeName.Length > 0
            ? safeName.ToString()
            : "Player";
        byte[] encodedName = Encoding.UTF8.GetBytes(cleanedName);
        int byteLength = Mathf.Min(encodedName.Length, 48);
        while (byteLength > 0)
        {
            string truncatedName = Encoding.UTF8.GetString(
                encodedName, 0, byteLength);
            if (Encoding.UTF8.GetByteCount(truncatedName) <= 48)
                return truncatedName;

            byteLength--;
        }

        return "Player";
    }

    private Vector3 GetRandomMultiplayerSpawnPosition()
    {
        Transform anchor = MultiplayerSpawnAnchor;
        Vector3 center = anchor != null ? anchor.position : Vector3.zero;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Terrain[] terrains = FindObjectsOfType<Terrain>();
            if (terrains.Length > 0)
                terrain = terrains[0];
        }

        if (TryGetFenceEnclosedBounds(out Bounds fencedArea))
        {
            float minimumX =
                fencedArea.min.x + MultiplayerSpawnAreaEdgePadding;
            float maximumX =
                fencedArea.max.x - MultiplayerSpawnAreaEdgePadding;
            float minimumZ =
                fencedArea.min.z + MultiplayerSpawnAreaEdgePadding;
            float maximumZ =
                fencedArea.max.z - MultiplayerSpawnAreaEdgePadding;

            if (minimumX < maximumX && minimumZ < maximumZ)
            {
                Vector3 lastPosition = new Vector3(
                    fencedArea.center.x, 0f, fencedArea.center.z);
                for (int attempt = 0; attempt < 160; attempt++)
                {
                    Vector3 candidate = new Vector3(
                        UnityEngine.Random.Range(minimumX, maximumX),
                        0f,
                        UnityEngine.Random.Range(minimumZ, maximumZ)
                    );
                    lastPosition = candidate;

                    if (!IsMultiplayerSpawnPositionClear(candidate) ||
                        IsTooCloseToConnectedPlayer(candidate))
                        continue;

                    return candidate;
                }

                Debug.LogWarning(
                    "Could not find a clear multiplayer spawn inside the " +
                    "fence bounds; using an in-bounds random position.",
                    this);
                return lastPosition;
            }

            Debug.LogWarning(
                "The fence bounds are too small for multiplayer spawning; " +
                "using nearby scene objects instead.",
                this);
        }

        List<Bounds> landmarks = new List<Bounds>();
        Collider[] colliders = FindObjectsOfType<Collider>();
        foreach (Collider worldCollider in colliders)
        {
            if (worldCollider == null || !worldCollider.enabled ||
                worldCollider.isTrigger ||
                worldCollider.GetComponentInParent<Terrain>() != null ||
                !IsStaticWorldObject(worldCollider.transform) ||
                IsPlayerOrDynamicObject(worldCollider.transform))
                continue;

            landmarks.Add(worldCollider.bounds);
        }

        if (landmarks.Count == 0)
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (Renderer worldRenderer in renderers)
            {
                if (worldRenderer == null || !worldRenderer.enabled ||
                    worldRenderer.GetComponentInParent<Terrain>() != null ||
                    !IsStaticWorldObject(worldRenderer.transform) ||
                    IsPlayerOrDynamicObject(worldRenderer.transform))
                    continue;

                landmarks.Add(worldRenderer.bounds);
            }
        }

        if (landmarks.Count == 0)
        {
            Debug.LogError(
                "No static scene objects were found for multiplayer spawn placement.",
                this);
            return new Vector3(center.x, 0f, center.z);
        }

        Vector3 terrainOrigin = terrain != null
            ? terrain.transform.position
            : Vector3.zero;
        Vector3 terrainSize = terrain != null && terrain.terrainData != null
            ? terrain.terrainData.size
            : Vector3.zero;
        Vector3 lastCandidate = new Vector3(center.x, 0f, center.z);
        bool foundCandidateOnTerrain = terrain == null;

        for (int attempt = 0; attempt < 120; attempt++)
        {
            Bounds landmark = landmarks[
                UnityEngine.Random.Range(0, landmarks.Count)
            ];
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;
            direction.Normalize();

            float landmarkExtent =
                Mathf.Abs(direction.x) * landmark.extents.x +
                Mathf.Abs(direction.y) * landmark.extents.z;
            float padding = UnityEngine.Random.Range(
                MultiplayerSpawnBuildingPadding,
                MultiplayerSpawnMaximumBuildingPadding
            );
            Vector3 candidate = new Vector3(
                landmark.center.x + direction.x * (landmarkExtent + padding),
                0f,
                landmark.center.z + direction.y * (landmarkExtent + padding)
            );
            lastCandidate = candidate;

            if (terrain != null && terrainSize != Vector3.zero &&
                (candidate.x < terrainOrigin.x ||
                 candidate.x > terrainOrigin.x + terrainSize.x ||
                 candidate.z < terrainOrigin.z ||
                 candidate.z > terrainOrigin.z + terrainSize.z))
                continue;

            lastCandidate = candidate;
            foundCandidateOnTerrain = true;

            if (!IsMultiplayerSpawnPositionClear(candidate))
                continue;

            if (!IsTooCloseToConnectedPlayer(candidate))
                return candidate;
        }

        Debug.LogWarning(
            "Could not find a clear multiplayer spawn near a scene object; " +
            "using the last in-bounds position sampled.",
            this);
        if (foundCandidateOnTerrain)
            return lastCandidate;

        Debug.LogError(
            "Scene objects suitable for spawning were outside the active terrain.",
            this);
        return new Vector3(center.x, 0f, center.z);
    }

    private bool TryGetFenceEnclosedBounds(out Bounds bounds)
    {
        bounds = default;
        GameObject fenceObject = GameObject.Find("Fence");
        if (fenceObject == null)
            return false;

        Renderer[] fenceRenderers =
            fenceObject.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer fenceRenderer in fenceRenderers)
        {
            if (fenceRenderer == null || !fenceRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = fenceRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(fenceRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] fenceColliders =
                fenceObject.GetComponentsInChildren<Collider>(true);
            foreach (Collider fenceCollider in fenceColliders)
            {
                if (fenceCollider == null || !fenceCollider.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = fenceCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(fenceCollider.bounds);
                }
            }
        }

        return hasBounds;
    }

    private bool IsTooCloseToConnectedPlayer(Vector3 candidate)
    {
        foreach (NetworkIdentity networkIdentity in
                 NetworkServer.spawned.Values)
        {
            NetworkTankPlayer player =
                networkIdentity.GetComponent<NetworkTankPlayer>();
            if (player == null)
                continue;

            Vector3 existingPosition =
                player.transform.position;
            existingPosition.y = 0f;

            if (Vector3.Distance(candidate, existingPosition) <
                MultiplayerSpawnMinDistance)
                return true;
        }

        return false;
    }

    private bool IsStaticWorldObject(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.gameObject.isStatic)
                return true;
        }

        return false;
    }

    private bool IsPlayerOrDynamicObject(Transform target)
    {
        if (target.GetComponentInParent<TankHealth>() != null ||
            target.GetComponentInParent<NetworkIdentity>() != null)
            return true;

        Rigidbody body = target.GetComponentInParent<Rigidbody>();
        return body != null && !body.isKinematic;
    }

    private bool IsMultiplayerSpawnPositionClear(Vector3 position)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            position + Vector3.up * 2f,
            3.5f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null ||
                overlap.GetComponentInParent<Terrain>() != null ||
                overlap.GetComponentInParent<TankHealth>() != null)
                continue;

            return false;
        }

        return true;
    }

    private void OnClientConnected(int connectionId)
    {
        pendingOnlineClients.Remove(connectionId);

        if (NetworkServer.active)
        {
            if (approvedClientSpawnPositions.TryGetValue(
                    connectionId, out Vector3 spawnPosition))
            {
                StartCoroutine(
                    ApplyApprovedSpawnPosition(connectionId, spawnPosition));
            }
        }

        if (connectionId != LocalConnectionId)
            return;

        RemoveSceneAIObjects();

        showMainMenu = false;
        showPauseMenu = false;
        showOnlineMenu = false;
        onlineStatusMessage = "";
        Time.timeScale = 1f;
        SetCursor(false);
    }

    private IEnumerator ApplyApprovedSpawnPosition(
        int connectionId,
        Vector3 spawnPosition)
    {
        for (int frame = 0; frame < 120; frame++)
        {
            if (!NetworkServer.active)
                yield break;

            if (NetworkServer.spawned.TryGetValue(
                    connectionId, out NetworkIdentity networkIdentity) &&
                networkIdentity != null)
            {
                break;
            }

            yield return null;
        }

        if (!NetworkServer.spawned.TryGetValue(
                connectionId, out NetworkIdentity identity) ||
            identity == null)
        {
            Debug.LogError(
                "Could not apply the approved spawn position for client " +
                connectionId + ".",
                this);
            yield break;
        }

        Quaternion spawnRotation =
            Quaternion.Euler(
                0f,
                UnityEngine.Random.Range(0f, 360f),
                0f
            );
        Transform playerTransform = identity.transform;
        playerTransform.SetPositionAndRotation(
            spawnPosition, spawnRotation);

        Rigidbody playerRigidbody =
            playerTransform.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.position = spawnPosition;
            playerRigidbody.rotation = spawnRotation;
        }

        NetworkTankPlayer player =
            playerTransform.GetComponent<NetworkTankPlayer>();
        if (player == null)
        {
            Debug.LogError(
                "The multiplayer player prefab is missing " +
                "NetworkTankPlayer.",
                playerTransform);
            yield break;
        }

        if (approvedClientTeams.TryGetValue(connectionId, out TankTeam team) &&
            approvedClientNames.TryGetValue(connectionId, out string playerName))
        {
            player.SetServerIdentity(team, playerName);
        }

        if (connectionId != LocalConnectionId)
        {
            player.RpcSetSpawnPosition(
                spawnPosition,
                spawnRotation
            );
        }
    }

    private void OnClientDisconnected(int connectionId)
    {
        bool wasOnlineClient = currentGameMode == GameMode.Online;
        approvedClientTeams.Remove(connectionId);
        approvedClientNames.Remove(connectionId);
        approvedClientSpawnPositions.Remove(connectionId);
        approvedClientWalletAddresses.Remove(connectionId);

        if (approvedClientUserIds.TryGetValue(connectionId, out string userId))
        {
            approvedClientUserIds.Remove(connectionId);
            StartCoroutine(RemoveOnlineRoomMember(userId));
        }

        if (connectionId != LocalConnectionId)
            return;

        pendingOnlineClients.Remove(connectionId);

        string failureMessage =
            "Connection lost. Select a room to try again.";
        Debug.LogWarning(failureMessage, this);

        showMainMenu = true;
        showOnlineMenu = wasOnlineClient;
        onlineStatusMessage = failureMessage;
        Time.timeScale = 0f;
        SetCursor(true);
        RestoreScenePlayerAndAI();
    }

    private void PrepareMultiplayerScene(
        GameMode selectedMode = GameMode.Online)
    {
        currentGameMode = selectedMode;
        approvedClientTeams.Clear();
        approvedClientNames.Clear();
        approvedClientSpawnPositions.Clear();
        approvedClientUserIds.Clear();
        totalAITanks = 0;
        destroyedTanks = 0;
        localPlayerEnemyTanksDestroyed = 0;
        gameOver = false;
        victory = false;
        finishMenuVisible = false;
        multiplayerWinningTeam = TankTeam.Unassigned;
        multiplayerMatchEnded = false;

        if (playerHealth != null)
            playerHealth.gameObject.SetActive(false);

        showMainMenu = true;
        Time.timeScale = 1f;
        SetCursor(true);
    }

    private void RemoveSceneAIObjects()
    {
        AIRandomSpawner[] tankSpawners =
            FindObjectsOfType<AIRandomSpawner>(true);
        foreach (AIRandomSpawner spawner in tankSpawners)
            spawner.enabled = false;

        AIDroneSpawner[] droneSpawners =
            FindObjectsOfType<AIDroneSpawner>(true);
        foreach (AIDroneSpawner spawner in droneSpawners)
            spawner.enabled = false;

        TankHealth[] tanks = FindObjectsOfType<TankHealth>(true);
        foreach (TankHealth tank in tanks)
        {
            if (tank != null && !tank.isPlayerTank)
                Destroy(tank.gameObject);
        }

        AIDroneController[] drones =
            FindObjectsOfType<AIDroneController>(true);
        foreach (AIDroneController drone in drones)
        {
            if (drone != null)
                Destroy(drone.gameObject);
        }
    }

    private void RestoreScenePlayerAndAI()
    {
        currentGameMode = GameMode.SinglePlay;

        if (scenePlayerHealth != null)
            playerHealth = scenePlayerHealth;

        if (scenePlayerMovement != null)
            playerMovement = scenePlayerMovement;

        if (playerHealth != null)
        {
            playerHealth.gameObject.SetActive(true);

            TankDronecontroll sceneDroneController =
                playerHealth.GetComponent<TankDronecontroll>();
            if (sceneDroneController != null)
                sceneDroneController.Initialize(true);
        }

        AIRandomSpawner[] tankSpawners =
            FindObjectsOfType<AIRandomSpawner>(true);
        foreach (AIRandomSpawner spawner in tankSpawners)
            spawner.enabled = true;

        AIDroneSpawner[] droneSpawners =
            FindObjectsOfType<AIDroneSpawner>(true);
        foreach (AIDroneSpawner spawner in droneSpawners)
            spawner.enabled = true;

        TankHealth[] tanks = FindObjectsOfType<TankHealth>(true);
        bool foundAITank = false;
        foreach (TankHealth tank in tanks)
        {
            if (tank != null && !tank.isPlayerTank)
            {
                tank.gameObject.SetActive(true);
                foundAITank = true;
            }
        }

        if (!foundAITank)
        {
            AIRandomSpawner tankSpawner =
                FindObjectOfType<AIRandomSpawner>();

            if (tankSpawner != null)
                tankSpawner.SpawnTanks();
        }

        AIDroneController[] drones =
            FindObjectsOfType<AIDroneController>(true);
        foreach (AIDroneController drone in drones)
        {
            if (drone != null)
                drone.gameObject.SetActive(true);
        }

        RestoreSceneCameraFollow();
    }

    private void RestoreSceneCameraFollow()
    {
        if (sceneCameraFollow == null)
            sceneCameraFollow = FindObjectOfType<TankCameraFollow>(true);

        if (sceneCameraFollow == null || scenePlayerHealth == null)
            return;

        Transform playerTransform = scenePlayerHealth.transform;
        sceneCameraFollow.target = playerTransform;

        TankTurretMove turretControls =
            playerTransform.GetComponent<TankTurretMove>();
        sceneCameraFollow.rotationTarget =
            turretControls != null ? turretControls.turret : null;
    }

    // --------------------------------------------------
    // PAUSE AND RESUME
    // --------------------------------------------------

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

    // --------------------------------------------------
    // BUTTON ACTIONS
    // --------------------------------------------------

    private void PlayAgain()
    {
        if (IsMultiplayerMode)
        {
            ShutdownServers();

            RestoreScenePlayerAndAI();
            localNetworkTankPlayer = null;
            localPlayerEnemyTanksDestroyed = 0;
            totalAITanks = 0;
            destroyedTanks = 0;
            registeredTanks.Clear();
            destroyedTankIds.Clear();
            gameOver = false;
            victory = false;
            multiplayerWinningTeam = TankTeam.Unassigned;
            finishMenuVisible = false;
            multiplayerMatchEnded = false;
            showPauseMenu = false;
            showSettings = false;
            showControlSettings = false;
            showMainMenu = true;
            showOnlineMenu = true;
            onlineStatusMessage = "Choose an online room.";
            StartCoroutine(RefreshOnlineRooms());
            Time.timeScale = 0f;
            SetCursor(true);
            UpdateSceneMenuCanvasVisibility();
            return;
        }

        startSinglePlayOnSceneLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void ReturnToMainMenu()
    {
        startSinglePlayOnSceneLoad = false;
        Time.timeScale = 1f;
        ShutdownServers();
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        ShutdownServers();
        Application.Quit();

        Debug.Log("Exit requested.");
    }

    // --------------------------------------------------
    // GUI
    // --------------------------------------------------

}
