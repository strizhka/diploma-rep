using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Данные")]
    [SerializeField] private ItemRegistry _itemRegistry;

    [Header("Визуал")]
    [SerializeField] private Transform _holdPoint;
    [SerializeField] private Vector3 _holdOffset = new Vector3(0.35f, -0.3f, 0.5f);
    [SerializeField] private Vector3 _holdRotation = Vector3.zero;

    private readonly SyncList<string> _items = new();

    [SyncVar(hook = nameof(OnHeldItemChanged))]
    private string _heldItemId = "";

    private GameObject _heldVisualInstance;
    private bool _visualHidden;

    public IReadOnlyList<string> Items => _items;
    public string HeldItemId => _heldItemId;
    public int Count => _items.Count;
    public ItemRegistry Registry => _itemRegistry;
    public Transform HoldPoint => _holdPoint;

    public event Action OnInventoryChanged;

    // ──────────────────────── LIFECYCLE ────────────────────────

    public override void OnStartClient()
    {
        base.OnStartClient();
        _items.Callback += OnItemsListChanged;

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

        inspectable.Collect();
        _items.Add(itemId);

        if (string.IsNullOrEmpty(_heldItemId))
            _heldItemId = itemId;

        PuzzleDebugOverlay.Log(
            $"[Inventory] Игрок подобрал '{itemId}'. Всего: {_items.Count}",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    // ──────────────────────── ПРИМЕНЕНИЕ ПРЕДМЕТА ────────────────────────

    public void ApplyItemToReceiver(ItemReceiver receiver)
    {
        // Подробное логирование каждой проверки — найти точку отказа
        if (receiver == null)
        {
            PuzzleDebugOverlay.Log("[Apply] ОТКАЗ: receiver == null", PuzzleDebugOverlay.DebugLevel.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_heldItemId))
        {
            PuzzleDebugOverlay.Log("[Apply] ОТКАЗ: руки пусты", PuzzleDebugOverlay.DebugLevel.Warning);
            return;
        }

        if (receiver.IsFilled)
        {
            PuzzleDebugOverlay.Log("[Apply] ОТКАЗ: receiver уже заполнен", PuzzleDebugOverlay.DebugLevel.Warning);
            return;
        }

        if (receiver.RequiredItemId != _heldItemId)
        {
            PuzzleDebugOverlay.Log(
                $"[Apply] ОТКАЗ: требуется '{receiver.RequiredItemId}', в руках '{_heldItemId}'",
                PuzzleDebugOverlay.DebugLevel.Warning);
            return;
        }

        PuzzleDebugOverlay.Log(
            $"[Apply] Отправляю Command: '{_heldItemId}' → receiver '{receiver.name}'",
            PuzzleDebugOverlay.DebugLevel.Ok);

        CmdApplyItem(receiver.netIdentity, _heldItemId);
    }

    [Command]
    private void CmdApplyItem(NetworkIdentity receiverIdentity, string itemId)
    {
        PuzzleDebugOverlay.Log($"[Apply:Server] Command получен: itemId='{itemId}'");

        if (receiverIdentity == null)
        {
            Debug.LogError("[Apply:Server] NetworkIdentity получателя == null.");
            return;
        }

        var receiver = receiverIdentity.GetComponent<ItemReceiver>();
        if (receiver == null)
        {
            Debug.LogError("[Apply:Server] ItemReceiver не найден на объекте.");
            return;
        }

        if (!_items.Contains(itemId))
        {
            PuzzleDebugOverlay.Log(
                $"[Apply:Server] ОТКАЗ: '{itemId}' нет в _items. Содержимое: [{string.Join(", ", _items)}]",
                PuzzleDebugOverlay.DebugLevel.Error);
            return;
        }

        bool success = receiver.TryApply(itemId);
        PuzzleDebugOverlay.Log($"[Apply:Server] TryApply = {success}");

        if (!success) return;

        if (receiver.ShouldConsume)
        {
            ConsumeItem(itemId);
            PuzzleDebugOverlay.Log(
                $"[Apply:Server] '{itemId}' израсходован. Осталось: [{string.Join(", ", _items)}]",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
    }

    [Server]
    private void ConsumeItem(string itemId)
    {
        bool removed = _items.Remove(itemId);
        PuzzleDebugOverlay.Log($"[Consume] Remove('{itemId}') = {removed}");

        if (_heldItemId == itemId)
            _heldItemId = "";
    }

    // ──────────────────────── ПОСТАМЕНТ ────────────────────────

    /// <summary>
    /// Поставить предмет из рук на PedestalSlot.
    /// </summary>
    public void PlaceOnPedestal(PedestalSlot pedestal)
    {
        if (pedestal == null) return;
        if (string.IsNullOrEmpty(_heldItemId)) return;

        CmdPlaceOnPedestal(pedestal.netIdentity, _heldItemId);
    }

    [Command]
    private void CmdPlaceOnPedestal(NetworkIdentity pedestalIdentity, string itemId)
    {
        if (pedestalIdentity == null) return;

        var pedestal = pedestalIdentity.GetComponent<PedestalSlot>();
        if (pedestal == null) return;

        if (!_items.Contains(itemId))
        {
            PuzzleDebugOverlay.Log($"[Pedestal] '{itemId}' нет в инвентаре",
                PuzzleDebugOverlay.DebugLevel.Error);
            return;
        }

        bool placed = pedestal.TryPlace(itemId);
        if (!placed) return;

        ConsumeItem(itemId);
    }

    // ──────────────────────── ЭКИПИРОВКА ────────────────────────

    public void EquipItem(string itemId)
    {
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
        PuzzleDebugOverlay.Log(
            $"[Inventory] Экипирован: '{(string.IsNullOrEmpty(itemId) ? "пусто" : itemId)}'");
    }

    // ──────────────────────── УТИЛИТЫ ────────────────────────

    public bool HasItem(string itemId) => _items.Contains(itemId);

    public ItemDefinition GetItemAt(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        return _itemRegistry?.Get(_items[index]);
    }

    public ItemDefinition GetItemDefinition(string itemId)
    {
        return _itemRegistry?.Get(itemId);
    }

    // ──────────────────────── ВИЗУАЛ В РУКАХ ────────────────────────

    public void HideHeldVisual()
    {
        _visualHidden = true;
        if (_heldVisualInstance != null)
            _heldVisualInstance.SetActive(false);
    }

    public void ShowHeldVisual()
    {
        _visualHidden = false;
        UpdateHeldVisual(_heldItemId);
    }

    private void OnHeldItemChanged(string oldId, string newId)
    {
        if (isLocalPlayer && !_visualHidden)
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
        _heldVisualInstance.transform.localPosition = _holdOffset;
        _heldVisualInstance.transform.localRotation = Quaternion.Euler(_holdRotation);

        foreach (var netId in _heldVisualInstance.GetComponentsInChildren<NetworkIdentity>())
            Destroy(netId);

        if (_visualHidden)
            _heldVisualInstance.SetActive(false);

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

    private void OnItemsListChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        OnInventoryChanged?.Invoke();
    }
}