using Mirror;
using UnityEngine;

/// <summary>
/// Объект, который можно осмотреть (и, возможно, собрать в инвентарь).
/// 
/// Реализует IFocusable — InteractionRaycaster подсвечивает его при наведении.
/// При нажатии E → InspectionController начинает осмотр.
/// При нажатии G (во время осмотра, если CanCollect) → PlayerInventory забирает предмет.
///
/// Сетевая часть:
/// - _isCollected (SyncVar) — когда предмет собран, скрывается у обоих игроков.
/// - Регистрируется в InteractableObjectRegistry → интегрируется с PuzzleManager.
///   При сборе состояние меняется на "collected", что может быть условием пазла.
///
/// НАСТРОЙКА НА СЦЕНЕ:
/// 1. Создай объект с моделью предмета
/// 2. Добавь InspectableObject + Collider (на Interactable Layer!)
/// 3. Добавь OutlineEffect на меш-дочерний объект с ГОЛУБЫМ материалом обводки
/// 4. Назначь ItemDefinition
/// 5. Задай уникальный _objectId
/// 6. Добавь NetworkIdentity
/// </summary>
public class InspectableObject : NetworkBehaviour, IFocusable
{
    [Header("Идентификация")]
    [SerializeField] private string _objectId;

    [Header("Данные предмета")]
    [SerializeField] private ItemDefinition _itemDefinition;

    [SyncVar(hook = nameof(OnCollectedChanged))]
    private bool _isCollected;

    // Кэш для восстановления видимости (если понадобится)
    private Renderer[] _renderers;
    private Collider[] _colliders;

    public string ObjectId => _objectId;
    public ItemDefinition ItemDefinition => _itemDefinition;
    public bool IsCollected => _isCollected;
    public bool CanCollect => _itemDefinition != null && _itemDefinition.CanCollect;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _colliders = GetComponentsInChildren<Collider>();
    }

    // ──────────────────────── СЕТЬ ────────────────────────

    public override void OnStartServer()
    {
        // Регистрируем в общем реестре — PuzzleManager может отслеживать состояние
        InteractableObjectRegistry.Register(_objectId, null);
        // null потому что это не InteractableObject, но ID занят.
        // Если нужна интеграция с пазлами, см. примечание в GUIDE.
    }

    public override void OnStartClient()
    {
        // Если предмет уже собран (late-join), скрываем
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

    // ──────────────────────── IFocusable ────────────────────────

    public void SetHighlight(bool enabled)
    {
        if (_isCollected) return;

        var outline = GetComponentInChildren<OutlineEffect>();
        outline?.SetHighlight(enabled);
    }

    // ──────────────────────── СБОР ────────────────────────

    /// <summary>
    /// Вызывается сервером (через PlayerInventory.CmdPickupItem).
    /// Помечает предмет как собранный — SyncVar-хук скроет его у всех клиентов.
    /// </summary>
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

    /// <summary>
    /// Скрывает/показывает объект: отключает рендереры и коллайдеры.
    /// НЕ отключает сам GameObject — Mirror продолжает отслеживать NetworkIdentity.
    /// </summary>
    private void SetVisible(bool visible)
    {
        foreach (var r in _renderers)
            r.enabled = visible;

        foreach (var c in _colliders)
            c.enabled = visible;
    }
}
