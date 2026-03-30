using Mirror;
using UnityEngine;

public class InspectableObject : NetworkBehaviour, IFocusable
{
    [Header("Идентификация")]
    [Tooltip("Уникальный ID. Если пусто — автогенерируется из имени GameObject.")]
    [SerializeField] private string _objectId;

    [Header("Данные предмета")]
    [SerializeField] private ItemDefinition _itemDefinition;

    [SyncVar(hook = nameof(OnCollectedChanged))]
    private bool _isCollected;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private OutlineEffect _outlineEffect;

    public string ObjectId => _objectId;
    public ItemDefinition ItemDefinition => _itemDefinition;
    public bool IsCollected => _isCollected;
    public bool CanCollect => _itemDefinition != null && _itemDefinition.CanCollect;

    private void Awake()
    {
        if (string.IsNullOrEmpty(_objectId))
            _objectId = gameObject.name;

        // includeInactive = true — объект может начинать скрытым
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _outlineEffect = GetComponentInChildren<OutlineEffect>(true);
    }

    public override void OnStartClient()
    {
        if (_isCollected)
            SetVisible(false);
    }

    public void SetHighlight(bool enabled)
    {
        if (_isCollected) return;
        _outlineEffect?.SetHighlight(enabled);
    }
    
    [Server]
    public void Collect()
    {
        _isCollected = true;
        PuzzleDebugOverlay.Log(
            $"[Inspectable] '{_objectId}' собран",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    private void OnCollectedChanged(bool _, bool isCollected)
    {
        SetVisible(!isCollected);

        if (isCollected)
            PuzzleDebugOverlay.Log($"[Inspectable] '{_objectId}' скрыт (собран)");
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in _renderers)
            if (r != null) r.enabled = visible;

        foreach (var c in _colliders)
            if (c != null) c.enabled = visible;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (string.IsNullOrEmpty(_objectId))
            _objectId = gameObject.name;
    }
#endif
}