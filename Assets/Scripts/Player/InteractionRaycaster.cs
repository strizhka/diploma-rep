using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class InteractionRaycaster : NetworkBehaviour
{
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private LayerMask _interactableLayer;

    private Transform _rayOrigin;
    private IFocusable _currentFocus;
    private bool _enabled = true;

    public IFocusable CurrentFocus => _currentFocus;

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

        IFocusable newFocus = hit ? FindBestFocusable(hitInfo.collider) : null;

        if (!ReferenceEquals(newFocus, _currentFocus))
        {
            _currentFocus?.SetHighlight(false);
            newFocus?.SetHighlight(true);
            _currentFocus = newFocus;
        }
    }
    
    private static IFocusable FindBestFocusable(Collider col)
    {
        var phone = col.GetComponentInParent<PhoneController>();
        if (phone != null) return phone;

        var inspectable = col.GetComponentInParent<InspectableObject>();
        if (inspectable != null && !inspectable.IsCollected)
            return inspectable;

        var interactable = col.GetComponentInParent<InteractableObject>();
        return interactable;
    }

    private void ClearFocus()
    {
        _currentFocus?.SetHighlight(false);
        _currentFocus = null;
    }
}