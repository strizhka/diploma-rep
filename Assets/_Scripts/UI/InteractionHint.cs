using TMPro;
using UnityEngine;

/// <summary>
/// Контекстная подсказка под прицелом.
///
/// Два режима:
///   1) Свободный — подсказка зависит от IFocusable в фокусе (E/F/G).
///   2) Режим осмотра — независимо от фокуса показываем «[Q] Выход», и «[G] Взять»
///      если осматриваемый предмет CanCollect.
///
/// Оптимизация: GetComponent дёргается только при смене фокуса.
/// </summary>
public class InteractionHint : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private GameObject _hintPanel;

    [Header("Ссылки")]
    [SerializeField] private InteractionRaycaster _raycaster;
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private InspectionController _inspection;

    [Header("Тексты — свободный режим")]
    [SerializeField] private string _interactHint = "[E] Взаимодействовать";
    [SerializeField] private string _inspectHint = "[E] Осмотреть";
    [SerializeField] private string _collectHint = "[G] Подобрать";
    [SerializeField] private string _phoneHint = "[E] Телефон";
    [SerializeField] private string _codeButtonHint = "[E] Нажать";
    [SerializeField] private string _applyItemHint = "[F] Применить";
    [SerializeField] private string _lockedHint = "Заблокировано";

    [Header("Тексты — режим осмотра")]
    [SerializeField] private string _inspectExitHint = "[Q] Выход";
    [SerializeField] private string _inspectTakeHint = "[G] Взять";
    [SerializeField] private string _hintsSeparator = "\n";

    // Кэш компонентов текущего фокуса
    private IFocusable _lastFocus;
    private ItemReceiver _cachedReceiver;
    private PedestalSlot _cachedPedestal;

    private void Awake()
    {
        if (_raycaster == null) _raycaster = GetComponent<InteractionRaycaster>();
        if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
        if (_inspection == null) _inspection = GetComponent<InspectionController>();
        if (_hintPanel != null) _hintPanel.SetActive(false);
    }

    private void Update()
    {
        if (_hintText == null) return;

        // Приоритет: режим осмотра перекрывает обычные подсказки
        if (_inspection != null && _inspection.IsActive)
        {
            ShowInspectionHints();
            return;
        }

        ShowFocusHints();
    }

    // ──────────────────────── РЕЖИМ ОСМОТРА ────────────────────────

    private void ShowInspectionHints()
    {
        bool canCollect = _inspection.CurrentWorldObject != null
                          && _inspection.CurrentWorldObject.CanCollect;

        string hint = canCollect
            ? $"{_inspectExitHint}{_hintsSeparator}{_inspectTakeHint}"
            : _inspectExitHint;

        SetHint(hint);
    }

    // ──────────────────────── СВОБОДНЫЙ РЕЖИМ ────────────────────────

    private void ShowFocusHints()
    {
        if (_raycaster == null) { Hide(); return; }

        var focus = _raycaster.CurrentFocus;

        if (!ReferenceEquals(focus, _lastFocus))
        {
            _lastFocus = focus;
            CacheComponents(focus);
        }

        if (focus == null) { Hide(); return; }

        string hint = GetFocusHint(focus);
        if (string.IsNullOrEmpty(hint)) { Hide(); return; }

        SetHint(hint);
    }

    private void CacheComponents(IFocusable focus)
    {
        _cachedReceiver = null;
        _cachedPedestal = null;

        if (focus is Component comp)
        {
            _cachedReceiver = comp.GetComponent<ItemReceiver>();
            _cachedPedestal = comp.GetComponent<PedestalSlot>();
        }
    }

    private string GetFocusHint(IFocusable focus)
    {
        switch (focus)
        {
            case PhoneController phone:
                return phone.State switch
                {
                    PhoneController.PhoneState.Idle => _phoneHint,
                    PhoneController.PhoneState.Ringing => "[E] Снять трубку",
                    PhoneController.PhoneState.Calling => "[E] Отмена",
                    PhoneController.PhoneState.Connected => "[E] Положить трубку",
                    _ => _phoneHint
                };

            case DigitButton:
                return _codeButtonHint;

            case InspectableObject insp:
                return insp.CanCollect
                    ? $"{_inspectHint}{_hintsSeparator}{_collectHint}"
                    : _inspectHint;

            case SimpleInspectable siminsp:
                return siminsp.CanCollect
                    ? $"{_inspectHint}{_hintsSeparator}{_collectHint}"
                    : _inspectHint;

            case InteractableObject interactable:
                if (interactable.IsLocked) return _lockedHint;

                bool hasItem = _inventory != null && !string.IsNullOrEmpty(_inventory.HeldItemId);
                bool canApply = _cachedReceiver != null || _cachedPedestal != null;

                return hasItem && canApply
                    ? $"{_interactHint}{_hintsSeparator}{_applyItemHint}"
                    : _interactHint;

            case SimpleInteractable interactable:
                return _interactHint;

            default:
                return null;
        }
    }

    private void SetHint(string text)
    {
        _hintText.text = text;
        if (_hintPanel != null && !_hintPanel.activeSelf) _hintPanel.SetActive(true);
    }

    private void Hide()
    {
        if (_hintPanel != null && _hintPanel.activeSelf) _hintPanel.SetActive(false);
    }
}