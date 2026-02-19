using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpForce = 2f;
    [SerializeField] private float _gravityScale = -9.8f;
    [SerializeField] private GameEvent onDoorOpened;

    private Vector2 _moveInput;
    private Vector3 _velocity;

    private CharacterController _characterController;

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        Vector3 move = new Vector3(_moveInput.x, 0, _moveInput.y);
        _characterController.Move(move * _moveSpeed * Time.deltaTime);

        _velocity.y += _gravityScale * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && _characterController.isGrounded)
        {
            _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravityScale);
            onDoorOpened?.Raise();
        }
    }
}
