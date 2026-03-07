using Mirror;
using UnityEngine;

public class NetworkGameEventDispatcher : NetworkBehaviour
{
    private static NetworkGameEventDispatcher _instance;

    private void Awake()
    {
        _instance = this;
    }

    public static void Raise(GameEvent gameEvent)
    {
        _instance.CmdRaiseEvent(gameEvent.EventId);
    }

    [Command(requiresAuthority = false)]
    private void CmdRaiseEvent(int eventId)
    {
        RpcRaiseEvent(eventId);
    }

    [ClientRpc]
    private void RpcRaiseEvent(int eventId)
    {
        var gameEvent = NetworkGameEventRegistry.Get(eventId);
        gameEvent?.Raise();
    }
}