using UnityEngine;

public class SimpleInspectable : MonoBehaviour, IFocusable
{
    [Header("Данные предмета")]
    [SerializeField] private ItemDefinition _itemDefinition;

    private OutlineEffect _outline;

    public ItemDefinition ItemDefinition => _itemDefinition;
    public bool CanCollect => false;
    public bool IsCollected => false;
    public string ObjectId => gameObject.name;

    private void Awake()
    {
        _outline = GetComponentInChildren<OutlineEffect>(true);
    }

    public void SetHighlight(bool enabled)
    {
        _outline?.SetHighlight(enabled);
    }
}
