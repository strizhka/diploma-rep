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

    // SyncVar: сервер меняет → все клиенты получают автоматически
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
        // На хосте сервер уже зарегистрировал этот объект — Register сам разберётся
        InteractableObjectRegistry.Register(_objectId, this);
    }

    public override void OnStopServer()
    {
        InteractableObjectRegistry.Unregister(_objectId);
    }

    public override void OnStopClient()
    {
        // На хосте не разрегистрируем на OnStopClient — это произойдёт в OnStopServer
        if (!NetworkServer.active)
            InteractableObjectRegistry.Unregister(_objectId);
    }

    public void SetHighlight(bool enabled)
    {
        // Ищем OutlineEffect на себе или дочерних объектах (меш может быть на дочернем)
        var outline = GetComponentInChildren<OutlineEffect>();
        outline?.SetHighlight(enabled);
    }

    /// <summary>
    /// Вызывается игроком при нажатии E. Только локально — решает какое следующее состояние.
    /// Затем передаёт решение на шину событий → PuzzleNetworkBridge → сервер.
    /// </summary>
    public void Interact()
    {
        if (_statesCycle.Length == 0) return;
        _currentStateIndex = (_currentStateIndex + 1) % _statesCycle.Length;
        string nextState = _statesCycle[_currentStateIndex];
        PuzzleDebugOverlay.Log($"[Interact] {_objectId} попытка → {nextState}");
        _onInteractEvent?.Raise(new InteractionData(_objectId, nextState));
    }

    /// <summary>
    /// Вызывается PuzzleManager'ом с сервера для принудительной смены состояния.
    /// </summary>
    [Server]
    public void ApplyState(string newState)
    {
        _currentState = newState; // SyncVar → hook сработает на всех клиентах
    }

    // Hook вызывается на каждом клиенте при изменении SyncVar
    private void OnStateSync(string oldState, string newState)
    {
        OnStateChanged?.Invoke(newState);
        PuzzleDebugOverlay.Log($"[{_objectId}] {oldState} → {newState}", PuzzleDebugOverlay.DebugLevel.Ok);
    }
}