using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Центральный контроллер игрока: движение, камера, маршрутизация ввода.
///
/// ИЗМЕНЕНИЯ:
/// 1. E теперь проверяет тип объекта: InteractableObject ? Interact(),
///    InspectableObject ? InspectionController.StartWorldInspection()
/// 2. Новые входы: Q (выход из осмотра/инвентаря), G (забрать/экипировать),
///    B (инвентарь), стрелки (навигация инвентаря), мышь (вращение при осмотре)
/// 3. Движение и рейкаст блокируются во время осмотра и инвентаря
/// 4. Инициализирует InspectionController и InventoryUI в OnStartLocalPlayer
///
/// INPUT ACTIONS (добавить в Input Action Asset):
/// ?? Gameplay Map (существующие + новые) ??
///   Move:        WASD / Left Stick         (Value, Vector2)
///   Jump:        Space                     (Button)
///   Interact:    E                         (Button)
///   Debug:       F3                        (Button)
///   OpenInventory: B                       (Button)     ? НОВЫЙ
///   Look:        Mouse Delta               (Value, Vector2) ? для осмотра
///
/// ?? Общие (работают всегда) ??
///   InspectExit: Q                         (Button)     ? НОВЫЙ
///   Grab:        G                         (Button)     ? НОВЫЙ
///   Navigate:    Left/Right Arrow          (Value, Vector2 или float) ? НОВЫЙ
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

    private Vector2 _moveInput;
    private Vector3 _velocity;

    /// <summary>
    /// true когда игрок в режиме осмотра или инвентаря — движение/рейкаст заблокированы.
    /// </summary>
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

        // Инициализируем подсистемы
        _inspectionController?.Initialize(_cinCam.transform);
        _inventoryUI?.Initialize(_playerInventory, _inspectionController);
    }

    private void Update()
    {
        if (!isLocalPlayer || _cinCam == null) return;

        // Блокируем рейкаст во время осмотра/инвентаря
        if (_interactionRaycaster != null)
            _interactionRaycaster.Enabled = !IsBusy;

        // Блокируем движение во время осмотра/инвентаря
        if (IsBusy) return;

        HandleRotation();
        HandleMovement();
        HandleGravity();
    }

    // ???????????????????????? ДВИЖЕНИЕ ????????????????????????

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

    // ???????????????????????? INPUT CALLBACKS ????????????????????????
    // Все методы вызываются через PlayerInput (SendMessages или UnityEvents).
    // Каждый метод проверяет текущее состояние и маршрутизирует ввод.

    /// <summary>
    /// WASD / стик. Action: "Move".
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// Пробел. Action: "Jump".
    /// </summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || IsBusy) return;
        if (context.performed && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
    }

    /// <summary>
    /// E — контекстное взаимодействие.
    /// Action: "Interact".
    ///
    /// Свободный режим: InteractableObject ? Interact(), InspectableObject ? осмотр.
    /// Инвентарь: осмотреть выбранный предмет.
    /// Осмотр: игнорируется (Q для выхода, G для сбора).
    /// </summary>
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        // ??? Инвентарь: E = осмотр выбранного слота ???
        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            _inventoryUI.InspectSelected();
            return;
        }

        // ??? Осмотр активен: игнорируем E ???
        if (_inspectionController != null && _inspectionController.IsActive)
            return;

        // ??? Свободный режим: проверяем что в прицеле ???
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

    /// <summary>
    /// Q — выход из осмотра или инвентаря.
    /// Action: "InspectExit".
    /// </summary>
    public void OnInspectExit(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        // Приоритет: сначала закрываем осмотр, потом инвентарь
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
    /// G — забрать предмет (осмотр) или экипировать (инвентарь).
    /// Action: "Grab".
    /// </summary>
    public void OnGrab(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        // ??? Инвентарь: G = экипировать ???
        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            // Если сейчас осмотр из инвентаря — сначала закрываем
            if (_inspectionController != null && _inspectionController.IsActive)
                _inspectionController.StopInspection();

            _inventoryUI.EquipSelected();
            return;
        }

        // ??? Осмотр со сцены: G = забрать в инвентарь ???
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

    /// <summary>
    /// B — открыть/закрыть инвентарь.
    /// Action: "OpenInventory".
    /// </summary>
    public void OnOpenInventory(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || !context.performed) return;

        // Нельзя открыть инвентарь во время осмотра со сцены
        if (_inspectionController != null && _inspectionController.IsActive)
            return;

        if (_inventoryUI == null) return;

        if (_inventoryUI.IsOpen)
            _inventoryUI.Close();
        else
            _inventoryUI.Open();
    }

    /// <summary>
    /// Стрелки ? ? — навигация по инвентарю.
    /// Action: "Navigate" (Value, Vector2 — используем только X).
    /// </summary>
    public void OnNavigate(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;

        // Только в инвентаре
        if (_inventoryUI == null || !_inventoryUI.IsOpen) return;

        if (context.performed)
        {
            float x = context.ReadValue<Vector2>().x;
            if (x < -0.5f) _inventoryUI.Navigate(-1);
            else if (x > 0.5f) _inventoryUI.Navigate(1);
        }
    }

    /// <summary>
    /// Мышь — вращение объекта при осмотре.
    /// Action: "Look" (Value, Vector2 — Mouse Delta).
    ///
    /// В свободном режиме Cinemachine обрабатывает мышь сама через CinemachineInputAxisController.
    /// Во время осмотра CinemachineInputAxisController отключён — мышь вращает объект.
    /// </summary>
    public void OnLook(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;

        // Передаём дельту в InspectionController (он проигнорирует если неактивен)
        if (_inspectionController != null && _inspectionController.IsActive)
        {
            _inspectionController.OnRotateInput(context.ReadValue<Vector2>());
        }
    }
}
