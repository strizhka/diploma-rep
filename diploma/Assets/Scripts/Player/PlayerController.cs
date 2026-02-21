using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpForce = 2f;
    [SerializeField] private float _gravity = -9.8f;

    [Header("References")]
    [SerializeField] private CinemachineCamera _cinCam;
    [SerializeField] private GameEvent onDoorOpened;

    private CharacterController _controller;

    private Vector2 _moveInput;
    private Vector3 _velocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
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
        Vector3 move =
            transform.right * _moveInput.x +
            transform.forward * _moveInput.y;

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
        if (context.performed && _controller.isGrounded)
        {
            _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            onDoorOpened?.Raise();
        }
    }
}