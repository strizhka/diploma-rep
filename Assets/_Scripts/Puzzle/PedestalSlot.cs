using Mirror;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Универсальный постамент. При F — спавнит фигурку через NetworkServer.Spawn().
/// Фигурка — полноценный InspectableObject (можно подобрать G).
/// Постамент занят пока фигурку не подберут.
///
/// НАСТРОЙКА:
/// 1. На постаменте: InteractableObject + PedestalSlot + NetworkIdentity + Collider
/// 2. Заполни _spawnPrefabs: ItemId → Prefab для каждой допустимой фигурки
/// 3. Префабы должны быть в Registered Spawnable Prefabs (NetworkManager)
/// 4. На префабе: InspectableObject + NetworkIdentity + Collider + OutlineEffect
/// 5. OnItemPlaced → PedestalMatcher.CheckMatch
/// </summary>
public class PedestalSlot : NetworkBehaviour
{
    [Header("Фигурки (ItemId → Префаб)")]
    [Tooltip("Какие предметы можно ставить и какой префаб спавнить для каждого.")]
    [SerializeField] private FigurineEntry[] _figurines;

    [System.Serializable]
    public class FigurineEntry
    {
        [Tooltip("ItemId из ItemDefinition")]
        public string ItemId;

        [Tooltip("Префаб с InspectableObject + NetworkIdentity")]
        public GameObject Prefab;
    }

    [Header("Спавн")]
    [Tooltip("Смещение от центра постамента")]
    [SerializeField] private Vector3 _spawnOffset = Vector3.up * 0.3f;

    [Tooltip("Поворот спавна")]
    [SerializeField] private Vector3 _spawnRotation = Vector3.zero;

    [Header("События")]
    public UnityEvent OnItemPlaced;
    public UnityEvent OnItemRemoved;

    // ──── Сетевое состояние ────

    [SyncVar]
    private string _placedItemId = "";

    [SyncVar]
    private uint _spawnedNetId;

    public string PlacedItemId => _placedItemId;
    public bool IsOccupied => !string.IsNullOrEmpty(_placedItemId);

    // ──────────────────────── РАЗМЕЩЕНИЕ ────────────────────────

    /// <summary>
    /// Поставить предмет. Вызывается из PlayerInventory.CmdPlaceOnPedestal.
    /// </summary>
    [Server]
    public bool TryPlace(string itemId)
    {
        if (IsOccupied)
        {
            PuzzleDebugOverlay.Log(
                $"[Pedestal] '{gameObject.name}' занят — сначала забери фигурку",
                PuzzleDebugOverlay.DebugLevel.Warning);
            return false;
        }

        // Ищем префаб
        GameObject prefab = FindPrefab(itemId);
        if (prefab == null)
        {
            PuzzleDebugOverlay.Log(
                $"[Pedestal] '{gameObject.name}': префаб для '{itemId}' не найден",
                PuzzleDebugOverlay.DebugLevel.Error);
            return false;
        }

        // Запоминаем
        _placedItemId = itemId;

        // Спавним
        Quaternion rotation = transform.rotation * Quaternion.Euler(_spawnRotation);
        var go = Instantiate(prefab, transform.position + _spawnOffset, rotation);
        NetworkServer.Spawn(go);

        var netIdentity = go.GetComponent<NetworkIdentity>();
        _spawnedNetId = netIdentity != null ? netIdentity.netId : 0;

        // Событие
        RpcNotifyPlaced();

        PuzzleDebugOverlay.Log(
            $"[Pedestal] '{gameObject.name}' ← '{itemId}' (netId={_spawnedNetId})",
            PuzzleDebugOverlay.DebugLevel.Ok);

        return true;
    }

    // ──────────────────────── АВТООЧИСТКА ────────────────────────

    /// <summary>
    /// Сервер проверяет: если фигурку подобрали — освобождаем постамент.
    /// </summary>
    private void Update()
    {
        if (!NetworkServer.active) return;
        if (!IsOccupied || _spawnedNetId == 0) return;

        if (NetworkServer.spawned.TryGetValue(_spawnedNetId, out var identity))
        {
            var inspectable = identity.GetComponent<InspectableObject>();
            if (inspectable != null && inspectable.IsCollected)
                ClearSlot();
        }
        else
        {
            // Объект уничтожен
            ClearSlot();
        }
    }

    [Server]
    private void ClearSlot()
    {
        string wasItem = _placedItemId;
        _placedItemId = "";
        _spawnedNetId = 0;

        RpcNotifyRemoved();

        PuzzleDebugOverlay.Log(
            $"[Pedestal] '{gameObject.name}': '{wasItem}' забрана, постамент свободен");
    }

    // ──────────────────────── ПОИСК ПРЕФАБА ────────────────────────

    private GameObject FindPrefab(string itemId)
    {
        if (_figurines == null) return null;

        foreach (var entry in _figurines)
        {
            if (entry.ItemId == itemId && entry.Prefab != null)
                return entry.Prefab;
        }

        return null;
    }

    // ──────────────────────── RPC ────────────────────────

    [ClientRpc]
    private void RpcNotifyPlaced()
    {
        OnItemPlaced?.Invoke();
    }

    [ClientRpc]
    private void RpcNotifyRemoved()
    {
        OnItemRemoved?.Invoke();
    }
}