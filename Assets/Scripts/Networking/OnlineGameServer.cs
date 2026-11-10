using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class OnlineGameServer : MonoBehaviour
{
    public event Action<int> OnClientConnected;
    public event Action<int> OnClientDisconnected;

    public NetworkManager Manager { get; private set; }
    public Transport Transport { get; private set; }
    public TelepathyTransport TelepathyTransport { get; private set; }
    public bool IsListening => NetworkServer.active;
    public bool IsServer => NetworkServer.active;
    public int LocalConnectionId =>
        NetworkClient.isConnected ? NetworkClient.connection.connectionId : -1;

    public GameObject PlayerPrefab { get; set; }
    public int MaxPlayers { get; set; } = 4;

    private byte[] connectionPayload = Array.Empty<byte>();
    private Action<NetworkConnection, bool> connectionApproval;

    public void Configure(
        GameObject playerPrefab,
        int maxPlayers,
        byte[] connectionPayload)
    {
        PlayerPrefab = playerPrefab;
        MaxPlayers = maxPlayers;
        this.connectionPayload = connectionPayload ?? Array.Empty<byte>();
    }

    public void SetConnectionApproval(Action<NetworkConnection, bool> callback)
    {
        connectionApproval = callback;
    }

    public bool StartServer(
        string address,
        ushort port,
        bool useEncryption,
        string serverCertificatePem,
        string serverPrivateKeyPem)
    {
        Shutdown();
        CreateNetworkManager();

        NetworkServer.OnConnectEvent += OnServerConnectInternal;
        NetworkServer.OnDisconnectEvent += OnServerDisconnectInternal;
        NetworkClient.OnConnectedEvent += OnClientConnectedInternal;
        NetworkClient.OnDisconnectedEvent += OnClientDisconnectedInternal;

        if (Manager is Mirror.NetworkManager mirrorManager)
        {
            mirrorManager.networkAddress = address;
            mirrorManager.networkPort = port;
        }

        if (!NetworkServer.Listen(port))
        {
            Debug.LogError("Could not start the online server.", this);
            Shutdown();
            return false;
        }

        return true;
    }

    public bool StartClient(
        string address,
        ushort port,
        bool useEncryption,
        string serverHostSecrets)
    {
        Shutdown();
        CreateNetworkManager();

        NetworkServer.OnConnectEvent += OnServerConnectInternal;
        NetworkServer.OnDisconnectEvent += OnServerDisconnectInternal;
        NetworkClient.OnConnectedEvent += OnClientConnectedInternal;
        NetworkClient.OnDisconnectedEvent += OnClientDisconnectedInternal;

        if (Manager is Mirror.NetworkManager mirrorManager)
        {
            mirrorManager.networkAddress = address;
            mirrorManager.networkPort = port;
        }

        NetworkClient.Connect(address, port);

        return true;
    }

    public bool StartHost(
        string address,
        ushort port,
        bool useEncryption,
        string serverHostSecrets)
    {
        Shutdown();
        CreateNetworkManager();

        NetworkServer.OnConnectEvent += OnServerConnectInternal;
        NetworkServer.OnDisconnectEvent += OnServerDisconnectInternal;
        NetworkClient.OnConnectedEvent += OnClientConnectedInternal;
        NetworkClient.OnDisconnectedEvent += OnClientDisconnectedInternal;

        if (Manager is Mirror.NetworkManager mirrorManager)
        {
            mirrorManager.networkAddress = address;
            mirrorManager.networkPort = port;
        }

        if (!NetworkServer.Listen(port))
        {
            Debug.LogError("Could not start the online host.", this);
            Shutdown();
            return false;
        }

        NetworkClient.Connect("127.0.0.1", port);

        return true;
    }

    private void CreateNetworkManager()
    {
        GameObject managerObject = new GameObject("Mirror Network Manager");
        Manager = managerObject.AddComponent<Mirror.NetworkManager>();
        Transport = managerObject.gameObject.AddComponent<TelepathyTransport>();
        TelepathyTransport = Transport as TelepathyTransport;
        Manager.transport = Transport;

        Manager.startOnAwake = false;
        Manager.autoCreatePlayer = false;
        Manager.playerPrefab = PlayerPrefab;
        Manager.maxConnections = MaxPlayers;

        if (PlayerPrefab != null)
        {
            NetworkClient.RegisterPrefab(PlayerPrefab);
            TankFire prefabTankFire = PlayerPrefab.GetComponent<TankFire>();
            if (prefabTankFire != null && prefabTankFire.shellPrefab != null)
            {
                NetworkClient.RegisterPrefab(prefabTankFire.shellPrefab);
            }
        }
    }

    public void Shutdown()
    {
        NetworkServer.OnConnectEvent -= OnServerConnectInternal;
        NetworkServer.OnDisconnectEvent -= OnServerDisconnectInternal;
        NetworkClient.OnConnectedEvent -= OnClientConnectedInternal;
        NetworkClient.OnDisconnectedEvent -= OnClientDisconnectedInternal;

        if (NetworkServer.active)
            NetworkServer.Shutdown();

        if (NetworkClient.isConnected)
            NetworkClient.Disconnect();

        if (Manager != null)
        {
            Destroy(Manager.gameObject);
            Manager = null;
        }

        Transport = null;
        TelepathyTransport = null;
    }

    private void OnServerConnectInternal(NetworkConnection conn)
    {
        bool approved = connectionApproval != null
            ? connectionApproval(conn, true)
            : true;

        if (!approved)
        {
            conn.Disconnect();
            return;
        }

        OnClientConnected?.Invoke(conn.connectionId);
    }

    private void OnServerDisconnectInternal(NetworkConnection conn)
    {
        OnClientDisconnected?.Invoke(conn.connectionId);
    }

    private void OnClientConnectedInternal(NetworkConnection conn)
    {
        OnClientConnected?.Invoke(conn.connectionId);
    }

    private void OnClientDisconnectedInternal(NetworkConnection conn)
    {
        OnClientDisconnected?.Invoke(conn.connectionId);
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
