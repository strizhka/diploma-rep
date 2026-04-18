using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Живёт на Player-префабе — нужен для того, чтобы Input Actions мог ссылаться
/// на метод конкретного компонента. Всё состояние — в PauseState (один объект
/// в сцене).
///
/// Диагностика: метод OnPauseInput логирует каждый вызов с меткой [Pause:Input].
/// Если при нажатии Esc в логе НИЧЕГО с этим тегом нет — значит Input Actions
/// не вызывает метод вообще, ищи проблему в привязке UnityEvent на Player-префабе.
/// </summary>
public class PauseManager : NetworkBehaviour
{
    /// <summary>
    /// Esc. Повесь этот метод на PlayerInput → Actions → Pause.
    /// </summary>
    public void OnPauseInput(InputAction.CallbackContext context)
    {
        // Логируем ВСЕГДА при входе — даже если context не performed,
        // чтобы точно убедиться, что Input Actions дёргает метод.
        Debug.Log($"[Pause:Input] OnPauseInput вызван: phase={context.phase}, " +
                  $"isLocalPlayer={isLocalPlayer}, hasPauseState={PauseState.Instance != null}");

        if (!context.performed) return;
        if (!isLocalPlayer)
        {
            Debug.Log("[Pause:Input] Пропуск: не локальный игрок.");
            return;
        }

        if (PauseState.Instance == null)
        {
            Debug.LogError(
                "[Pause:Input] PauseState не найден в сцене! " +
                "Создай GameObject в сцене Tutorial → добавь NetworkIdentity + PauseState.");
            return;
        }

        Debug.Log("[Pause:Input] → PauseState.TogglePause()");
        PauseState.Instance.TogglePause();
    }
}