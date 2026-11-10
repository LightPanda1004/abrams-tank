using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[Serializable]
public class LanRoomAnnouncement
{
    public string roomName;
    public int gamePort;
    public int playerCount;
    public int maxPlayers;
}

public class LanRoomInfo
{
    public string roomName;
    public string address;
    public int gamePort;
    public int playerCount;
    public int maxPlayers;
    public float lastSeen;
}

public class LanRoomDiscovery : MonoBehaviour
{
    public const int DiscoveryPort = 47777;

    private readonly List<LanRoomInfo> rooms = new List<LanRoomInfo>();
    private UdpClient listener;
    private UdpClient advertiser;
    private Coroutine advertiseRoutine;
    private Func<int> playerCountProvider;
    private string advertisedRoomName;
    private int advertisedGamePort;
    private int advertisedMaxPlayers;

    public IReadOnlyList<LanRoomInfo> Rooms => rooms;

    public void StartSearching()
    {
        StopSearching();
        rooms.Clear();

        try
        {
            listener = new UdpClient();
            listener.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
            listener.Client.Bind(
                new IPEndPoint(IPAddress.Any, DiscoveryPort)
            );
            listener.EnableBroadcast = true;
        }
        catch (SocketException exception)
        {
            Debug.LogWarning("LAN room discovery failed: " + exception.Message);
            StopSearching();
        }
    }

    public void StartAdvertising(
        string roomName,
        int gamePort,
        int maxPlayers,
        Func<int> getPlayerCount)
    {
        StopAdvertising();

        try
        {
            advertiser = new UdpClient();
            advertiser.EnableBroadcast = true;
            advertisedRoomName = roomName;
            advertisedGamePort = gamePort;
            advertisedMaxPlayers = maxPlayers;
            playerCountProvider = getPlayerCount;
            advertiseRoutine = StartCoroutine(AdvertiseRoutine());
        }
        catch (SocketException exception)
        {
            Debug.LogWarning("LAN room advertisement failed: " + exception.Message);
            StopAdvertising();
        }
    }

    public void StopSearching()
    {
        if (listener == null)
            return;

        listener.Close();
        listener = null;
    }

    public void StopAdvertising()
    {
        if (advertiseRoutine != null)
        {
            StopCoroutine(advertiseRoutine);
            advertiseRoutine = null;
        }

        if (advertiser != null)
        {
            advertiser.Close();
            advertiser = null;
        }

        playerCountProvider = null;
    }

    private void Update()
    {
        ReceiveAnnouncements();
        rooms.RemoveAll(room => Time.realtimeSinceStartup - room.lastSeen > 4f);
    }

    private IEnumerator AdvertiseRoutine()
    {
        IPEndPoint broadcastEndpoint =
            new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        while (advertiser != null)
        {
            LanRoomAnnouncement announcement = new LanRoomAnnouncement
            {
                roomName = advertisedRoomName,
                gamePort = advertisedGamePort,
                playerCount = playerCountProvider != null
                    ? playerCountProvider()
                    : 1,
                maxPlayers = advertisedMaxPlayers
            };

            byte[] payload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(announcement)
            );

            try
            {
                advertiser.Send(payload, payload.Length, broadcastEndpoint);
            }
            catch (SocketException exception)
            {
                Debug.LogWarning("LAN room advertisement stopped: " + exception.Message);
                StopAdvertising();
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private void ReceiveAnnouncements()
    {
        if (listener == null)
            return;

        try
        {
            while (listener.Available > 0)
            {
                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] payload = listener.Receive(ref remoteEndpoint);
                LanRoomAnnouncement announcement = JsonUtility.FromJson<LanRoomAnnouncement>(
                    Encoding.UTF8.GetString(payload)
                );

                if (announcement == null || string.IsNullOrWhiteSpace(announcement.roomName))
                    continue;

                string address = remoteEndpoint.Address.ToString();
                LanRoomInfo room = rooms.Find(item =>
                    item.address == address &&
                    item.gamePort == announcement.gamePort &&
                    item.roomName == announcement.roomName
                );

                if (room == null)
                {
                    room = new LanRoomInfo
                    {
                        address = address,
                        gamePort = announcement.gamePort,
                        roomName = announcement.roomName
                    };
                    rooms.Add(room);
                }

                room.playerCount = announcement.playerCount;
                room.maxPlayers = announcement.maxPlayers;
                room.lastSeen = Time.realtimeSinceStartup;
            }
        }
        catch (SocketException exception)
        {
            Debug.LogWarning("LAN room discovery stopped: " + exception.Message);
            StopSearching();
        }
    }

    private void OnDestroy()
    {
        StopSearching();
        StopAdvertising();
    }
}
