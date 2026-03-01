using System.Net;
using Mirror.Discovery;
using UnityEngine;

public class EscapeRoomNetworkDiscovery : NetworkDiscoveryBase<DiscoveryRequest, DiscoveryResponse>
{
    [Header("Настройки комнаты")]
    [SerializeField] private string _roomName = "Комната побега";
    [SerializeField] private int _maxPlayers = 2;

    public new long ServerId { get; private set; }

    public override void Start()
    {
        ServerId = RandomLong();
        base.Start();
        PuzzleDebugOverlay.Log($"[Discovery] Инициализирован. ServerId={ServerId}");
    }

    protected override DiscoveryResponse ProcessRequest(DiscoveryRequest request, IPEndPoint endpoint)
    {
        PuzzleDebugOverlay.Log($"[Discovery] Запрос от клиента: {endpoint.Address}");

        return new DiscoveryResponse
        {
            RoomName = _roomName,
            CurrentPlayers = Mirror.NetworkServer.connections.Count,
            MaxPlayers = _maxPlayers,
            ServerId = ServerId
        };
    }

    protected override void ProcessResponse(DiscoveryResponse response, IPEndPoint endpoint)
    {
        string ip = IsLocalAddress(endpoint.Address)
            ? "127.0.0.1"
            : endpoint.Address.ToString();

        var uri = new System.Uri($"kcp://{ip}");

        PuzzleDebugOverlay.Log($"[Discovery] Найден сервер: {response.RoomName} на {ip}");
        LobbyUIManager.OnServerFound(response, uri);
    }

    private bool IsLocalAddress(System.Net.IPAddress address)
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.Equals(address)) return true;
        }
        return false;
    }

    public void StartAdvertising()
    {
        AdvertiseServer();
        PuzzleDebugOverlay.Log("[Discovery] Хост начал рассылку");
    }

    public void StartSearching()
    {
        StartDiscovery();
        PuzzleDebugOverlay.Log("[Discovery] Клиент начал поиск");
    }

    public void StopAll()
    {
        StopDiscovery();
        PuzzleDebugOverlay.Log("[Discovery] Discovery остановлен");
    }

    private static new long RandomLong()
    {
        var bytes = new byte[8];
        new System.Random().NextBytes(bytes);
        return System.BitConverter.ToInt64(bytes, 0);
    }
}