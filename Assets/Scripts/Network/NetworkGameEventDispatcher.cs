using Mirror;
using UnityEngine;

public class NetworkGameEventDispatcher : NetworkBehaviour
{
    private static NetworkGameEventDispatcher _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[NetworkGameEventDispatcher] Дубликат уничтожен.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public static void Raise(GameEvent gameEvent)
    {
        if (_instance == null)
        {
            Debug.LogError("[NetworkGameEventDispatcher] Instance не существует. Событие не отправлено.");
            return;
        }
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