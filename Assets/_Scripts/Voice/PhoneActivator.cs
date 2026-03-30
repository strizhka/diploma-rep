using Mirror;
using UnityEngine;

/// <summary>
/// Мост между PuzzleDirector и PhoneController.
/// PuzzleDirector вызывает T_SetState "ring" на InteractableObject →
/// OnStateChanged → PhoneActivator.OnActivate("ring") → оба телефона звонят.
///
/// НАСТРОЙКА:
/// 1. Создай пустой GO "PhoneActivator" на сцене
/// 2. Добавь: InteractableObject + PhoneActivator + NetworkIdentity
/// 3. InteractableObject: statesCycle = ["idle"], _startHidden = false
///    OnStateChanged → PhoneActivator.OnActivate (Dynamic string)
/// 4. PhoneActivator: _phoneA = Phone_A, _phoneB = Phone_B
/// 5. В PuzzleDirector: T_SetState на PhoneActivator, TargetState = "ring"
/// </summary>
public class PhoneActivator : NetworkBehaviour
{
    [SerializeField] private PhoneController _phoneA;
    [SerializeField] private PhoneController _phoneB;

    [Tooltip("Состояние-команда от PuzzleDirector")]
    [SerializeField] private string _activateState = "ring";

    /// <summary>
    /// Привязывается к InteractableObject.OnStateChanged (Dynamic string).
    /// </summary>
    public void OnActivate(string newState)
    {
        if (newState != _activateState) return;

        // OnStateChanged вызывается на всех клиентах, но StartRinging — [Server]
        if (!NetworkServer.active) return;

        if (_phoneA != null) _phoneA.StartRinging();
        if (_phoneB != null) _phoneB.StartRinging();

        PuzzleDebugOverlay.Log("[PhoneActivator] Телефоны звонят!",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}