using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Рейкаст из камеры для обнаружения интерактивных и осматриваемых объектов.
///
/// ИЗМЕНЕНИЯ:
/// 1. Работает с IFocusable вместо конкретного InteractableObject
///    ? Поддерживает и InteractableObject (жёлтая обводка), и InspectableObject (голубая)
/// 2. Экспортирует CurrentFocus — PlayerController решает, что делать при нажатии E
/// 3. Добавлен флаг Enabled — отключается во время осмотра/инвентаря
/// 4. Убран OnInteract() — вся логика нажатия E перенесена в PlayerController
///    (раньше был двойной raycast: один в Update, один в OnInteract)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class InteractionRaycaster : NetworkBehaviour
{
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private LayerMask _interactableLayer;

    private Transform _rayOrigin;
    private IFocusable _currentFocus;
    private bool _enabled = true;

    /// <summary>
    /// Текущий объект в прицеле. Может быть InteractableObject или InspectableObject.
    /// null если ничего в прицеле или рейкастер отключён.
    /// </summary>
    public IFocusable CurrentFocus => _currentFocus;

    /// <summary>
    /// Включить/выключить рейкастер. false во время осмотра и инвентаря.
    /// При выключении текущий фокус сбрасывается.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
                ClearFocus();
        }
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

        var cinCam = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .FirstOrDefault(cam => cam.CompareTag("FPCamera"));

        if (cinCam != null)
        {
            _rayOrigin = cinCam.transform;
            PuzzleDebugOverlay.Log($"[Raycaster] rayOrigin = {_rayOrigin.name}");
        }
        else
        {
            PuzzleDebugOverlay.Log(
                "[Raycaster] CinemachineCamera не найдена!",
                PuzzleDebugOverlay.DebugLevel.Error);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || _rayOrigin == null || !_enabled)
        {
            // Если отключены — убеждаемся что фокус сброшен
            if (_currentFocus != null)
                ClearFocus();
            return;
        }

        bool hit = Physics.Raycast(
            _rayOrigin.position,
            _rayOrigin.forward,
            out var hitInfo,
            _interactionDistance,
            _interactableLayer,
            QueryTriggerInteraction.Collide
        );

        Debug.DrawRay(
            _rayOrigin.position,
            _rayOrigin.forward * _interactionDistance,
            hit ? Color.green : Color.red);

        // Ищем IFocusable — подхватит и InteractableObject, и InspectableObject
        IFocusable newFocus = hit
            ? hitInfo.collider.GetComponentInParent<IFocusable>()
            : null;

        if (!ReferenceEquals(newFocus, _currentFocus))
        {
            _currentFocus?.SetHighlight(false);
            newFocus?.SetHighlight(true);
            _currentFocus = newFocus;
        }
    }

    private void ClearFocus()
    {
        _currentFocus?.SetHighlight(false);
        _currentFocus = null;
    }
}
