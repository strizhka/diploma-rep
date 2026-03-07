using System;
using UnityEngine;

[Serializable]
public class FoundServerEntry
{
    public string RoomName;
    public int CurrentPlayers;
    public int MaxPlayers;
    public Uri Uri;
    public long ServerId;
    public float DiscoveredAt;
}