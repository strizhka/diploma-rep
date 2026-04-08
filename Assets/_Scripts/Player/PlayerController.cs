using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ИЗМЕНЕНИЯ (v5):
/// - Добавлен OnApplyItem (F) — применение предмета из рук к объекту с ItemReceiver
/// - InteractionRaycaster.CurrentFocus проверяется на наличие ItemReceiver
///
/// INPUT ACTION (добавить):
///   ApplyItem:  F  (Button)
/// </summary>
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
    private CinemachineInputAxisController _inputAxisController;
    private CinemachinePanTilt _panTilt;

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

        _inputAxisController = _cinCam.GetComponent<CinemachineInputAxisController>();
        _panTilt = _cinCam.GetComponent<CinemachinePanTilt>();

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

        // Блокировка камеры при открытом инвентаре или осмотре
        bool busy = IsBusy;
        if (_inputAxisController != null)
            _inputAxisController.enabled = !busy;
        if (_panTilt != null)
            _panTilt.enabled = !busy;

        if (busy) return;

        HandleRotation();
        HandleMovement();
        HandleGravity();
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer || _cinCam == null) return;

        // Предмет в руках следует за наклоном камеры (тилт)
        UpdateHeldItemTilt();
    }

    // ──────────────────────── ДВИЖЕНИЕ ────────────────────────

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

    /// <summary>
    /// Предмет в руках наклоняется вместе с камерой (тилт вверх/вниз).
    /// </summary>
    private void UpdateHeldItemTilt()
    {
        if (_playerInventory == null || _playerInventory.HoldPoint == null) return;

        float pitch = _cinCam.transform.eulerAngles.x;
        float yaw = _cinCam.transform.eulerAngles.y;
        _playerInventory.HoldPoint.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // ──────────────────────── INPUT ────────────────────────

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

    /// <summary>
    /// E — контекстное взаимодействие (переключить / осмотреть).
    /// </summary>
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

        // Телефон
        if (focus is PhoneController phone)
        {
            phone.Use();
            return;
        }

        // Кнопка кодового замка
        if (focus is DigitButton digit)
        {
            digit.Press();
            return;
        }

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

    /// <summary>
    /// F — применить предмет из рук к объекту в прицеле.
    /// Поддерживает: ItemReceiver (обычный) и PedestalSlot (универсальный постамент).
    /// </summary>
    public void OnApplyItem(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;
        if (IsBusy) return;
        if (_playerInventory == null || string.IsNullOrEmpty(_playerInventory.HeldItemId)) return;

        var focus = _interactionRaycaster?.CurrentFocus;
        if (focus is not InteractableObject interactable) return;

        // Приоритет 1: PedestalSlot (универсальный постамент)
        var pedestal = interactable.GetComponent<PedestalSlot>();
        if (pedestal != null)
        {
            _playerInventory.PlaceOnPedestal(pedestal);
            PuzzleDebugOverlay.Log(
                $"[Player] Ставлю '{_playerInventory.HeldItemId}' на '{interactable.ObjectId}'");
            return;
        }

        // Приоритет 2: ItemReceiver (обычный)
        var receiver = interactable.GetComponent<ItemReceiver>();
        if (receiver != null)
        {
            _playerInventory.ApplyItemToReceiver(receiver);
            PuzzleDebugOverlay.Log(
                $"[Player] Применяю '{_playerInventory.HeldItemId}' к '{interactable.ObjectId}'");
        }
    }

    /// <summary>
    /// Q — выход из осмотра / инвентаря.
    /// </summary>
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

    /// <summary>
    /// G — забрать предмет.
    /// Приоритет: инвентарь (экипировать) → осмотр (забрать) → свободный режим (быстрый сбор).
    /// </summary>
    public void OnGrab(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        // ─── Инвентарь: G = экипировать ───
        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            if (_inspectionController != null && _inspectionController.IsActive)
                _inspectionController.StopInspection();

            _inventoryUI.EquipSelected();
            return;
        }

        // ─── Осмотр со сцены: G = забрать в инвентарь ───
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

        // ─── Свободный режим: G на InspectableObject = быстрый сбор без осмотра ───
        if (_playerInventory != null)
        {
            var focus = _interactionRaycaster?.CurrentFocus;
            if (focus is InspectableObject inspectable && inspectable.CanCollect && !inspectable.IsCollected)
            {
                _playerInventory.PickupItem(inspectable);
                PuzzleDebugOverlay.Log(
                    $"[Player] Быстро забрал '{inspectable.ObjectId}'",
                    PuzzleDebugOverlay.DebugLevel.Ok);
            }
        }
    }

    /// <summary>
    /// B — открыть/закрыть инвентарь.
    /// </summary>
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

    /// <summary>
    /// Стрелки — навигация инвентаря.
    /// </summary>
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

    /// <summary>
    /// Мышь — вращение при осмотре.
    /// </summary>
    public void OnLook(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;

        if (_inspectionController != null && _inspectionController.IsActive)
            _inspectionController.OnRotateInput(context.ReadValue<Vector2>());
    }
}