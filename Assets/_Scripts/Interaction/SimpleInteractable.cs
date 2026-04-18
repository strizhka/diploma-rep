using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Локальный интерактивный объект БЕЗ сети. Для ящиков, выключателей и т.д.
/// которые не участвуют в загадках и не нужны второму игроку.
///
/// НЕ требует NetworkIdentity. Каждый клиент управляет своей копией.
/// Состояние НЕ синхронизируется — но для ящика в своей комнате это не нужно.
///
/// ЗАМЕНА: InteractableObject + NetworkIdentity → SimpleInteractable (без NetworkIdentity)
/// </summary>
public class SimpleInteractable : MonoBehaviour, IFocusable
{
    [Header("Состояния")]
    [SerializeField] private string[] _statesCycle = { "closed", "open" };

    [Header("Реакция")]
    public UnityEvent<string> OnStateChanged;

    private int _currentIndex;
    private string _currentState;
    private OutlineEffect _outline;

    public string CurrentState => _currentState;

    private void Awake()
    {
        _outline = GetComponentInChildren<OutlineEffect>(true);
        _currentState = _statesCycle.Length > 0 ? _statesCycle[0] : "";
    }

    public void SetHighlight(bool enabled)
    {
        _outline?.SetHighlight(enabled);
    }

    public void Interact()
    {
        if (_statesCycle.Length == 0) return;

        _currentIndex = (_currentIndex + 1) % _statesCycle.Length;
        _currentState = _statesCycle[_currentIndex];
        OnStateChanged?.Invoke(_currentState);
    }
}
