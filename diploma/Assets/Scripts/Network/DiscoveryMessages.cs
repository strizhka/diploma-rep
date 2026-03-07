using Mirror;

public struct DiscoveryRequest : NetworkMessage
{
    // пустой — клиент просто кричит "есть кто?"
}

public struct DiscoveryResponse : NetworkMessage
{
    public string RoomName;
    public int CurrentPlayers;
    public int MaxPlayers;
    public long ServerId;
}