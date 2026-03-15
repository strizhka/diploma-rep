using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InteractionRaycaster))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpForce = 2f;
    [SerializeField] private float _gravity = -9.8f;

    [Header("References")]
    [SerializeField] private Transform _head;

    private CharacterController _controller;
    private InteractionRaycaster _interactionRaycaster;
    private InspectionController _inspectionController;
    private PlayerInventory _playerInventory;
    private InventoryUI _inventoryUI;
    private CinemachineCamera _cinCam;

    private Vector2 _moveInput;
    private Vector3 _velocity;

    private bool IsBusy =>
        (_inspectionController != null && _inspectionController.IsActive) ||
        (_inventoryUI != null && _inventoryUI.IsOpen);

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _interactionRaycaster = GetComponent<InteractionRaycaster>();
        _inspectionController = GetComponent<InspectionController>();
        _playerInventory = GetComponent<PlayerInventory>();
        _inventoryUI = GetComponent<InventoryUI>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

        _cinCam = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .FirstOrDefault(cam => cam.CompareTag("FPCamera"));

        if (_cinCam == null)
        {
            Debug.LogWarning("Камера с тегом FPCamera не найдена!");
            return;
        }

        _cinCam.Follow = _head;
        _cinCam.LookAt = _head;

        _inspectionController?.Initialize(_cinCam.transform);
        _inventoryUI?.Initialize(_playerInventory, _inspectionController);

        if (_inspectionController != null && _playerInventory != null)
        {
            _inspectionController.OnInspectionStarted += _playerInventory.HideHeldVisual;
            _inspectionController.OnInspectionEnded += _playerInventory.ShowHeldVisual;
        }
    }

    private void OnDestroy()
    {
        if (_inspectionController != null && _playerInventory != null)
        {
            _inspectionController.OnInspectionStarted -= _playerInventory.HideHeldVisual;
            _inspectionController.OnInspectionEnded -= _playerInventory.ShowHeldVisual;
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || _cinCam == null) return;

        if (_interactionRaycaster != null)
            _interactionRaycaster.Enabled = !IsBusy;

        if (IsBusy) return;

        HandleRotation();
        HandleMovement();
        HandleGravity();
    }

    private void HandleRotation()
    {
        float cameraY = _cinCam.transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, cameraY, 0f);
    }

    private void HandleMovement()
    {
        Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        _controller.Move(move * _moveSpeed * Time.deltaTime);
    }

    private void HandleGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0)
            _velocity.y = -2f;

        _velocity.y += _gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || IsBusy) return;
        if (context.performed && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
    }
    
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;
        
        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            _inventoryUI.InspectSelected();
            return;
        }

        if (_inspectionController != null && _inspectionController.IsActive)
            return;

        var focus = _interactionRaycaster?.CurrentFocus;
        if (focus == null) return;

        switch (focus)
        {
            case InteractableObject interactable:
                interactable.Interact();
                break;

            case InspectableObject inspectable:
                _inspectionController?.StartWorldInspection(inspectable);
                break;
        }
    }

    public void OnInspectExit(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        if (_inspectionController != null && _inspectionController.IsActive)
        {
            _inspectionController.StopInspection();
            return;
        }

        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            _inventoryUI.Close();
            return;
        }
    }
    
    public void OnGrab(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;
        
        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            if (_inspectionController != null && _inspectionController.IsActive)
                _inspectionController.StopInspection();

            _inventoryUI.EquipSelected();
            return;
        }
        
        if (_inspectionController != null && _inspectionController.IsActive)
        {
            var obj = _inspectionController.CurrentWorldObject;
            if (obj != null && obj.CanCollect && _playerInventory != null)
            {
                _playerInventory.PickupItem(obj);
                _inspectionController.StopInspectionCollected();
                PuzzleDebugOverlay.Log(
                    $"[Player] Забрал '{obj.ObjectId}'",
                    PuzzleDebugOverlay.DebugLevel.Ok);
            }
            return;
        }
    }
    
    public void OnOpenInventory(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        if (_inspectionController != null && _inspectionController.IsActive)
            return;

        if (_inventoryUI == null) return;

        if (_inventoryUI.IsOpen)
            _inventoryUI.Close();
        else
            _inventoryUI.Open();
    }
    
    public void OnNavigate(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        if (_inventoryUI == null || !_inventoryUI.IsOpen) return;

        if (context.performed)
        {
            float x = context.ReadValue<Vector2>().x;
            if (x < -0.5f) _inventoryUI.Navigate(-1);
            else if (x > 0.5f) _inventoryUI.Navigate(1);
        }
    }
    
    public void OnLook(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;

        if (_inspectionController != null && _inspectionController.IsActive)
            _inspectionController.OnRotateInput(context.ReadValue<Vector2>());
    }
}