using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : NetworkBehaviour
{
    public void OnPauseInput(InputAction.CallbackContext context)
    {
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
            return;
        }

        Debug.Log("[Pause:Input] → PauseState.TogglePause()");
        PauseState.Instance.TogglePause();
    }
}