using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : NetworkBehaviour, IFocusable
{
    [Header("Идентификация")]
    [Tooltip("Уникальный ID. Если пусто — автогенерируется из имени GameObject.")]
    [SerializeField] private string _objectId;

    [Header("Состояния")]
    [SerializeField] private string _defaultState = "default";

    [Tooltip("Список состояний по очереди. Interact() циклически переключает их.")]
    [SerializeField] private string[] _statesCycle = { "default", "activated" };

    [Header("Событие взаимодействия (локальная шина)")]
    [SerializeField] private GameEventInteraction _onInteractEvent;

    [Header("Начальное состояние")]
    [Tooltip("Объект начинает скрытым (невидим, нет коллайдера). " +
             "Показывается через RevealTemplate.")]
    [SerializeField] private bool _startHidden;

    [Tooltip("Объект начинает заблокированным (E не работает). " +
             "Разблокируется через UnlockTemplate.")]
    [SerializeField] private bool _startLocked;

    [Header("Реакция на смену состояния")]
    public UnityEvent<string> OnStateChanged;

    [SyncVar(hook = nameof(OnStateSync))]
    private string _currentState;

    [SyncVar(hook = nameof(OnHiddenSync))]
    private bool _isHidden;

    [SyncVar]
    private bool _isLocked;

    private int _currentStateIndex;
    private OutlineEffect _outlineEffect;
    private Renderer[] _renderers;
    private Collider[] _colliders;

    public string ObjectId => _objectId;
    public string CurrentState => _currentState;
    public bool IsHidden => _isHidden;
    public bool IsLocked => _isLocked;

    private void Awake()
    {
        if (string.IsNullOrEmpty(_objectId))
            _objectId = gameObject.name;

        _currentState = _defaultState;

        _outlineEffect = GetComponentInChildren<OutlineEffect>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    public override void OnStartServer()
    {
        InteractableObjectRegistry.Register(_objectId, this);

        _isHidden = _startHidden;
        _isLocked = _startLocked;
    }

    public override void OnStartClient()
    {
        InteractableObjectRegistry.Register(_objectId, this);
        SyncIndexFromState(_currentState);
        ApplyVisibility(!_isHidden);
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
        if (_isHidden) return;
        _outlineEffect?.SetHighlight(enabled);
    }

    public void Interact()
    {
        if (_isLocked)
        {
            PuzzleDebugOverlay.Log($"[Interact] {_objectId} заблокирован — игнорируем E");
            return;
        }

        if (_statesCycle.Length == 0) return;

        _currentStateIndex = (_currentStateIndex + 1) % _statesCycle.Length;
        string nextState = _statesCycle[_currentStateIndex];
        PuzzleDebugOverlay.Log($"[Interact] {_objectId} попытка → {nextState}");
        CmdSelfApplyState(nextState);
        _onInteractEvent?.Raise(new InteractionData(_objectId, nextState));
    }

    [Command(requiresAuthority = false)]
    private void CmdSelfApplyState(string newState)
    {
        _currentState = newState;
        PuzzleDebugOverlay.Log($"[Server] {_objectId} → {newState}");
    }

    [Server]
    public void ApplyState(string newState)
    {
        _currentState = newState;
    }

    [Server]
    public void SetHidden(bool hidden)
    {
        _isHidden = hidden;
    }

    [Server]
    public void SetLocked(bool locked)
    {
        _isLocked = locked;
    }

    private void OnStateSync(string oldState, string newState)
    {
        SyncIndexFromState(newState);
        OnStateChanged?.Invoke(newState);
        PuzzleDebugOverlay.Log(
            $"[{_objectId}] {oldState} → {newState}",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    private void OnHiddenSync(bool _, bool hidden)
    {
        ApplyVisibility(!hidden);
        PuzzleDebugOverlay.Log(
            $"[{_objectId}] {(hidden ? "скрыт" : "показан")}",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    private void ApplyVisibility(bool visible)
    {
        foreach (var r in _renderers)
            if (r != null) r.enabled = visible;

        foreach (var c in _colliders)
            if (c != null) c.enabled = visible;
    }

    private void SyncIndexFromState(string state)
    {
        int index = System.Array.IndexOf(_statesCycle, state);
        _currentStateIndex = index >= 0 ? index : 0;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (string.IsNullOrEmpty(_objectId))
            _objectId = gameObject.name;
    }
#endif
}