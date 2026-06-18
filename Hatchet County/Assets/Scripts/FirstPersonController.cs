using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hatchet County - FirstPersonController
/// Drives the player's CharacterController with walk, sprint, jump, and look.
/// Gravity and jump are applied manually so the CharacterController stays
/// authoritative over vertical movement.
///
/// Jump:
///   Reads inputHandler.JumpTriggered each frame; applies an instantaneous
///   upward velocity when grounded. Gravity accumulates in currentMovement.y
///   and is reset to a small negative snap value on landing so the controller
///   stays grounded on slopes.
///
/// Look:
///   Mouse input skips deltaTime scaling; controller input uses deltaTime.
///   A 0.1 deadzone is applied to the right stick before any processing.
///   verticalRotation is clamped to [-upDownRange, +upDownRange].
/// </summary>
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Speed")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintMultiplier = 2.0f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -18f;

    [Header("Camera Settings")]
    [SerializeField] private bool invertedYAxis = false;

    [Header("Look Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.2f;
    [SerializeField] private float controllerSensitivity = 100f;
    [SerializeField] private float upDownRange = 80f;

    private CharacterController characterController;
    private Camera mainCamera;
    private PlayerInputHandler inputHandler;
    private Animator animator;

    private Vector3 currentMovement = Vector3.zero;
    private float verticalRotation = 0f;

    

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        animator = FindAnyObjectByType<Animator>();
    }

    private void Start()
    {
        inputHandler = PlayerInputHandler.Instance;

        if (inputHandler == null)
            Debug.LogError("[FirstPersonController] PlayerInputHandler instance not found.");
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        float speed = walkSpeed * (inputHandler.SprintValue > 0 ? sprintMultiplier : 1f);

        Vector3 inputDirection = new Vector3(inputHandler.MoveInput.x, 0f, inputHandler.MoveInput.y);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        worldDirection.Normalize();

        currentMovement.x = worldDirection.x * speed;
        currentMovement.z = worldDirection.z * speed;

        HandleJump();

        characterController.Move(currentMovement * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (characterController.isGrounded)
        {
            currentMovement.y = -2f;

            if (inputHandler.JumpTriggered)
                currentMovement.y = jumpForce;
        }
        else
        {
            currentMovement.y += gravity * Time.deltaTime;
        }
    }

    private void HandleRotation()
    {
        Vector2 look = inputHandler.LookInput;

        if (look.magnitude < 0.1f)
            look = Vector2.zero;

        bool usingController = Gamepad.current != null && Gamepad.current.rightStick.ReadValue().magnitude > 0.1f;
        float sensitivity = usingController ? controllerSensitivity : mouseSensitivity;
        float mouseYInput = invertedYAxis ? -look.y : look.y;

        if (usingController)
        {
            transform.Rotate(0, look.x * sensitivity * Time.deltaTime, 0);
            verticalRotation -= mouseYInput * sensitivity * Time.deltaTime;
        }
        else
        {
            transform.Rotate(0, look.x * sensitivity, 0);
            verticalRotation -= mouseYInput * sensitivity;
        }

        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    private void OnExit(InputValue value)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}