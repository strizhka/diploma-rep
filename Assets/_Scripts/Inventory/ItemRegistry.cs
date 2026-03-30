using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр всех предметов в игре. Один экземпляр на проект.
/// Create → Items → ItemRegistry, затем перетяни все ItemDefinition в массив.
///
/// Используется PlayerInventory и InventoryUI для получения ItemDefinition по ID.
/// Ссылка на реестр задаётся через инспектор на PlayerInventory.
/// </summary>
[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Items/ItemRegistry")]
public class ItemRegistry : ScriptableObject
{
    [SerializeField] private ItemDefinition[] _items;

    private Dictionary<string, ItemDefinition> _lookup;

    /// <summary>
    /// Получить ItemDefinition по ID. Возвращает null если не найден.
    /// </summary>
    public ItemDefinition Get(string itemId)
    {
        EnsureLookup();

        if (_lookup.TryGetValue(itemId, out var def))
            return def;

        Debug.LogWarning($"[ItemRegistry] Предмет '{itemId}' не найден в реестре.");
        return null;
    }

    public bool Has(string itemId)
    {
        EnsureLookup();
        return _lookup.ContainsKey(itemId);
    }

    public IEnumerable<ItemDefinition> GetAll()
    {
        EnsureLookup();
        return _lookup.Values;
    }

    private void EnsureLookup()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<string, ItemDefinition>();
        foreach (var item in _items)
        {
            if (item == null) continue;

            if (!_lookup.TryAdd(item.ItemId, item))
                Debug.LogError($"[ItemRegistry] Дублирующийся ItemId: '{item.ItemId}'");
        }
    }

    // Сбрасываем кэш при перезагрузке SO в редакторе
    private void OnValidate()
    {
        _lookup = null;
    }
}
