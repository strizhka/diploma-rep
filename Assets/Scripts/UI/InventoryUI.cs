using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI экрана инвентаря. Полностью локальный — не синхронизируется.
///
/// Открывается по B, закрывается по Q.
/// Стрелками ← → выбирается слот, E = осмотр, G = экипировать.
/// Последний слот — всегда «Пустые руки».
///
/// НАСТРОЙКА:
/// 1. Создай Canvas (ScreenSpace-Overlay, sortingOrder > 10)
/// 2. Дочерний Image — фон затемнения (чёрный, alpha задаётся в коде)
/// 3. Дочерний HorizontalLayoutGroup — контейнер для слотов (_slotContainer)
/// 4. Создай префаб слота (_slotPrefab):
///    - Image (фон слота)
///    - Дочерний TextMeshProUGUI (имя предмета)
///    - Дочерний Image "SelectionBorder" (рамка выделения, по умолчанию неактивна)
/// 5. Добавь InventoryUI на Player-префаб, назначь ссылки
/// 6. Canvas по умолчанию неактивен (SetActive false)
///
/// ЗАВИСИМОСТИ:
/// - PlayerInventory (на том же игроке)
/// - InspectionController (для осмотра из инвентаря)
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _inventoryCanvas;
    [SerializeField] private Image _fadeImage;
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private GameObject _slotPrefab;

    [Header("Визуал")]
    [SerializeField] private Color _selectedColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color _emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    [SerializeField] private float _fadeAlpha = 0.8f;

    // ──── Состояние ────
    private bool _isOpen;
    private int _selectedIndex;
    private readonly List<SlotEntry> _slots = new();

    // Ссылки (инициализируются из PlayerController)
    private PlayerInventory _inventory;
    private InspectionController _inspectionController;

    public bool IsOpen => _isOpen;

    private struct SlotEntry
    {
        public GameObject SlotObject;
        public Image Background;
        public TextMeshProUGUI Label;
        public Image SelectionBorder;
        public bool IsEmptySlot; // последний слот "Пустые руки"
    }

    // ──────────────────────── INIT ────────────────────────

    /// <summary>
    /// Инициализация. Вызывается из PlayerController.OnStartLocalPlayer().
    /// </summary>
    public void Initialize(PlayerInventory inventory, InspectionController inspectionController)
    {
        _inventory = inventory;
        _inspectionController = inspectionController;

        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(false);

        // Подписка на изменения инвентаря для обновления UI
        if (_inventory != null)
            _inventory.OnInventoryChanged += RefreshSlots;
    }

    private void OnDestroy()
    {
        if (_inventory != null)
            _inventory.OnInventoryChanged -= RefreshSlots;
    }

    // ──────────────────────── ОТКРЫТЬ / ЗАКРЫТЬ ────────────────────────

    public void Open()
    {
        if (_isOpen || _inventory == null) return;

        _isOpen = true;
        _selectedIndex = 0;

        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(true);

        // Затемнение
        if (_fadeImage != null)
        {
            var c = _fadeImage.color;
            c.a = _fadeAlpha;
            _fadeImage.color = c;
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

        // Если сейчас идёт осмотр из инвентаря — сначала закрыть его
        if (_inspectionController != null && _inspectionController.IsActive)
            _inspectionController.StopInspection();

        _isOpen = false;

        if (_inventoryCanvas != null)
            _inventoryCanvas.SetActive(false);

        ClearSlots();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PuzzleDebugOverlay.Log("[InventoryUI] Закрыт");
    }

    // ──────────────────────── НАВИГАЦИЯ ────────────────────────

    /// <summary>
    /// Переместить выделение. direction: -1 = влево, +1 = вправо.
    /// </summary>
    public void Navigate(int direction)
    {
        if (!_isOpen || _slots.Count == 0) return;

        // Если идёт осмотр из инвентаря — игнорируем навигацию
        if (_inspectionController != null && _inspectionController.IsActive) return;

        _selectedIndex = Mathf.Clamp(_selectedIndex + direction, 0, _slots.Count - 1);
        UpdateSelection();
    }

    /// <summary>
    /// Осмотреть выделенный предмет (E).
    /// </summary>
    public void InspectSelected()
    {
        if (!_isOpen || _inspectionController == null) return;
        if (_selectedIndex < 0 || _selectedIndex >= _slots.Count) return;

        // Если осмотр уже активен — закрываем его (toggle)
        if (_inspectionController.IsActive)
        {
            _inspectionController.StopInspection();
            return;
        }

        var slot = _slots[_selectedIndex];
        if (slot.IsEmptySlot) return; // Пустой слот нельзя осмотреть

        var itemDef = _inventory.GetItemAt(_selectedIndex);
        if (itemDef == null || itemDef.PreviewPrefab == null) return;

        _inspectionController.StartInventoryInspection(itemDef);
    }

    /// <summary>
    /// Экипировать выделенный предмет (G).
    /// На слоте "Пустые руки" — убирает предмет из рук.
    /// </summary>
    public void EquipSelected()
    {
        if (!_isOpen || _inventory == null) return;

        // Если идёт осмотр — закрываем его сначала
        if (_inspectionController != null && _inspectionController.IsActive)
            _inspectionController.StopInspection();

        if (_selectedIndex < 0 || _selectedIndex >= _slots.Count) return;

        var slot = _slots[_selectedIndex];

        if (slot.IsEmptySlot)
        {
            // Пустые руки
            _inventory.EquipItem("");
            PuzzleDebugOverlay.Log("[InventoryUI] Руки опустели");
        }
        else
        {
            string itemId = _inventory.Items[_selectedIndex];
            _inventory.EquipItem(itemId);
            PuzzleDebugOverlay.Log($"[InventoryUI] Экипирован: {itemId}");
        }

        // Обновляем подсветку (экипированный может подсвечиваться иначе)
        UpdateSelection();
    }

    // ──────────────────────── ПОСТРОЕНИЕ СЛОТОВ ────────────────────────

    private void RebuildSlots()
    {
        ClearSlots();

        // Создаём слот для каждого предмета
        for (int i = 0; i < _inventory.Count; i++)
        {
            var itemDef = _inventory.GetItemAt(i);
            string label = itemDef != null ? itemDef.DisplayName : _inventory.Items[i];
            CreateSlot(label, isEmptySlot: false);
        }

        // Последний слот — «Пустые руки»
        CreateSlot("Пустые руки", isEmptySlot: true);
    }

    private void CreateSlot(string labelText, bool isEmptySlot)
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

        // Ищем рамку выделения (дочерний Image с именем "SelectionBorder")
        var borderTransform = go.transform.Find("SelectionBorder");
        if (borderTransform != null)
            entry.SelectionBorder = borderTransform.GetComponent<Image>();

        if (entry.Label != null)
            entry.Label.text = labelText;

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

            // Фон
            if (slot.Background != null)
            {
                if (isSelected)
                    slot.Background.color = _selectedColor;
                else if (slot.IsEmptySlot)
                    slot.Background.color = _emptySlotColor;
                else
                    slot.Background.color = _normalColor;
            }

            // Рамка
            if (slot.SelectionBorder != null)
                slot.SelectionBorder.gameObject.SetActive(isSelected);

            // Пометка экипированного
            if (slot.Label != null && !slot.IsEmptySlot)
            {
                string itemId = _inventory.Items[i];
                bool isHeld = itemId == _inventory.HeldItemId;

                // Добавляем маркер к имени
                var itemDef = _inventory.GetItemAt(i);
                string name = itemDef != null ? itemDef.DisplayName : itemId;
                slot.Label.text = isHeld ? $"[В руках] {name}" : name;
            }
        }
    }
}
