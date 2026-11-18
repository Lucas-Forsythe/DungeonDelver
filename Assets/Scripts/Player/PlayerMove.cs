using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInputComponent = UnityEngine.InputSystem.PlayerInput;

public class PlayerMove : MonoBehaviour
{
    public static PlayerMove Instance;

    // Editor Variables
    [SerializeField] public float MoveSpeed = 5f;
    [SerializeField] public float JumpForce = 5f;
    [SerializeField] private float Sensitivity = 50f;          // mouse
    [SerializeField] private float GamepadSensitivity = 150f;  // degrees per second at full stick
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
    private PlayerInputComponent PlayerInputComponent;
    public bool updatingRotation;

    private void Awake()
    {
        Instance = this;
        PlayerInputComponent = GetComponent<PlayerInputComponent>();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        updatingRotation = true;

        // Prevent collisions from tipping or spinning the player
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Keep the player stable against walls
        rb.angularDamping = 5f;

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

    private bool UsingGamepad()
    {
        if (PlayerInputComponent == null || string.IsNullOrEmpty(PlayerInputComponent.currentControlScheme)) return false;
        return PlayerInputComponent.currentControlScheme.ToLower().Contains("gamepad");
    }

    void HandleMouseLook()
    {
        if (!updatingRotation)
        {
            return;
        }

        float lookX, lookY;

        if (UsingGamepad())
        {
            // Stick is held, so scale by time to get degrees per second
            lookX = lookInput.x * GamepadSensitivity * Time.fixedDeltaTime;
            lookY = lookInput.y * GamepadSensitivity * Time.fixedDeltaTime;
        }
        else
        {
            lookX = lookInput.x * Sensitivity * Time.fixedDeltaTime;
            lookY = lookInput.y * Sensitivity * Time.fixedDeltaTime;
        }

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        CameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * lookX);
    }

    void OnJump()
    {
        if (!updatingRotation)
        {
            return;
        }

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
        if (!updatingRotation)
        {
            return;
        }

        Vector3 Direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        Direction = Direction.normalized;
        rb.linearVelocity = new Vector3(Direction.x * MoveSpeed, rb.linearVelocity.y, Direction.z * MoveSpeed);
    }
}