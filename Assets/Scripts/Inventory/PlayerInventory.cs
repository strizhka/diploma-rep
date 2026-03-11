using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Инвентарь игрока. NetworkBehaviour — синхронизирует список предметов и экипированный предмет.
///
/// Механика:
/// - _items: SyncList всех собранных предметов (по ItemId)
/// - _heldItemId: SyncVar — какой предмет в руках ("" = пустые руки)
/// - При сборе: если руки пусты — автоматически экипируется
///
/// ЗАВИСИМОСТИ:
/// - ItemRegistry (SO) — назначается в инспекторе для lookup ItemDefinition по ID
/// - InspectableObject на сцене — сервер вызывает Collect() при сборе
///
/// НАСТРОЙКА:
/// 1. Добавь на Player-префаб
/// 2. Назначь ItemRegistry в инспекторе
/// 3. (Опционально) Назначь _holdPoint — дочерний Transform для отображения предмета в руках
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Данные")]
    [SerializeField] private ItemRegistry _itemRegistry;

    [Header("Визуал (опционально)")]
    [Tooltip("Точка крепления предмета в руках. Дочерний Transform камеры или Head.")]
    [SerializeField] private Transform _holdPoint;

    // ──── Синхронизация ────

    /// <summary>
    /// Список ID всех собранных предметов. Синхронизируется Mirror.
    /// </summary>
    private readonly SyncList<string> _items = new();

    /// <summary>
    /// ID предмета в руках. "" = пустые руки.
    /// </summary>
    [SyncVar(hook = nameof(OnHeldItemChanged))]
    private string _heldItemId = "";

    // Локальный экземпляр модели в руках
    private GameObject _heldVisualInstance;

    // ──── Публичный API ────

    public IReadOnlyList<string> Items => _items;
    public string HeldItemId => _heldItemId;
    public int Count => _items.Count;
    public ItemRegistry Registry => _itemRegistry;

    /// <summary>
    /// Вызывается при изменении содержимого инвентаря (для обновления UI).
    /// </summary>
    public event Action OnInventoryChanged;

    // ──────────────────────── LIFECYCLE ────────────────────────

    public override void OnStartClient()
    {
        base.OnStartClient();

        // SyncList callback — вызывается при любом изменении списка
        _items.Callback += OnItemsListChanged;

        // Если late-join и в руках уже есть предмет — показать
        if (isLocalPlayer && !string.IsNullOrEmpty(_heldItemId))
            UpdateHeldVisual(_heldItemId);
    }

    public override void OnStopClient()
    {
        _items.Callback -= OnItemsListChanged;
        DestroyHeldVisual();
        base.OnStopClient();
    }

    // ──────────────────────── СБОР ПРЕДМЕТА ────────────────────────

    /// <summary>
    /// Подобрать предмет со сцены. Вызывается локальным игроком.
    /// Отправляет команду на сервер с NetworkIdentity объекта.
    /// </summary>
    public void PickupItem(InspectableObject obj)
    {
        if (obj == null || !obj.CanCollect || obj.IsCollected) return;

        CmdPickupItem(obj.netIdentity, obj.ItemDefinition.ItemId);
    }

    [Command]
    private void CmdPickupItem(NetworkIdentity objectIdentity, string itemId)
    {
        if (objectIdentity == null)
        {
            Debug.LogError("[Inventory] NetworkIdentity объекта не найден.");
            return;
        }

        var inspectable = objectIdentity.GetComponent<InspectableObject>();
        if (inspectable == null || inspectable.IsCollected)
        {
            Debug.LogWarning($"[Inventory] Объект '{itemId}' недоступен для сбора.");
            return;
        }

        // Помечаем объект как собранный (SyncVar скроет у всех клиентов)
        inspectable.Collect();

        // Добавляем в инвентарь
        _items.Add(itemId);

        // Если руки пусты — автоматически экипируем
        if (string.IsNullOrEmpty(_heldItemId))
            _heldItemId = itemId;

        PuzzleDebugOverlay.Log(
            $"[Inventory] Игрок подобрал '{itemId}'. Всего: {_items.Count}",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    // ──────────────────────── ЭКИПИРОВКА ────────────────────────

    /// <summary>
    /// Взять предмет из инвентаря в руки. "" = убрать предмет из рук (пустые руки).
    /// </summary>
    public void EquipItem(string itemId)
    {
        // Валидация: предмет должен быть в инвентаре (или "" для пустых рук)
        if (!string.IsNullOrEmpty(itemId) && !_items.Contains(itemId))
        {
            Debug.LogWarning($"[Inventory] Предмет '{itemId}' не в инвентаре.");
            return;
        }

        CmdEquipItem(itemId);
    }

    [Command]
    private void CmdEquipItem(string itemId)
    {
        _heldItemId = itemId;
        PuzzleDebugOverlay.Log($"[Inventory] Экипирован: '{(string.IsNullOrEmpty(itemId) ? "пусто" : itemId)}'");
    }

    // ──────────────────────── УТИЛИТЫ ────────────────────────

    /// <summary>
    /// Проверить наличие предмета в инвентаре.
    /// </summary>
    public bool HasItem(string itemId)
    {
        return _items.Contains(itemId);
    }

    /// <summary>
    /// Получить ItemDefinition по индексу в инвентаре.
    /// </summary>
    public ItemDefinition GetItemAt(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        return _itemRegistry?.Get(_items[index]);
    }

    /// <summary>
    /// Получить ItemDefinition по ID.
    /// </summary>
    public ItemDefinition GetItemDefinition(string itemId)
    {
        return _itemRegistry?.Get(itemId);
    }

    // ──────────────────────── ВИЗУАЛ В РУКАХ ────────────────────────

    private void OnHeldItemChanged(string oldId, string newId)
    {
        if (isLocalPlayer)
            UpdateHeldVisual(newId);

        OnInventoryChanged?.Invoke();
    }

    private void UpdateHeldVisual(string itemId)
    {
        DestroyHeldVisual();

        if (string.IsNullOrEmpty(itemId) || _holdPoint == null) return;

        var def = _itemRegistry?.Get(itemId);
        if (def == null || def.PreviewPrefab == null) return;

        _heldVisualInstance = Instantiate(def.PreviewPrefab, _holdPoint);
        _heldVisualInstance.transform.localPosition = Vector3.zero;
        _heldVisualInstance.transform.localRotation = Quaternion.identity;

        // Убираем сетевые компоненты с визуальной копии
        foreach (var netId in _heldVisualInstance.GetComponentsInChildren<Mirror.NetworkIdentity>())
            Destroy(netId);

        PuzzleDebugOverlay.Log($"[Inventory] Визуал в руках: '{itemId}'");
    }

    private void DestroyHeldVisual()
    {
        if (_heldVisualInstance != null)
        {
            Destroy(_heldVisualInstance);
            _heldVisualInstance = null;
        }
    }

    // ──────────────────────── SYNC CALLBACKS ────────────────────────

    private void OnItemsListChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        OnInventoryChanged?.Invoke();
    }
}
