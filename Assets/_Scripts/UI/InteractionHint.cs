using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Показывает контекстную подсказку при наведении на интерактивный объект.
/// Подсказка зависит от типа объекта в прицеле.
///
/// НАСТРОЙКА:
/// 1. Создай Canvas (Screen Space - Overlay) → Panel внизу по центру
/// 2. Внутри Panel: Text (TMP или обычный)
/// 3. На Player-префаб: добавь InteractionHint
/// 4. _hintText = ссылка на Text
/// 5. _raycaster = InteractionRaycaster (автоподхват если на том же GO)
/// </summary>
public class InteractionHint : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private GameObject _hintPanel;

    [Header("Ссылки")]
    [SerializeField] private InteractionRaycaster _raycaster;
    [SerializeField] private PlayerInventory _inventory;

    [Header("Тексты подсказок")]
    [SerializeField] private string _interactHint = "[E] Взаимодействовать";
    [SerializeField] private string _inspectHint = "[E] Осмотреть";
    [SerializeField] private string _collectHint = "[G] Подобрать";
    [SerializeField] private string _phoneHint = "[E] Телефон";
    [SerializeField] private string _codeButtonHint = "[E] Нажать";
    [SerializeField] private string _applyItemHint = "[F] Применить";
    [SerializeField] private string _lockedHint = "Заблокировано";

    private void Awake()
    {
        if (_raycaster == null)
            _raycaster = GetComponent<InteractionRaycaster>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_hintPanel != null)
            _hintPanel.SetActive(false);
    }

    private void Update()
    {
        if (NetworkClient.isConnected && !NetworkClient.ready)
            Debug.LogError("[NET] Клиент не ready — соединение потеряно!");

        if (_raycaster == null || _hintText == null) return;

        var focus = _raycaster.CurrentFocus;

        if (focus == null)
        {
            Hide();
            return;
        }

        string hint = GetHint(focus);

        if (string.IsNullOrEmpty(hint))
        {
            Hide();
            return;
        }

        _hintText.text = hint;
        if (_hintPanel != null)
            _hintPanel.SetActive(true);
    }

    private string GetHint(IFocusable focus)
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

            case InspectableObject inspectable:
                if (inspectable.CanCollect)
                    return $"{_inspectHint}\n{_collectHint}";
                return _inspectHint;

            case InteractableObject interactable:
                if (interactable.IsLocked)
                    return _lockedHint;

                // Есть ли предмет в руках + ItemReceiver/PedestalSlot на объекте?
                bool hasItem = _inventory != null && !string.IsNullOrEmpty(_inventory.HeldItemId);
                var receiver = ((MonoBehaviour)interactable).GetComponent<ItemReceiver>();
                var pedestal = ((MonoBehaviour)interactable).GetComponent<PedestalSlot>();

                if (hasItem && (receiver != null || pedestal != null))
                    return $"{_applyItemHint}";

                return _interactHint;

            default:
                return null;
        }
    }

    private void Hide()
    {
        if (_hintPanel != null)
            _hintPanel.SetActive(false);
    }
}