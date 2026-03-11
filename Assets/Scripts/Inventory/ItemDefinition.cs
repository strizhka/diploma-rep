using UnityEngine;

/// <summary>
/// Определение предмета: что это, можно ли собрать, как выглядит при осмотре.
/// Создаётся как ScriptableObject: Create → Items → ItemDefinition.
///
/// Привязывается к InspectableObject на сцене.
/// Используется PlayerInventory и InventoryUI для отображения собранных предметов.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Items/ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Уникальный строковый ID. Должен совпадать с objectId на InspectableObject.")]
    public string ItemId;

    [Tooltip("Отображаемое имя в инвентаре")]
    public string DisplayName;

    [Header("Поведение")]
    [Tooltip("Можно ли положить в инвентарь. false = только осмотр.")]
    public bool CanCollect = true;

    [Header("Визуал")]
    [Tooltip("Префаб для осмотра в инвентаре и отображения в руках. " +
             "Должен быть чистой моделью без NetworkIdentity.")]
    public GameObject PreviewPrefab;

    [Tooltip("Иконка для UI инвентаря (опционально)")]
    public Sprite Icon;
}
