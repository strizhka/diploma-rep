using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _inventoryCanvas;
    [SerializeField] private Image _fadeImage;
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private GameObject _slotPrefab;

    [Header("Иконки")]
    [SerializeField] private Sprite _emptySlotIcon;

    [Header("Визуал")]
    [SerializeField] private Color _selectedColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color _emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    [SerializeField] private Color _heldMarkerColor = new Color(0.2f, 0.8f, 0.3f, 1f);

    [Header("Анимация")]
    [SerializeField] private float _fadeAlpha = 0.8f;
    [SerializeField] private float _fadeDuration = 0.25f;
    
    private bool _isOpen;
    private int _selectedIndex;
    private readonly List<SlotEntry> _slots = new();

    private PlayerInventory _inventory;
    private InspectionController _inspectionController;

    private const string TweenFadeId = "InventoryFade";

    public bool IsOpen => _isOpen;

    private struct SlotEntry
    {
        public GameObject SlotObject;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI Label;
        public Image SelectionBorder;
        public bool IsEmptySlot;
    }

    public void Initialize(PlayerInventory inventory, InspectionController inspectionController)
    {
        _inventory = inventory;
        _inspectionController = inspectionController;

        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(false);

        if (_inventory != null)
            _inventory.OnInventoryChanged += RefreshSlots;
    }

    private void OnDestroy()
    {
        DOTween.Kill(TweenFadeId);

        if (_inventory != null)
            _inventory.OnInventoryChanged -= RefreshSlots;
    }

    public void Open()
    {
        if (_isOpen || _inventory == null) return;

        _isOpen = true;
        _selectedIndex = 0;

        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(true);
        
        if (_fadeImage != null)
        {
            DOTween.Kill(TweenFadeId);
            _fadeImage.DOFade(_fadeAlpha, _fadeDuration).SetId(TweenFadeId).SetUpdate(true);
        }

        RebuildSlots();
        UpdateSelection();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PuzzleDebugOverlay.Log("[InventoryUI] Открыт");
    }

    public void Close()
    {
        if (!_isOpen) return;

        if (_inspectionController != null && _inspectionController.IsActive)
            _inspectionController.StopInspection();

        _isOpen = false;
        
        if (_fadeImage != null)
        {
            DOTween.Kill(TweenFadeId);
            _fadeImage.DOFade(0f, _fadeDuration)
                .SetId(TweenFadeId)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (_inventoryCanvas != null)
                        _inventoryCanvas.SetActive(false);
                });
        }
        else if (_inventoryCanvas != null)
        {
            _inventoryCanvas.SetActive(false);
        }

        ClearSlots();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PuzzleDebugOverlay.Log("[InventoryUI] Закрыт");
    }
    
    public void RestoreCanvasAfterInspection()
    {
        if (!_isOpen) return;

        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(true);
    }

    public void Navigate(int direction)
    {
        if (!_isOpen || _slots.Count == 0) return;
        if (_inspectionController != null && _inspectionController.IsActive) return;

        _selectedIndex = Mathf.Clamp(_selectedIndex + direction, 0, _slots.Count - 1);
        UpdateSelection();
    }

    public void InspectSelected()
    {
        if (!_isOpen || _inspectionController == null) return;
        if (_selectedIndex < 0 || _selectedIndex >= _slots.Count) return;

        if (_inspectionController.IsActive)
        {
            _inspectionController.StopInspection();
            if (_inventoryCanvas != null)
                _inventoryCanvas.SetActive(true);
            return;
        }

        var slot = _slots[_selectedIndex];
        if (slot.IsEmptySlot) return;

        var itemDef = _inventory.GetItemAt(_selectedIndex);
        if (itemDef == null || itemDef.PreviewPrefab == null) return;
        
        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(false);

        _inspectionController.StartInventoryInspection(itemDef);
    }

    public void EquipSelected()
    {
        if (!_isOpen || _inventory == null) return;

        if (_inspectionController != null && _inspectionController.IsActive)
            _inspectionController.StopInspection();

        if (_selectedIndex < 0 || _selectedIndex >= _slots.Count) return;

        var slot = _slots[_selectedIndex];

        if (slot.IsEmptySlot)
        {
            _inventory.EquipItem("");
            PuzzleDebugOverlay.Log("[InventoryUI] Руки опустели");
        }
        else
        {
            string itemId = _inventory.Items[_selectedIndex];
            _inventory.EquipItem(itemId);
            PuzzleDebugOverlay.Log($"[InventoryUI] Экипирован: {itemId}");
        }
        
        Close();
    }

    private void RebuildSlots()
    {
        ClearSlots();

        for (int i = 0; i < _inventory.Count; i++)
        {
            var itemDef = _inventory.GetItemAt(i);
            CreateSlot(itemDef, isEmptySlot: false);
        }
        
        CreateSlot(null, isEmptySlot: true);
    }

    private void CreateSlot(ItemDefinition itemDef, bool isEmptySlot)
    {
        if (_slotPrefab == null || _slotContainer == null) return;

        var go = Instantiate(_slotPrefab, _slotContainer);
        go.SetActive(true);

        var entry = new SlotEntry
        {
            SlotObject = go,
            Background = go.GetComponent<Image>(),
            Label = go.GetComponentInChildren<TextMeshProUGUI>(),
            IsEmptySlot = isEmptySlot
        };
        
        var iconTransform = go.transform.Find("Icon");
        if (iconTransform != null)
            entry.Icon = iconTransform.GetComponent<Image>();

        var borderTransform = go.transform.Find("SelectionBorder");
        if (borderTransform != null)
            entry.SelectionBorder = borderTransform.GetComponent<Image>();

        if (isEmptySlot)
        {
            if (entry.Icon != null)
            {
                if (_emptySlotIcon != null)
                {
                    entry.Icon.sprite = _emptySlotIcon;
                    entry.Icon.color = new Color(1f, 1f, 1f, 0.5f);
                    entry.Icon.gameObject.SetActive(true);
                }
                else
                {
                    entry.Icon.gameObject.SetActive(false);
                }
            }

            if (entry.Label != null)
                entry.Label.text = "Пустые руки";
        }
        else
        {
            if (entry.Icon != null)
            {
                if (itemDef != null && itemDef.Icon != null)
                {
                    entry.Icon.sprite = itemDef.Icon;
                    entry.Icon.color = Color.white;
                    entry.Icon.gameObject.SetActive(true);
                }
                else
                {
                    entry.Icon.gameObject.SetActive(false);
                }
            }

            if (entry.Label != null)
                entry.Label.text = itemDef != null ? itemDef.DisplayName : "???";
        }

        _slots.Add(entry);
    }

    private void ClearSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot.SlotObject != null)
                Destroy(slot.SlotObject);
        }
        _slots.Clear();
    }
    
    private void SetSlotsVisible(bool visible)
    {
        if (_slotContainer != null)
            _slotContainer.gameObject.SetActive(visible);
    }

    private void RefreshSlots()
    {
        if (!_isOpen) return;

        int prevIndex = _selectedIndex;
        RebuildSlots();
        _selectedIndex = Mathf.Clamp(prevIndex, 0, Mathf.Max(0, _slots.Count - 1));
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            bool isSelected = (i == _selectedIndex);
            
            if (slot.Background != null)
            {
                if (isSelected)
                    slot.Background.color = _selectedColor;
                else if (slot.IsEmptySlot)
                    slot.Background.color = _emptySlotColor;
                else
                    slot.Background.color = _normalColor;
            }
            
            if (slot.SelectionBorder != null)
                slot.SelectionBorder.gameObject.SetActive(isSelected);
            
            if (slot.Label != null && !slot.IsEmptySlot)
            {
                string itemId = _inventory.Items[i];
                bool isHeld = itemId == _inventory.HeldItemId;

                var itemDef = _inventory.GetItemAt(i);
                string name = itemDef != null ? itemDef.DisplayName : itemId;

                slot.Label.text = isHeld ? $"● {name}" : name;
                
                if (isHeld && !isSelected && slot.Background != null)
                    slot.Background.color = _heldMarkerColor;
            }
        }
    }
}