using Mirror;
using UnityEngine;

public class InspectableObject : NetworkBehaviour, IFocusable
{
    [Header("Идентификация")]
    [SerializeField] private string _objectId;

    [Header("Данные предмета")]
    [SerializeField] private ItemDefinition _itemDefinition;

    [SyncVar(hook = nameof(OnCollectedChanged))]
    private bool _isCollected;

    private Renderer[] _renderers;
    private Collider[] _colliders;

    public string ObjectId => _objectId;
    public ItemDefinition ItemDefinition => _itemDefinition;
    public bool IsCollected => _isCollected;
    public bool CanCollect => _itemDefinition != null && _itemDefinition.CanCollect;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    public override void OnStartServer()
    {
        InteractableObjectRegistry.Register(_objectId, null);
    }

    public override void OnStartClient()
    {
        if (_isCollected)
            SetVisible(false);
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
        if (_isCollected) return;

        var outline = GetComponentInChildren<OutlineEffect>(true);
        outline?.SetHighlight(enabled);
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
}