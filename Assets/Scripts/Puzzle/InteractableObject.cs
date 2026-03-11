using Mirror;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Интерактивный объект с циклическими состояниями и сетевой синхронизацией.
///
/// ИЗМЕНЕНИЕ: реализует IFocusable — InteractionRaycaster работает через интерфейс.
/// Вся остальная логика без изменений.
/// </summary>
public class InteractableObject : NetworkBehaviour, IFocusable
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
        SyncIndexFromState(_currentState);
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

    // ──── IFocusable ────

    public void SetHighlight(bool enabled)
    {
        var outline = GetComponentInChildren<OutlineEffect>();
        outline?.SetHighlight(enabled);
    }

    // ──── Взаимодействие ────

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
        SyncIndexFromState(newState);
        OnStateChanged?.Invoke(newState);
        PuzzleDebugOverlay.Log($"[{_objectId}] {oldState} → {newState}", PuzzleDebugOverlay.DebugLevel.Ok);
    }

    private void SyncIndexFromState(string state)
    {
        int index = System.Array.IndexOf(_statesCycle, state);
        _currentStateIndex = index >= 0 ? index : 0;
    }
}
