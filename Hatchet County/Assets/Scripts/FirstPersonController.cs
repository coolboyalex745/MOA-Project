using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Speed")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintMultiplier = 2.0f;

    [Header("Camera Settings")]
    [SerializeField] private bool invertedYAxis = false;

    [Header("Look Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.2f;
    [SerializeField] private float controllerSensitivity = 100f;
    [SerializeField] private float upDownRange = 80.0f;

    Animator animator;
    private CharacterController characterController;
    private Camera mainCamera;
    private PlayerInputHandler inputHandler;
    private Vector3 currentMovement = Vector3.zero;
    private float verticalRotation;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        inputHandler = PlayerInputHandler.Instance;
        animator = FindAnyObjectByType<Animator>();
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        float speed = walkSpeed * (inputHandler.SprintValue > 0 ? sprintMultiplier : 1f);

        Vector3 inputDirection = new Vector3(inputHandler.MoveInput.x, 0f, inputHandler.MoveInput.y);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        worldDirection.Normalize();

        currentMovement.x = worldDirection.x * speed;
        currentMovement.z = worldDirection.z * speed;

        characterController.Move(currentMovement * Time.deltaTime);

    }
    void HandleRotation()
    {
        Vector2 look = inputHandler.LookInput;

        // Deadzone for controller
        if (look.magnitude < 0.1f)
            look = Vector2.zero;

        // Detect if controller is actively being used
        bool usingController = Gamepad.current != null && Gamepad.current.rightStick.ReadValue().magnitude > 0.1f;

        float sensitivity = usingController ? controllerSensitivity : mouseSensitivity;

        float mouseYInput = invertedYAxis ? -look.y : look.y;

        if (usingController)
        {
            // Controller needs deltaTime scaling
            float yaw = look.x * sensitivity * Time.deltaTime;
            float pitch = mouseYInput * sensitivity * Time.deltaTime;

            transform.Rotate(0, yaw, 0);

            verticalRotation -= pitch;
        }
        else
        {
            // Mouse should NOT use deltaTime
            float yaw = look.x * sensitivity;
            float pitch = mouseYInput * sensitivity;

            transform.Rotate(0, yaw, 0);

            verticalRotation -= pitch;
        }

        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    void OnExit(InputValue value)
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
