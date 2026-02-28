using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionRaycaster : NetworkBehaviour
{
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private LayerMask _interactableLayer;

    // ”бираем _cameraTransform из полей Ч берЄм камеру сами
    private Transform _rayOrigin;
    private InteractableObject _currentTarget;

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

        // »щем Cinemachine камеру Ч она содержит реальное вертикальное вращение
        var cinCam = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .FirstOrDefault(cam => cam.CompareTag("FPCamera"));

        if (cinCam != null)
        {
            _rayOrigin = cinCam.transform;
            PuzzleDebugOverlay.Log($"[Raycaster] rayOrigin = {_rayOrigin.name}");
        }
        else
        {
            PuzzleDebugOverlay.Log("[Raycaster] CinemachineCamera не найдена!", PuzzleDebugOverlay.DebugLevel.Error);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || _rayOrigin == null) return;

        bool hit = Physics.Raycast(
            _rayOrigin.position,
            _rayOrigin.forward,
            out var hitInfo,
            _interactionDistance,
            _interactableLayer,
            QueryTriggerInteraction.Collide
        );

        Debug.DrawRay(_rayOrigin.position, _rayOrigin.forward * _interactionDistance,
            hit ? Color.green : Color.red);

        // ќбновл€ем подсветку
        InteractableObject newTarget = hit
            ? hitInfo.collider.GetComponentInParent<InteractableObject>()
            : null;

        if (newTarget != _currentTarget)
        {
            _currentTarget?.SetHighlight(false);
            newTarget?.SetHighlight(true);
            _currentTarget = newTarget;
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed || _rayOrigin == null) return;

        if (Physics.Raycast(
            _rayOrigin.position,
            _rayOrigin.forward,
            out var hitInfo,
            _interactionDistance,
            _interactableLayer,
            QueryTriggerInteraction.Collide))
        {
            var obj = hitInfo.collider.GetComponentInParent<InteractableObject>();
            obj?.Interact();
        }
    }
}