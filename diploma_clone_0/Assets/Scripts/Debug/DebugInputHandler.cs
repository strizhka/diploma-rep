using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInputHandler : NetworkBehaviour
{
    public void OnDebug(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        if (context.performed)
            PuzzleDebugOverlay.ToggleStatic();
    }
}