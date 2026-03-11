using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : NetworkBehaviour
{
    [Header("Идентификация")]
    [SerializeField] private string _objectId;

    [Header("Состояния")]
    [SerializeField] private string _defaultState = "default";

    [Tooltip("Список состояний по очереди. Interact() циклически переключает их.")]
    [SerializeField] private string[] _statesCycle = { "default", "activated" };

    [Header("Событие взаимодействия (локальная шина)")]
    [SerializeField] private GameEventInteraction _onInteractEvent;

    [Header("Реакция на смену состояния")]
    public UnityEvent<string> OnStateChanged;

    [SyncVar(hook = nameof(OnStateSync))]
    private string _currentState;

    private int _currentStateIndex = 0;

    public string ObjectId => _objectId;
    public string CurrentState => _currentState;

    private void Awake()
    {
        _currentState = _defaultState;
    }

    public override void OnStartServer()
    {
        InteractableObjectRegistry.Register(_objectId, this);
    }

    public override void OnStartClient()
    {
        InteractableObjectRegistry.Register(_objectId, this);
    }

    public override void OnStopServer()
    {
        InteractableObjectRegistry.Unregister(_objectId);
    }

    public override void OnStopClient()
    {
        if (!NetworkServer.active)
            InteractableObjectRegistry.Unregister(_objectId);
    }

    public void SetHighlight(bool enabled)
    {
        var outline = GetComponentInChildren<OutlineEffect>();
        outline?.SetHighlight(enabled);
    }

    public void Interact()
    {
        if (_statesCycle.Length == 0) return;
        _currentStateIndex = (_currentStateIndex + 1) % _statesCycle.Length;
        string nextState = _statesCycle[_currentStateIndex];
        PuzzleDebugOverlay.Log($"[Interact] {_objectId} попытка → {nextState}");
        _onInteractEvent?.Raise(new InteractionData(_objectId, nextState));
    }


    [Server]
    public void ApplyState(string newState)
    {
        _currentState = newState;
    }

    private void OnStateSync(string oldState, string newState)
    {
        OnStateChanged?.Invoke(newState);
        PuzzleDebugOverlay.Log($"[{_objectId}] {oldState} → {newState}", PuzzleDebugOverlay.DebugLevel.Ok);
    }
}