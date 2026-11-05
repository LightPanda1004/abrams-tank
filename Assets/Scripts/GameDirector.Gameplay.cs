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
        NetworkManager manager = NetworkManager.Singleton;
        if (!IsMultiplayerMode || multiplayerMatchEnded ||
            destroyedPlayer == null || manager == null ||
            !manager.IsServer || !manager.IsListening)
        {
            return;
        }

        bool blueHasSurvivors = false;
        bool redHasSurvivors = false;
        foreach (NetworkObject networkObject in
                 manager.SpawnManager.SpawnedObjects.Values)
        {
            NetworkTankPlayer player =
                networkObject.GetComponent<NetworkTankPlayer>();
            TankHealth health =
                networkObject.GetComponent<TankHealth>();
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
        destroyedPlayer.BroadcastMultiplayerMatchEndClientRpc(
            (byte)(losingTeam == TankTeam.Blue
                ? TankTeam.Red
                : TankTeam.Blue)
        );
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

    private void CreateLanRoom()
    {
        if (selectedTeam == TankTeam.Unassigned)
        {
            roomStatusMessage = "Select a team before creating a room.";
            return;
        }

        if (multiplayerTankPrefab == null)
        {
            roomStatusMessage = "Assign the multiplayer tank prefab on GameDirector.";
            return;
        }

        PrepareMultiplayerScene();
        lanNetworkManager = CreateLanNetworkManager();

        UnityTransport transport =
            lanNetworkManager.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", LanGamePort, "0.0.0.0");
        lanNetworkManager.NetworkConfig.ConnectionData =
            CreateConnectionPayload();

        if (!lanNetworkManager.StartHost())
        {
            roomStatusMessage = "Could not start the LAN host.";
            CleanupLanNetworkManager();
            RestoreScenePlayerAndAI();
            return;
        }

        lanRoomDiscovery.StartAdvertising(
            string.IsNullOrWhiteSpace(roomNameInput)
                ? "Abrams Room"
                : roomNameInput.Trim(),
            LanGamePort,
            LanMaxPlayers,
            () => lanNetworkManager != null && lanNetworkManager.IsServer
                ? lanNetworkManager.ConnectedClients.Count
                : 1
        );

        roomStatusMessage = "Room created. Waiting for players...";
    }

    private void JoinLanRoom(LanRoomInfo room)
    {
        if (selectedTeam == TankTeam.Unassigned)
        {
            roomStatusMessage = "Select a team before joining a room.";
            return;
        }

        if (room == null || multiplayerTankPrefab == null)
        {
            roomStatusMessage = "This room cannot be joined.";
            return;
        }

        if (room.playerCount >= room.maxPlayers)
        {
            roomStatusMessage = "That room is full.";
            return;
        }

        PrepareMultiplayerScene();
        lanNetworkManager = CreateLanNetworkManager();

        UnityTransport transport =
            lanNetworkManager.GetComponent<UnityTransport>();
        transport.SetConnectionData(room.address, (ushort)room.gamePort);
        lanNetworkManager.NetworkConfig.ConnectionData =
            CreateConnectionPayload();

        if (!lanNetworkManager.StartClient())
        {
            roomStatusMessage = "Could not connect to that room.";
            CleanupLanNetworkManager();
            RestoreScenePlayerAndAI();
            return;
        }

        roomStatusMessage = "Connecting to " + room.roomName + "...";
    }

    private void CleanupLanNetworkManager()
    {
        if (lanNetworkManager == null)
            return;

        NetworkManager manager = lanNetworkManager;
        lanNetworkManager = null;
        manager.OnClientConnectedCallback -= OnLanClientConnected;
        manager.OnClientDisconnectCallback -= OnLanClientDisconnected;

        if (manager.IsListening)
            manager.Shutdown();

        Destroy(manager.gameObject);
    }

    private NetworkManager CreateLanNetworkManager()
    {
        GameObject managerObject = new GameObject("LAN Network Manager");
        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig();
        UnityTransport transport = managerObject.AddComponent<UnityTransport>();

        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.PlayerPrefab = multiplayerTankPrefab;
        manager.NetworkConfig.TickRate = 30;
        manager.NetworkConfig.EnableSceneManagement = true;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = ApproveLanClient;
        manager.AddNetworkPrefab(multiplayerTankPrefab);

        TankFire prefabTankFire = multiplayerTankPrefab.GetComponent<TankFire>();
        if (prefabTankFire != null && prefabTankFire.shellPrefab != null)
        {
            manager.AddNetworkPrefab(prefabTankFire.shellPrefab);
            // These hashes were reported for shells during late-join synchronization.
            AddLegacyShellPrefabHash(
                manager,
                prefabTankFire.shellPrefab,
                LegacyShellPrefabHashBeforeLastUpdate
            );
            AddLegacyShellPrefabHash(
                manager,
                prefabTankFire.shellPrefab,
                LegacyShellPrefabHashBeforePrefabRefresh
            );
        }

        manager.OnClientConnectedCallback += OnLanClientConnected;
        manager.OnClientDisconnectCallback += OnLanClientDisconnected;
        return manager;
    }

    private static void AddLegacyShellPrefabHash(
        NetworkManager manager,
        GameObject shellPrefab,
        uint legacyHash)
    {
        NetworkPrefab legacyShellPrefab = new NetworkPrefab
        {
            Override = NetworkPrefabOverride.Hash,
            SourceHashToOverride = legacyHash,
            Prefab = shellPrefab,
            OverridingTargetPrefab = shellPrefab
        };

        if (!manager.NetworkConfig.Prefabs.Add(legacyShellPrefab))
        {
            Debug.LogError(
                $"Could not register legacy shell prefab hash {legacyHash}."
            );
        }
    }

    private void ApproveLanClient(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        bool hasCapacity = lanNetworkManager == null ||
            lanNetworkManager.ConnectedClients.Count < LanMaxPlayers;
        bool isLocalHost = lanNetworkManager != null &&
            lanNetworkManager.IsHost &&
            request.ClientNetworkId == lanNetworkManager.LocalClientId;
        bool hasPayloadTeam = request.Payload != null &&
            request.Payload.Length >= 1 &&
            (request.Payload[0] == (byte)TankTeam.Blue ||
             request.Payload[0] == (byte)TankTeam.Red);
        TankTeam approvedTeam = hasPayloadTeam
            ? (TankTeam)request.Payload[0]
            : isLocalHost
                ? selectedTeam
                : TankTeam.Unassigned;
        bool hasValidTeam = approvedTeam == TankTeam.Blue ||
            approvedTeam == TankTeam.Red;

        response.Approved = hasCapacity && hasValidTeam;
        response.CreatePlayerObject = response.Approved;

        if (!response.Approved)
        {
            response.Reason = !hasCapacity
                ? "The room is full."
                : "Select a team before joining.";
            return;
        }

        approvedClientTeams[request.ClientNetworkId] =
            approvedTeam;
        approvedClientNames[request.ClientNetworkId] =
            hasPayloadTeam && request.Payload.Length > 1
                ? SanitizePlayerName(Encoding.UTF8.GetString(
                    request.Payload, 1, request.Payload.Length - 1))
                : SanitizePlayerName(playerNameInput);
        Vector3 spawnPosition = GetRandomMultiplayerSpawnPosition();
        approvedClientSpawnPositions[request.ClientNetworkId] =
            spawnPosition;
        response.Position = spawnPosition;
        response.Rotation = Quaternion.Euler(
            0f, UnityEngine.Random.Range(0f, 360f), 0f);
    }

    public void AssignIdentityToPlayer(NetworkTankPlayer player)
    {
        if (player == null || lanNetworkManager == null ||
            !lanNetworkManager.IsServer)
            return;

        TankTeam team;
        if (!approvedClientTeams.TryGetValue(
                player.OwnerClientId, out team))
        {
            if (player.OwnerClientId == lanNetworkManager.LocalClientId)
                team = selectedTeam;
            else
            {
                Debug.LogError(
                    "No approved team was recorded for network client " +
                    player.OwnerClientId + ".",
                    player);
                return;
            }
        }

        string playerName;
        if (!approvedClientNames.TryGetValue(
                player.OwnerClientId, out playerName))
            playerName = "Player";

        player.SetServerIdentity(team, playerName);
    }

    private byte[] CreateConnectionPayload()
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(
            SanitizePlayerName(playerNameInput));
        byte[] payload = new byte[nameBytes.Length + 1];
        payload[0] = (byte)selectedTeam;
        System.Buffer.BlockCopy(nameBytes, 0, payload, 1, nameBytes.Length);
        return payload;
    }

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
        if (lanNetworkManager == null)
            return false;

        foreach (NetworkClient client in
                 lanNetworkManager.ConnectedClients.Values)
        {
            if (client.PlayerObject == null)
                continue;

            Vector3 existingPosition =
                client.PlayerObject.transform.position;
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
            target.GetComponentInParent<NetworkObject>() != null)
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

    private void OnLanClientConnected(ulong clientId)
    {
        if (lanNetworkManager == null)
            return;

        if (lanNetworkManager.IsServer)
        {
            if (!approvedClientSpawnPositions.ContainsKey(clientId) &&
                lanNetworkManager.IsHost &&
                clientId == lanNetworkManager.LocalClientId)
            {
                approvedClientTeams[clientId] = selectedTeam;
                approvedClientNames[clientId] =
                    SanitizePlayerName(playerNameInput);
                approvedClientSpawnPositions[clientId] =
                    GetRandomMultiplayerSpawnPosition();
            }

            if (approvedClientSpawnPositions.TryGetValue(
                    clientId, out Vector3 spawnPosition))
            {
                StartCoroutine(
                    ApplyApprovedSpawnPosition(clientId, spawnPosition));
            }
        }

        if (clientId != lanNetworkManager.LocalClientId)
            return;

        RemoveSceneAIObjects();

        showMainMenu = false;
        showPauseMenu = false;
        showMultiplayerMenu = false;
        showJoinRoomMenu = false;
        roomStatusMessage = "";
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StopSearching();
        Time.timeScale = 1f;
        SetCursor(false);
    }

    private IEnumerator ApplyApprovedSpawnPosition(
        ulong clientId,
        Vector3 spawnPosition)
    {
        NetworkClient client = null;
        for (int frame = 0; frame < 120; frame++)
        {
            if (lanNetworkManager == null ||
                !lanNetworkManager.IsListening)
                yield break;

            if (lanNetworkManager.ConnectedClients.TryGetValue(
                    clientId, out client) &&
                client.PlayerObject != null &&
                client.PlayerObject.IsSpawned)
                break;

            yield return null;
        }

        if (client == null || client.PlayerObject == null ||
            !client.PlayerObject.IsSpawned)
        {
            Debug.LogError(
                "Could not apply the approved spawn position for client " +
                clientId + ".",
                this);
            yield break;
        }

        Quaternion spawnRotation =
            Quaternion.Euler(
                0f,
                UnityEngine.Random.Range(0f, 360f),
                0f
            );
        Transform playerTransform = client.PlayerObject.transform;
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

        if (approvedClientTeams.TryGetValue(clientId, out TankTeam team) &&
            approvedClientNames.TryGetValue(clientId, out string playerName))
        {
            player.SetServerIdentity(team, playerName);
        }

        if (clientId != lanNetworkManager.LocalClientId)
        {
            player.SetSpawnPositionClientRpc(
                spawnPosition,
                spawnRotation,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    }
                });
        }
    }

    private void OnLanClientDisconnected(ulong clientId)
    {
        approvedClientTeams.Remove(clientId);
        approvedClientNames.Remove(clientId);
        approvedClientSpawnPositions.Remove(clientId);

        if (lanNetworkManager == null ||
            clientId != lanNetworkManager.LocalClientId ||
            lanNetworkManager.IsHost)
            return;

        NetworkManager disconnectedManager = lanNetworkManager;
        string disconnectReason = disconnectedManager.DisconnectReason;
        lanNetworkManager = null;
        disconnectedManager.OnClientConnectedCallback -= OnLanClientConnected;
        disconnectedManager.OnClientDisconnectCallback -= OnLanClientDisconnected;
        disconnectedManager.Shutdown();
        Destroy(disconnectedManager.gameObject);

        string failureMessage = string.IsNullOrWhiteSpace(disconnectReason)
            ? "Connection lost. Select a room to try again."
            : "Connection failed: " + disconnectReason;
        Debug.LogWarning(failureMessage, this);

        showMainMenu = true;
        showMultiplayerMenu = true;
        showJoinRoomMenu = true;
        roomStatusMessage = failureMessage;
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StartSearching();
        Time.timeScale = 0f;
        SetCursor(true);
        RestoreScenePlayerAndAI();
    }

    private void PrepareMultiplayerScene()
    {
        currentGameMode = GameMode.Multiplayer;
        approvedClientTeams.Clear();
        approvedClientNames.Clear();
        approvedClientSpawnPositions.Clear();
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
            playerHealth.gameObject.SetActive(true);

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
            if (lanRoomDiscovery != null)
                lanRoomDiscovery.StopAdvertising();
            CleanupLanNetworkManager();

            RestoreScenePlayerAndAI();
            currentGameMode = GameMode.Multiplayer;
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
            OpenMultiplayerMenu();
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
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StopAdvertising();
        CleanupLanNetworkManager();
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        if (lanRoomDiscovery != null)
            lanRoomDiscovery.StopAdvertising();
        CleanupLanNetworkManager();
        Application.Quit();

        Debug.Log("Exit requested.");
    }

    // --------------------------------------------------
    // GUI
    // --------------------------------------------------

}
