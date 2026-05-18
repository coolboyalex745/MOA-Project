using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hatchet County - PlayerInputHandler
/// Singleton that owns all InputAction bindings and exposes clean properties
/// for other systems to read each frame. Lives across scene loads.
///
/// Inputs exposed:
///   MoveInput       -- WASD / left stick (Vector2)
///   LookInput       -- mouse delta / right stick (Vector2)
///   SprintValue     -- hold to sprint (float, 0 or 1)
///   AttackTriggered -- true while attack button held
///   IsBlocking      -- true while block button held
///   JumpTriggered   -- true while jump button held; FirstPersonController
///                      gates this to grounded frames only
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset playerActions;

    [Header("Action Map Name References")]
    [SerializeField] private string actionMapName = "Action";

    [Header("Action Name References")]
    [SerializeField] private string move = "Move";
    [SerializeField] private string look = "Look";
    [SerializeField] private string sprint = "Sprint";
    [SerializeField] private string fire = "Attack";
    [SerializeField] private string block = "Block";
    [SerializeField] private string jump = "Jump";

    [Header("Deadzone Values")]
    [SerializeField] private float leftStickDeadzoneValue;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction blockAction;
    private InputAction jumpAction;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public float SprintValue { get; private set; }
    public bool AttackTriggered { get; private set; }
    public bool IsBlocking { get; private set; }
    public bool JumpTriggered { get; private set; }

    public static PlayerInputHandler Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InputActionMap mapReference = playerActions.FindActionMap(actionMapName);
        moveAction = mapReference.FindAction(move);
        lookAction = mapReference.FindAction(look);
        sprintAction = mapReference.FindAction(sprint);
        attackAction = mapReference.FindAction(fire);
        blockAction = mapReference.FindAction(block);
        jumpAction = mapReference.FindAction(jump);

        RegisterInputActions();

        InputSystem.settings.defaultDeadzoneMin = leftStickDeadzoneValue;
        PrintDevices();
    }

    private void RegisterInputActions()
    {
        moveAction.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => MoveInput = Vector2.zero;

        lookAction.performed += ctx => LookInput = ctx.ReadValue<Vector2>();
        lookAction.canceled += ctx => LookInput = Vector2.zero;

        sprintAction.performed += ctx => SprintValue = ctx.ReadValue<float>();
        sprintAction.canceled += ctx => SprintValue = 0f;

        attackAction.performed += ctx => AttackTriggered = true;
        attackAction.canceled += ctx => AttackTriggered = false;

        blockAction.performed += ctx => IsBlocking = true;
        blockAction.canceled += ctx => IsBlocking = false;

        jumpAction.performed += ctx => JumpTriggered = true;
        jumpAction.canceled += ctx => JumpTriggered = false;
    }

    private void OnEnable()
    {
        playerActions.FindActionMap(actionMapName).Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        playerActions.FindActionMap(actionMapName).Disable();
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch (change)
        {
            case InputDeviceChange.Disconnected:
                Debug.Log("Device Disconnected: " + device.name);
                break;
            case InputDeviceChange.Reconnected:
                Debug.Log("Device Reconnected: " + device.name);
                break;
        }
    }

    private void PrintDevices()
    {
        foreach (var device in InputSystem.devices)
        {
            if (device.enabled)
                Debug.Log("Active Devices: " + device.name);
        }
    }
}