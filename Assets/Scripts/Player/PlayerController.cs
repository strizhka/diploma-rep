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
    private CinemachineCamera _cinCam;
    private Vector2 _moveInput;
    private Vector3 _velocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _interactionRaycaster = GetComponent<InteractionRaycaster>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        HandleRotation();
        HandleMovement();
        HandleGravity();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        _cinCam = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .FirstOrDefault(cam => cam.CompareTag("FPCamera"));

        if (_cinCam == null)
        {
            Debug.LogWarning("Камера с тегом FPCamera не найдена!");
            return;
        }

        _cinCam.Follow = _head;
        _cinCam.LookAt = _head;
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
        if (!isLocalPlayer) return;
        if (context.performed && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        _interactionRaycaster.OnInteract(context);
    }
}