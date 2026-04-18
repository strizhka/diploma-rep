using Mirror;
using UnityEngine;

public class NetworkEventBridge : NetworkBehaviour
{
    [Header("Событийная шина")]
    [Tooltip("Та же GameEventInteraction что на InteractableObject и ItemReceiver")]
    [SerializeField] private GameEventInteraction _interactionEvent;

    private static NetworkEventBridge _instance;
    
    private PuzzleDirector _director;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[NetworkEventBridge] Дубликат уничтожен.");
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

    private void OnEnable()
    {
        _interactionEvent?.AddListener(OnLocalInteraction);
    }

    private void OnDisable()
    {
        _interactionEvent?.RemoveListener(OnLocalInteraction);
    }

    public override void OnStartServer()
    {
        _director = FindAnyObjectByType<PuzzleDirector>();
        if (_director == null)
            Debug.LogWarning("[NetworkEventBridge] PuzzleDirector не найден на сцене.");
        else
            PuzzleDebugOverlay.Log("[NetworkEventBridge] PuzzleDirector закэширован.");
    }

    private void OnLocalInteraction(InteractionData data)
    {
        PuzzleDebugOverlay.Log($"[Bridge] локальное событие: {data.ObjectId} = {data.NewState}");
        CmdReportInteraction(data.ObjectId, data.NewState);
    }

    [Command(requiresAuthority = false)]
    private void CmdReportInteraction(string objectId, string newState)
    {
        PuzzleDebugOverlay.Log($"[Bridge:Server] {objectId} = {newState}");

        if (_director == null)
            _director = FindAnyObjectByType<PuzzleDirector>();

        _director?.ReportInteraction(objectId, newState);
    }
    
    public static void Broadcast(GameEvent gameEvent)
    {
        if (_instance == null)
        {
            Debug.LogError("[NetworkEventBridge] Instance не существует. Broadcast не отправлен.");
            return;
        }
        _instance.CmdBroadcast(gameEvent.EventId);
    }

    [Command(requiresAuthority = false)]
    private void CmdBroadcast(int eventId)
    {
        RpcBroadcast(eventId);
    }

    [ClientRpc]
    private void RpcBroadcast(int eventId)
    {
        var gameEvent = NetworkGameEventRegistry.Get(eventId);
        gameEvent?.Raise();
    }
}
