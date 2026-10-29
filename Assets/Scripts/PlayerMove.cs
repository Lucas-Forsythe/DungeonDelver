using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    // Editor Varaibles
    [SerializeField] public float MoveSpeed = 5f;
    [SerializeField] public float JumpForce = 5f;
    [SerializeField] private float Sensitivity = 50f;
    [SerializeField] private Transform CameraTransform;


    // Private Variables
    public Transform GroundCheck;
    public float GroundDistance = 0.4f;
    public LayerMask GroundMask;
    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isGrounded;
    private float xRotation = 0f;
    private Vector2 lookInput;
    private PlayerInput playerInput;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = new PlayerInput();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate()
    {
        CheckGround();
        MovePlayer();
        HandleMouseLook();
    }

    public void OnLook(InputValue inputValue)
    {
        lookInput = inputValue.Get<Vector2>();
    }

    void HandleMouseLook()
    {
        float mouseX = lookInput.x * Sensitivity * Time.fixedDeltaTime;
        float mouseY = lookInput.y * Sensitivity * Time.fixedDeltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        CameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void OnJump()
    {
        if (isGrounded)
        {
            rb.AddForce(new Vector3(0, JumpForce, 0), ForceMode.Impulse);
        }
    }

    void CheckGround()
    {
        isGrounded = Physics.CheckSphere(GroundCheck.position, GroundDistance, GroundMask);
    }

    void OnMovement(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void MovePlayer()
    {
        Vector3 Direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        Direction = Direction.normalized;
        rb.linearVelocity = new Vector3(Direction.x * MoveSpeed, rb.linearVelocity.y, Direction.z * MoveSpeed);
    }


}
