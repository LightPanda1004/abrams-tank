using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public partial class GameDirector
{
    [Serializable]
    private sealed class OnlineRoomsResponse
    {
        public OnlineRoomInfo[] rooms = Array.Empty<OnlineRoomInfo>();
    }

    [Serializable]
    private sealed class OnlineRoomResponse
    {
        public OnlineRoomInfo room;
    }

    [Serializable]
    private sealed class OnlineGameAuthRequest
    {
        public string type = "authenticate";
        public string ticket = "";
    }

    [Serializable]
    private sealed class OnlineRoomInfo
    {
        public string id = "";
        public string name = "";
        public string status = "";
        public string wsUrl = "";
        public string ticket = "";
        public string host = "";
        public int port;
        public int maxPlayers;
        public int playerCount;
    }

    [Serializable]
    private sealed class OnlineGameSocketMessage
    {
        public string type = "";
        public string message = "";
        public string reason = "";
        public string roomId = "";
        public string userId = "";
        public string playerName = "";
        public int team;
        public OnlineGameRoomState snapshot;
        public OnlineGameRoomState state;
        public OnlineGamePlayer[] players = Array.Empty<OnlineGamePlayer>();
        public OnlineGameProjectile[] projectiles =
            Array.Empty<OnlineGameProjectile>();
        public OnlineGameEvent[] events = Array.Empty<OnlineGameEvent>();
    }

    [Serializable]
    private sealed class OnlineGameRoomState
    {
        public string type = "";
        public string roomId = "";
        public OnlineGamePlayer[] players = Array.Empty<OnlineGamePlayer>();
        public OnlineGameProjectile[] projectiles =
            Array.Empty<OnlineGameProjectile>();
        public OnlineGameEvent[] events = Array.Empty<OnlineGameEvent>();
    }

    [Serializable]
    private sealed class OnlineGamePlayer
    {
        public string userId = "";
        public string playerName = "";
        public int team;
        public float x;
        public float z;
        public float heading;
        public float aimHeading;
        public float aimPitch;
        public float speed;
        public int health;
        public int kills;
        public int deaths;
        public bool alive;
    }

    [Serializable]
    private sealed class OnlineGameEvent
    {
        public string type = "";
        public string userId = "";
        public string attackerId = "";
        public int health;
        public int projectileId;
        public float cooldown;
    }

    [Serializable]
    private sealed class OnlineGameProjectile
    {
        public int id;
        public string ownerId = "";
        public float x;
        public float z;
        public float heading;
        public float pitch;
    }

    [Serializable]
    private sealed class OnlineGameSocketInput
    {
        public string type = "input";
        public float throttle;
        public float steering;
        public bool fire;
        public float aimHeading;
        public float aimPitch;
    }

    private sealed class OnlineTransformTarget
    {
        public Vector3 position;
        public Quaternion rotation;
        public Quaternion turretRotation;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Quaternion startTurretRotation;
        public float cannonPitch;
        public float startCannonPitch;
        public float startedAt;
        public float duration;
        public bool initialized;
        public bool wasAlive;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AbramsGameSocketConnect(
        string target,
        string url
    );

    [DllImport("__Internal")]
    private static extern void AbramsGameSocketSend(
        string target,
        string message
    );

    [DllImport("__Internal")]
    private static extern void AbramsGameSocketClose(string target);
#endif

    private readonly List<OnlineRoomInfo> availableOnlineRooms =
        new List<OnlineRoomInfo>();
    private readonly Dictionary<string, GameObject> onlineRemotePlayers =
        new Dictionary<string, GameObject>();
    private readonly Dictionary<string, OnlineTransformTarget>
        onlineRemoteTargets =
            new Dictionary<string, OnlineTransformTarget>();
    private readonly Dictionary<int, GameObject> onlineProjectiles =
        new Dictionary<int, GameObject>();
    private readonly OnlineTransformTarget onlineLocalTarget =
        new OnlineTransformTarget();
    private OnlineGamePlayer[] currentOnlinePlayers =
        Array.Empty<OnlineGamePlayer>();
    private Collider[] onlinePlayerColliders = Array.Empty<Collider>();
    private bool[] onlinePlayerColliderStates = Array.Empty<bool>();
    private Renderer[] onlinePlayerRenderers = Array.Empty<Renderer>();
    private bool[] onlinePlayerRendererStates = Array.Empty<bool>();
    private bool onlinePlayerMovementWasEnabled;
    private bool onlinePlayerBodyWasKinematic;
    private RigidbodyInterpolation onlinePlayerBodyInterpolation;
    private bool onlinePlayerTankFireWasEnabled;
    private float onlineOriginalPlayerHealth;
    private bool onlineMissileLauncherWasEnabled;
    private bool showRoomMenu;
    private bool onlineRoomActive;
    private bool onlineSocketAuthenticated;
    private bool onlineWorldOffsetInitialized;
    private Vector2 onlineRoomsScroll;
    private string onlineRoomNameInput = "Tank Battle";
    private string selectedOnlineTeam = "blue";
    private string pendingOnlineGameTicket = "";
    private string onlineGameRoomId = "";
    private string onlineGameUserId = "";
    private float nextOnlineInputTime;
    private bool pendingOnlineFireInput;
    private Vector3 onlineWorldOffset;
    private Vector3 onlineOriginalPlayerPosition;
    private Quaternion onlineOriginalPlayerRotation;
    private bool onlineOriginalPlayerTransformSaved;

    private void OpenOnlineRooms()
    {
        showOnlineMenu = false;
        showEconomyMenu = false;
        if (string.IsNullOrEmpty(onlineAuthToken))
        {
            showOnlineMenu = true;
            onlineStatusMessage = "Sign in before joining an online room.";
            return;
        }

        showRoomMenu = true;
        showMainMenu = true;
        showPauseMenu = false;
        Time.timeScale = 0f;
        SetCursor(true);
        RefreshOnlineRooms();
    }

    private void RefreshOnlineRooms()
    {
        if (onlineRequestInProgress || string.IsNullOrEmpty(onlineAuthToken))
            return;

        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            "/api/rooms",
            "GET",
            "",
            body =>
            {
                onlineRequestInProgress = false;
                OnlineRoomsResponse result =
                    JsonUtility.FromJson<OnlineRoomsResponse>(body);
                if (result == null || result.rooms == null)
                {
                    onlineStatusMessage =
                        "The server returned an invalid room list.";
                    return;
                }

                availableOnlineRooms.Clear();
                availableOnlineRooms.AddRange(result.rooms);
                onlineStatusMessage = availableOnlineRooms.Count == 0
                    ? "No active rooms. Create one to start playing."
                    : "Choose a room or create a new one.";
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private void CreateOnlineRoom()
    {
        if (onlineRequestInProgress || string.IsNullOrEmpty(onlineAuthToken))
            return;

        string name = onlineRoomNameInput.Trim();
        if (string.IsNullOrEmpty(name))
        {
            onlineStatusMessage = "Enter a room name.";
            return;
        }

        OnlineRequestData data = new OnlineRequestData
        {
            name = name,
            playerName = onlineUsernameInput,
            team = selectedOnlineTeam
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
                    onlineStatusMessage =
                        "The server returned an invalid room response.";
                    return;
                }
                ConnectToOnlineRoom(result.room);
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
            }
        ));
    }

    private void JoinOnlineRoom(string roomId)
    {
        if (onlineRequestInProgress || string.IsNullOrEmpty(onlineAuthToken))
            return;

        OnlineRequestData data = new OnlineRequestData
        {
            playerName = onlineUsernameInput,
            team = selectedOnlineTeam
        };
        onlineRequestInProgress = true;
        StartCoroutine(SendOnlineRequest(
            "/api/rooms/" + Uri.EscapeDataString(roomId) + "/join",
            "POST",
            JsonUtility.ToJson(data),
            body =>
            {
                onlineRequestInProgress = false;
                OnlineRoomResponse result =
                    JsonUtility.FromJson<OnlineRoomResponse>(body);
                if (result == null || result.room == null)
                {
                    onlineStatusMessage =
                        "The server returned an invalid room response.";
                    return;
                }
                ConnectToOnlineRoom(result.room);
            },
            error =>
            {
                onlineRequestInProgress = false;
                onlineStatusMessage = error;
                RefreshOnlineRooms();
            }
        ));
    }

    private void ConnectToOnlineRoom(OnlineRoomInfo room)
    {
        if (room == null || string.IsNullOrWhiteSpace(room.ticket) ||
            string.IsNullOrWhiteSpace(room.wsUrl) ||
            !Uri.TryCreate(room.wsUrl, UriKind.Absolute, out Uri socketUri) ||
            (socketUri.Scheme != "ws" && socketUri.Scheme != "wss"))
        {
            onlineStatusMessage =
                "The room server returned an invalid WebSocket ticket or URL.";
            return;
        }
        if (socketUri.IsLoopback &&
            Uri.TryCreate(onlineServiceUrl, UriKind.Absolute, out Uri apiUri) &&
            !apiUri.IsLoopback)
        {
            onlineStatusMessage =
                "The room server advertised a localhost WebSocket URL. On the " +
                "VPS, set ABRAMS_ONLINE_WS_PUBLIC_URL to " +
                "ws://62.210.246.125:7777/game, open TCP port 7777, then restart " +
                "the WebSocket gateway and create a new room.";
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        bool pageUsesTls = Application.absoluteURL.StartsWith(
            "https://",
            StringComparison.OrdinalIgnoreCase
        );
        if (pageUsesTls && socketUri.Scheme != "wss")
        {
            onlineStatusMessage =
                "Secure browser builds require a WSS game server.";
            return;
        }

        pendingOnlineGameTicket = room.ticket;
        onlineGameRoomId = room.id;
        onlineSocketAuthenticated = false;
        onlineWorldOffsetInitialized = false;
        onlineStatusMessage = "Connecting to " + room.name + "...";
        AbramsGameSocketConnect(gameObject.name, room.wsUrl);
#else
        onlineStatusMessage =
            "Online multiplayer requires a WebGL browser build.";
#endif
    }

    public void OnGameSocketMessage(string json)
    {
        OnlineGameSocketMessage message;
        try
        {
            message = JsonUtility.FromJson<OnlineGameSocketMessage>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning(
                "Could not read game-server message: " + exception.Message,
                this
            );
            onlineStatusMessage = "The game server sent invalid data.";
            return;
        }

        if (message == null)
        {
            onlineStatusMessage = "The game server sent an empty response.";
            return;
        }

        switch (message.type)
        {
            case "open":
#if UNITY_WEBGL && !UNITY_EDITOR
                AbramsGameSocketSend(
                    gameObject.name,
                    JsonUtility.ToJson(new OnlineGameAuthRequest
                    {
                        ticket = pendingOnlineGameTicket
                    })
                );
#endif
                break;
            case "authenticated":
                onlineSocketAuthenticated = true;
                onlineGameUserId = message.userId;
                pendingOnlineGameTicket = "";
                onlineGameRoomId = message.roomId;
                selectedOnlineTeam = message.team == 1 ? "blue" : "red";
                onlineStatusMessage =
                    "Connected to the room. Team: " +
                    (message.team == 1 ? "Blue" : "Red");
                StartOnlineRoomGameplay();
                if (message.snapshot != null)
                    ApplyOnlineRoomState(message.snapshot);
                break;
            case "state":
                ApplyOnlineRoomState(new OnlineGameRoomState
                {
                    roomId = message.roomId,
                    players = message.players,
                    projectiles = message.projectiles,
                    events = message.events
                });
                break;
            case "error":
                onlineStatusMessage = string.IsNullOrWhiteSpace(message.message)
                    ? "Could not connect to the game server."
                    : message.message;
                break;
            case "close":
                if (onlineRoomActive)
                {
                    onlineStatusMessage =
                        "Disconnected from the online room.";
                    EndOnlineRoom(true);
                }
                else if (!string.IsNullOrEmpty(message.reason))
                {
                    onlineStatusMessage = message.reason;
                }
                break;
        }
    }

    private void StartOnlineRoomGameplay()
    {
        if (playerHealth == null)
        {
            onlineStatusMessage =
                "The local tank is unavailable; returning to the room list.";
            CloseOnlineRoom(true);
            return;
        }

        showRoomMenu = false;
        showOnlineMenu = false;
        showMainMenu = false;
        showPauseMenu = false;
        showSettings = false;
        showControlSettings = false;
        finishMenuVisible = false;
        gameOver = false;
        onlineRoomActive = true;
        nextOnlineInputTime = 0f;
        SetCursor(false);
        Time.timeScale = 1f;
        onlineOriginalPlayerPosition = playerHealth.transform.position;
        onlineOriginalPlayerRotation = playerHealth.transform.rotation;
        onlineOriginalPlayerHealth = playerHealth.CurrentHealth;
        onlineOriginalPlayerTransformSaved = true;

        AIRandomSpawner[] tankSpawners =
            FindObjectsOfType<AIRandomSpawner>(true);
        foreach (AIRandomSpawner spawner in tankSpawners)
            spawner.enabled = false;

        AIDroneSpawner[] droneSpawners =
            FindObjectsOfType<AIDroneSpawner>(true);
        foreach (AIDroneSpawner spawner in droneSpawners)
            spawner.enabled = false;

        AIDroneController[] drones =
            FindObjectsOfType<AIDroneController>(true);
        foreach (AIDroneController drone in drones)
        {
            if (drone != null)
                drone.gameObject.SetActive(false);
        }

        TankHealth[] tanks = FindObjectsOfType<TankHealth>(true);
        foreach (TankHealth tank in tanks)
        {
            if (tank != null && !tank.isPlayerTank)
                tank.gameObject.SetActive(false);
        }

        if (playerMovement != null)
        {
            onlinePlayerMovementWasEnabled = playerMovement.enabled;
            playerMovement.currentSpeed = 0f;
            playerMovement.enabled = true;
        }

        if (playerHealth != null)
        {
            onlinePlayerRenderers =
                playerHealth.GetComponentsInChildren<Renderer>(true);
            onlinePlayerRendererStates =
                new bool[onlinePlayerRenderers.Length];
            for (int i = 0; i < onlinePlayerRenderers.Length; i++)
                onlinePlayerRendererStates[i] = onlinePlayerRenderers[i].enabled;

            onlinePlayerColliders =
                playerHealth.GetComponentsInChildren<Collider>(true);
            onlinePlayerColliderStates =
                new bool[onlinePlayerColliders.Length];
            for (int i = 0; i < onlinePlayerColliders.Length; i++)
            {
                onlinePlayerColliderStates[i] =
                    onlinePlayerColliders[i].enabled;
                onlinePlayerColliders[i].enabled = false;
            }

            Rigidbody body = playerHealth.GetComponent<Rigidbody>();
            if (body != null)
            {
                onlinePlayerBodyWasKinematic = body.isKinematic;
                onlinePlayerBodyInterpolation = body.interpolation;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            TankFire tankFire = playerHealth.GetComponent<TankFire>();
            if (tankFire != null)
            {
                onlinePlayerTankFireWasEnabled = tankFire.enabled;
                tankFire.enabled = false;
            }
        }
        if (playerMissileLauncher != null)
        {
            onlineMissileLauncherWasEnabled =
                playerMissileLauncher.enabled;
            playerMissileLauncher.enabled = false;
        }
        UpdateGameplayStatusBarsVisibility();
    }

    private void UpdateOnlineRoomInput()
    {
        if (!onlineRoomActive || !onlineSocketAuthenticated)
        {
            return;
        }

        pendingOnlineFireInput |=
            Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
        if (Time.unscaledTime < nextOnlineInputTime)
            return;

        nextOnlineInputTime = Time.unscaledTime + 0.05f;
        OnlineGameSocketInput input = new OnlineGameSocketInput
        {
            throttle = Input.GetAxisRaw("Vertical"),
            steering = Input.GetAxisRaw("Horizontal"),
            fire = pendingOnlineFireInput ||
                Input.GetMouseButton(0) ||
                Input.GetKey(KeyCode.Space)
        };
        pendingOnlineFireInput = false;
        TankTurretMove turretControls = playerHealth != null
            ? playerHealth.GetComponentInChildren<TankTurretMove>(true)
            : null;
        if (turretControls != null && turretControls.turret != null)
            input.aimHeading = turretControls.turret.eulerAngles.y;
        else if (playerHealth != null)
            input.aimHeading = playerHealth.transform.eulerAngles.y;

        if (turretControls != null && turretControls.cannon != null)
        {
            float pitch = turretControls.cannon.localEulerAngles.x;
            input.aimPitch = pitch > 180f ? pitch - 360f : pitch;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        AbramsGameSocketSend(
            gameObject.name,
            JsonUtility.ToJson(input)
        );
#endif
    }

    private void ApplyOnlineRoomState(OnlineGameRoomState state)
    {
        if (state == null || state.players == null)
            return;

        OnlineGamePlayer localPlayer = null;
        foreach (OnlineGamePlayer player in state.players)
        {
            if (player != null && player.userId == onlineGameUserId)
            {
                localPlayer = player;
                break;
            }
        }
        if (localPlayer != null && playerHealth != null &&
            !onlineWorldOffsetInitialized)
        {
            Vector3 worldPosition = playerHealth.transform.position;
            onlineWorldOffset = worldPosition -
                new Vector3(localPlayer.x, worldPosition.y, localPlayer.z);
            onlineWorldOffsetInitialized = true;
        }

        HashSet<string> currentPlayers = new HashSet<string>();
        foreach (OnlineGamePlayer player in state.players)
        {
            if (player == null || string.IsNullOrEmpty(player.userId))
                continue;

            currentPlayers.Add(player.userId);
            if (player.userId == onlineGameUserId)
                continue;

            GameObject remote = GetOrCreateRemotePlayer(player);
            if (remote == null)
                continue;
            remote.SetActive(player.alive);
            if (player.alive)
            {
                SetOnlineTransformTarget(
                    player.userId,
                    remote.transform,
                    player,
                    onlineRemoteTargets
                );
            }
            else if (onlineRemoteTargets.TryGetValue(
                player.userId,
                out OnlineTransformTarget deadTarget))
            {
                deadTarget.wasAlive = false;
            }
        }

        List<string> disconnected = new List<string>();
        foreach (KeyValuePair<string, GameObject> entry in onlineRemotePlayers)
        {
            if (!currentPlayers.Contains(entry.Key))
                disconnected.Add(entry.Key);
        }
        foreach (string userId in disconnected)
        {
            Destroy(onlineRemotePlayers[userId]);
            onlineRemotePlayers.Remove(userId);
            onlineRemoteTargets.Remove(userId);
        }

        ApplyOnlineRoomEvents(state.events);
        if (localPlayer != null && playerHealth != null)
        {
            SetLocalPlayerVisible(localPlayer.alive);
            if (localPlayer.alive)
            {
                SetOnlineTransformTarget(
                    localPlayer,
                    playerHealth.transform,
                    onlineLocalTarget
                );
            }
            if (playerMovement != null)
            {
                playerMovement.enabled = localPlayer.alive;
                if (!localPlayer.alive)
                    playerMovement.currentSpeed = 0f;
            }
            if (!localPlayer.alive)
            {
                Rigidbody body = playerHealth.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            if (!localPlayer.alive)
                onlineLocalTarget.wasAlive = false;
            playerHealth.SetAuthoritativeHealth(localPlayer.health);
        }
        ApplyOnlineProjectiles(state.projectiles);
        currentOnlinePlayers = state.players;
    }

    private GameObject GetOrCreateRemotePlayer(OnlineGamePlayer player)
    {
        if (onlineRemotePlayers.TryGetValue(
            player.userId,
            out GameObject existing) && existing != null)
        {
            return existing;
        }

        if (playerHealth == null)
        {
            Debug.LogWarning(
                "Online room state arrived before the local tank was found.",
                this
            );
            return null;
        }

        GameObject remote = Instantiate(
            playerHealth.gameObject,
            ToWorldPosition(player.x, player.z),
            Quaternion.Euler(0f, player.heading, 0f)
        );
        remote.name = "Online Tank " + player.playerName;
        foreach (MonoBehaviour behaviour in
            remote.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            behaviour.enabled =
                behaviour is TankTrackTextureAnimation ||
                behaviour is TankRotorAnimation ||
                behaviour is TankExhaustSmoke;
        }
        foreach (Camera camera in remote.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;
        foreach (AudioListener listener in
            remote.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
        }
        foreach (Collider collider in
            remote.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
        foreach (Rigidbody body in
            remote.GetComponentsInChildren<Rigidbody>(true))
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        SetTankVisuals(
            remote,
            player.alive,
            player.team == 1
                ? new Color(0.25f, 0.58f, 1f)
                : new Color(1f, 0.34f, 0.27f)
        );
        OnlineTransformTarget target = new OnlineTransformTarget
        {
            initialized = true,
            wasAlive = player.alive,
            position = ToWorldPosition(player.x, player.z),
            rotation = Quaternion.Euler(0f, player.heading, 0f),
            turretRotation = Quaternion.Euler(
                0f,
                player.aimHeading,
                0f
            ),
            cannonPitch = player.aimPitch,
            startCannonPitch = player.aimPitch,
            startedAt = Time.unscaledTime
        };
        remote.transform.SetPositionAndRotation(
            target.position,
            target.rotation
        );
        SetRemoteTurretRotation(
            remote.transform,
            target.turretRotation,
            target.cannonPitch
        );
        onlineRemoteTargets[player.userId] = target;
        onlineRemotePlayers[player.userId] = remote;
        return remote;
    }

    private void SetOnlineTransformTarget(
        string userId,
        Transform playerTransform,
        OnlineGamePlayer player,
        Dictionary<string, OnlineTransformTarget> targets)
    {
        if (!targets.TryGetValue(
            userId,
            out OnlineTransformTarget target))
        {
            target = new OnlineTransformTarget();
            targets[userId] = target;
        }
        SetOnlineTransformTarget(player, playerTransform, target);
    }

    private void SetOnlineTransformTarget(
        OnlineGamePlayer player,
        Transform playerTransform,
        OnlineTransformTarget target)
    {
        Vector3 position = ToWorldPosition(player.x, player.z);
        Quaternion rotation = Quaternion.Euler(0f, player.heading, 0f);
        Quaternion turretRotation = Quaternion.Euler(
            0f,
            player.aimHeading,
            0f
        );
        float cannonPitch = player.aimPitch;
        bool respawned = player.alive && !target.wasAlive;

        if (!target.initialized || respawned)
        {
            playerTransform.SetPositionAndRotation(position, rotation);
            target.startPosition = position;
            target.startRotation = rotation;
            target.startTurretRotation = turretRotation;
            target.startCannonPitch = cannonPitch;
            target.duration = 0f;
            target.initialized = true;
        }
        else
        {
            target.startPosition = playerTransform.position;
            target.startRotation = playerTransform.rotation;
            TankTurretMove turretControls =
                playerTransform.GetComponentInChildren<TankTurretMove>(true);
            target.startTurretRotation =
                turretControls != null && turretControls.turret != null
                    ? turretControls.turret.rotation
                    : target.turretRotation;
            target.startCannonPitch =
                turretControls != null && turretControls.cannon != null
                    ? NormalizeAngle(turretControls.cannon.localEulerAngles.x)
                    : target.cannonPitch;
            target.duration = Mathf.Clamp(
                Time.unscaledTime - target.startedAt,
                0.04f,
                0.15f
            );
        }

        target.position = position;
        target.rotation = rotation;
        target.turretRotation = turretRotation;
        target.cannonPitch = cannonPitch;
        target.startedAt = Time.unscaledTime;
        target.wasAlive = player.alive;
    }

    private static void SetRemoteTurretRotation(
        Transform tank,
        Quaternion rotation,
        float cannonPitch)
    {
        TankTurretMove turretControls =
            tank.GetComponentInChildren<TankTurretMove>(true);
        if (turretControls != null && turretControls.turret != null)
            turretControls.turret.rotation = rotation;
        if (turretControls != null && turretControls.cannon != null)
        {
            Vector3 angles = turretControls.cannon.localEulerAngles;
            angles.x = cannonPitch;
            turretControls.cannon.localEulerAngles = angles;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private void ApplyOnlineRoomEvents(OnlineGameEvent[] events)
    {
        if (events == null)
            return;

        foreach (OnlineGameEvent roomEvent in events)
        {
            if (roomEvent == null)
                continue;

            if (roomEvent.type == "playerFired")
            {
                GameObject tank = GetOnlinePlayerObject(roomEvent.userId);
                TankFire tankFire = tank != null
                    ? tank.GetComponent<TankFire>()
                    : null;
                if (tankFire == null)
                    continue;

                tankFire.PlayNetworkedShotEffect();
                if (roomEvent.userId == onlineGameUserId)
                    tankFire.NotifyNetworkedShot(roomEvent.cooldown);

                GameObject projectile =
                    tankFire.CreateNetworkedProjectileVisual();
                if (projectile != null)
                    onlineProjectiles[roomEvent.projectileId] = projectile;
            }
            else if (roomEvent.type == "playerHit" &&
                onlineProjectiles.TryGetValue(
                    roomEvent.projectileId,
                    out GameObject projectile))
            {
                GameObject shooter =
                    GetOnlinePlayerObject(roomEvent.attackerId);
                TankFire tankFire = shooter != null
                    ? shooter.GetComponent<TankFire>()
                    : null;
                if (tankFire != null)
                    tankFire.PlayNetworkedImpactEffect(
                        projectile.transform.position
                    );
            }
        }
    }

    private GameObject GetOnlinePlayerObject(string userId)
    {
        if (userId == onlineGameUserId)
            return playerHealth != null ? playerHealth.gameObject : null;
        return onlineRemotePlayers.TryGetValue(userId, out GameObject remote)
            ? remote
            : null;
    }

    private void ApplyOnlineProjectiles(OnlineGameProjectile[] projectiles)
    {
        HashSet<int> activeProjectiles = new HashSet<int>();
        if (projectiles != null)
        {
            foreach (OnlineGameProjectile projectile in projectiles)
            {
                if (projectile == null || projectile.id <= 0)
                    continue;

                activeProjectiles.Add(projectile.id);
                if (!onlineProjectiles.TryGetValue(
                    projectile.id,
                    out GameObject visual) || visual == null)
                {
                    GameObject owner = GetOnlinePlayerObject(
                        projectile.ownerId
                    );
                    TankFire tankFire = owner != null
                        ? owner.GetComponent<TankFire>()
                        : null;
                    visual = tankFire != null
                        ? tankFire.CreateNetworkedProjectileVisual()
                        : null;
                    if (visual != null)
                        onlineProjectiles[projectile.id] = visual;
                }

                if (visual != null)
                {
                    visual.transform.SetPositionAndRotation(
                        ToWorldPosition(projectile.x, projectile.z) +
                            Vector3.up * 1.5f,
                        Quaternion.Euler(
                            projectile.pitch,
                            projectile.heading,
                            0f
                        )
                    );
                }
            }
        }

        List<int> expiredProjectiles = new List<int>();
        foreach (KeyValuePair<int, GameObject> entry in onlineProjectiles)
        {
            if (!activeProjectiles.Contains(entry.Key))
            {
                if (entry.Value != null)
                    Destroy(entry.Value);
                expiredProjectiles.Add(entry.Key);
            }
        }
        foreach (int projectileId in expiredProjectiles)
            onlineProjectiles.Remove(projectileId);
    }

    private void UpdateOnlinePlayerTransforms()
    {
        if (playerHealth != null && onlineLocalTarget.initialized &&
            onlineLocalTarget.wasAlive)
        {
            float correction = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            playerHealth.transform.position = Vector3.Lerp(
                playerHealth.transform.position,
                onlineLocalTarget.position,
                correction
            );
            playerHealth.transform.rotation = Quaternion.Slerp(
                playerHealth.transform.rotation,
                onlineLocalTarget.rotation,
                correction
            );
        }

        float now = Time.unscaledTime;
        foreach (KeyValuePair<string, OnlineTransformTarget> entry in
            onlineRemoteTargets)
        {
            if (!onlineRemotePlayers.TryGetValue(
                entry.Key,
                out GameObject remote) || remote == null ||
                !remote.activeSelf)
            {
                continue;
            }

            OnlineTransformTarget target = entry.Value;
            float blend = target.duration <= 0f
                ? 1f
                : Mathf.Clamp01((now - target.startedAt) / target.duration);
            remote.transform.SetPositionAndRotation(
                Vector3.Lerp(target.startPosition, target.position, blend),
                Quaternion.Slerp(target.startRotation, target.rotation, blend)
            );
            SetRemoteTurretRotation(
                remote.transform,
                Quaternion.Slerp(
                    target.startTurretRotation,
                    target.turretRotation,
                    blend
                ),
                Mathf.LerpAngle(
                    target.startCannonPitch,
                    target.cannonPitch,
                    blend
                )
            );

            TankMovement movement =
                remote.GetComponentInChildren<TankMovement>(true);
            if (movement != null)
                movement.currentSpeed = currentOnlinePlayers.Length > 0
                    ? GetOnlinePlayerSpeed(entry.Key)
                    : 0f;
        }
    }

    private float GetOnlinePlayerSpeed(string userId)
    {
        foreach (OnlineGamePlayer player in currentOnlinePlayers)
        {
            if (player != null && player.userId == userId)
                return player.alive ? player.speed : 0f;
        }
        return 0f;
    }

    private Vector3 ToWorldPosition(float x, float z)
    {
        float y = playerHealth != null
            ? playerHealth.transform.position.y
            : 0f;
        return onlineWorldOffset + new Vector3(x, y, z);
    }

    private void SetLocalPlayerVisible(bool visible)
    {
        for (int i = 0; i < onlinePlayerRenderers.Length; i++)
        {
            Renderer renderer = onlinePlayerRenderers[i];
            if (renderer != null)
                renderer.enabled = visible && onlinePlayerRendererStates[i];
        }
    }

    private static void SetTankVisuals(
        GameObject tank,
        bool visible,
        Color? tint)
    {
        foreach (Renderer renderer in
            tank.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
            if (visible && tint.HasValue)
            {
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", tint.Value);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", tint.Value);
            }
        }
    }

    private void EndOnlineRoom(bool showMenu)
    {
        onlineRoomActive = false;
        onlineSocketAuthenticated = false;
        pendingOnlineGameTicket = "";
        onlineGameUserId = "";
        onlineGameRoomId = "";
        onlineWorldOffsetInitialized = false;
        pendingOnlineFireInput = false;
        onlineLocalTarget.initialized = false;
        onlineLocalTarget.wasAlive = false;

        foreach (GameObject remote in onlineRemotePlayers.Values)
        {
            if (remote != null)
                Destroy(remote);
        }
        onlineRemotePlayers.Clear();
        onlineRemoteTargets.Clear();
        foreach (GameObject projectile in onlineProjectiles.Values)
        {
            if (projectile != null)
                Destroy(projectile);
        }
        onlineProjectiles.Clear();

        if (playerMovement != null)
            playerMovement.enabled = onlinePlayerMovementWasEnabled;
        if (playerHealth != null)
        {
            if (onlineOriginalPlayerTransformSaved)
            {
                playerHealth.transform.SetPositionAndRotation(
                    onlineOriginalPlayerPosition,
                    onlineOriginalPlayerRotation
                );
            }
            Rigidbody body = playerHealth.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = onlinePlayerBodyWasKinematic;
                body.interpolation = onlinePlayerBodyInterpolation;
            }
            TankFire tankFire = playerHealth.GetComponent<TankFire>();
            if (tankFire != null)
                tankFire.enabled = onlinePlayerTankFireWasEnabled;
            playerHealth.SetAuthoritativeHealth(onlineOriginalPlayerHealth);
        }
        if (playerMissileLauncher != null)
        {
            playerMissileLauncher.enabled =
                onlineMissileLauncherWasEnabled;
        }
        onlineOriginalPlayerTransformSaved = false;
        if (playerMovement != null)
            playerMovement.currentSpeed = 0f;
        for (int i = 0; i < onlinePlayerColliders.Length; i++)
        {
            Collider collider = onlinePlayerColliders[i];
            if (collider != null)
                collider.enabled = onlinePlayerColliderStates[i];
        }
        onlinePlayerColliders = Array.Empty<Collider>();
        onlinePlayerColliderStates = Array.Empty<bool>();
        for (int i = 0; i < onlinePlayerRenderers.Length; i++)
        {
            Renderer renderer = onlinePlayerRenderers[i];
            if (renderer != null)
                renderer.enabled = onlinePlayerRendererStates[i];
        }
        onlinePlayerRenderers = Array.Empty<Renderer>();
        onlinePlayerRendererStates = Array.Empty<bool>();
        currentOnlinePlayers = Array.Empty<OnlineGamePlayer>();

        if (showMenu)
        {
            showMainMenu = true;
            showRoomMenu = !string.IsNullOrEmpty(onlineAuthToken);
            showOnlineMenu = false;
            Time.timeScale = 0f;
            SetCursor(true);
            RefreshOnlineRooms();
        }
        UpdateGameplayStatusBarsVisibility();
    }

    private void CloseOnlineRoom(bool showMenu)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AbramsGameSocketClose(gameObject.name);
#endif
        EndOnlineRoom(showMenu);
    }

    private void DrawOnlineRoomMenu(float x, float y, float width)
    {
        GUI.Label(
            new Rect(x + 32f, y + 72f, width - 64f, 32f),
            onlineStatusMessage,
            statsStyle
        );
        GUI.Label(
            new Rect(x + 32f, y + 108f, width - 64f, 24f),
            "ROOM NAME",
            statsStyle
        );
        onlineRoomNameInput = GUI.TextField(
            new Rect(x + 32f, y + 132f, width - 64f, 38f),
            onlineRoomNameInput,
            32,
            menuInputStyle
        );

        float teamButtonWidth = (width - 72f) * 0.5f;
        if (DrawMenuButton(
            new Rect(x + 32f, y + 180f, teamButtonWidth, 38f),
            selectedOnlineTeam == "blue" ? "✓ BLUE TEAM" : "BLUE TEAM",
            selectedOnlineTeam == "blue" ? selectedButtonStyle : buttonStyle))
        {
            selectedOnlineTeam = "blue";
        }
        if (DrawMenuButton(
            new Rect(x + 40f + teamButtonWidth, y + 180f, teamButtonWidth, 38f),
            selectedOnlineTeam == "red" ? "✓ RED TEAM" : "RED TEAM",
            selectedOnlineTeam == "red" ? selectedButtonStyle : buttonStyle))
        {
            selectedOnlineTeam = "red";
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = !onlineRequestInProgress;
        if (DrawMenuButton(
            new Rect(x + 32f, y + 228f, teamButtonWidth, 40f),
            "CREATE ROOM",
            buttonStyle))
        {
            CreateOnlineRoom();
        }
        if (DrawMenuButton(
            new Rect(x + 40f + teamButtonWidth, y + 228f, teamButtonWidth, 40f),
            "REFRESH ROOMS",
            buttonStyle))
        {
            RefreshOnlineRooms();
        }
        GUI.enabled = previousEnabled;

        GUI.Label(
            new Rect(x + 32f, y + 276f, width - 64f, 24f),
            "ACTIVE ROOMS",
            statsStyle
        );
        Rect listRect = new Rect(
            x + 24f,
            y + 302f,
            width - 48f,
            330f
        );
        Rect contentRect = new Rect(
            0f,
            0f,
            listRect.width - 18f,
            Mathf.Max(330f, availableOnlineRooms.Count * 58f)
        );
        onlineRoomsScroll = GUI.BeginScrollView(
            listRect,
            onlineRoomsScroll,
            contentRect
        );
        if (availableOnlineRooms.Count == 0)
        {
            GUI.Label(
                new Rect(10f, 10f, contentRect.width - 20f, 52f),
                "No open rooms yet.\nCreate a room or refresh to try again.",
                statsStyle
            );
        }
        for (int i = 0; i < availableOnlineRooms.Count; i++)
        {
            OnlineRoomInfo room = availableOnlineRooms[i];
            float rowY = i * 58f;
            GUI.Label(
                new Rect(8f, rowY, contentRect.width - 154f, 26f),
                room.name + "  [" + room.status + "]",
                statsStyle
            );
            GUI.Label(
                new Rect(8f, rowY + 27f, contentRect.width - 154f, 22f),
                room.playerCount + " / " + room.maxPlayers + " players",
                statsStyle
            );

            bool canJoin = room.status == "waiting" &&
                room.playerCount < room.maxPlayers &&
                !onlineRequestInProgress;
            bool previousJoinEnabled = GUI.enabled;
            GUI.enabled = canJoin;
            if (DrawMenuButton(
                new Rect(contentRect.width - 138f, rowY + 6f, 128f, 38f),
                "JOIN",
                buttonStyle))
            {
                JoinOnlineRoom(room.id);
            }
            GUI.enabled = previousJoinEnabled;
        }
        GUI.EndScrollView();

        if (DrawMenuButton(
            new Rect(x + 32f, y + 652f, width - 64f, 38f),
            "BACK TO MENU",
            buttonStyle))
        {
            showRoomMenu = false;
            showOnlineMenu = false;
            showMainMenu = true;
            Time.timeScale = 0f;
            SetCursor(true);
        }
    }

    private void DrawOnlineRoomHud()
    {
        if (!onlineRoomActive)
            return;

        GUI.Box(new Rect(18f, 18f, 340f, 96f), GUIContent.none, hudPanelStyle);
        GUI.Label(
            new Rect(30f, 22f, 175f, 26f),
            "ONLINE ROOM",
            hudValueStyle
        );

        if (DrawMenuButton(
            new Rect(190f, 23f, 140f, 28f),
            "LEAVE ROOM",
            buttonStyle))
        {
            CloseOnlineRoom(true);
        }
        GUI.Label(
            new Rect(30f, 52f, 300f, 22f),
            "Room " + onlineGameRoomId + "  |  Team " +
                (selectedOnlineTeam == "blue" ? "Blue" : "Red"),
            statsStyle
        );
        OnlineGamePlayer localPlayer = null;
        if (currentOnlinePlayers != null)
        {
            foreach (OnlineGamePlayer player in currentOnlinePlayers)
            {
                if (player != null && player.userId == onlineGameUserId)
                {
                    localPlayer = player;
                    break;
                }
            }
        }
        GUI.Label(
            new Rect(30f, 74f, 310f, 22f),
            localPlayer == null
                ? "Waiting for room state..."
                : "Players " + currentOnlinePlayers.Length +
                    "  |  K " + localPlayer.kills +
                    "  D " + localPlayer.deaths +
                    "  HP " + localPlayer.health,
            statsStyle
        );
    }

    private void UpdateOnlineRoom()
    {
        if (!onlineRoomActive)
            return;

        UpdateOnlineRoomInput();
        UpdateOnlinePlayerTransforms();
        if (playerTankFire != null)
            playerTankFire.UpdateNetworkReloadState();
    }

    private void OnDestroyOnlineRoom()
    {
        foreach (GameObject remote in onlineRemotePlayers.Values)
        {
            if (remote != null)
                Destroy(remote);
        }
        onlineRemotePlayers.Clear();
        onlineRemoteTargets.Clear();
        foreach (GameObject projectile in onlineProjectiles.Values)
        {
            if (projectile != null)
                Destroy(projectile);
        }
        onlineProjectiles.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
        AbramsGameSocketClose(gameObject.name);
#endif
    }
}
