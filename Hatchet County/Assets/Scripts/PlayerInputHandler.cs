using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset playerControls;

    [Header("Action Map Name References")]
    [SerializeField] private string actionMapName = "Action";

    [Header("Action Name References")]
    [SerializeField] private string move = "Move";
    [SerializeField] private string look = "Look";
    [SerializeField] private string sprint = "Sprint";
    [SerializeField] private string fire = "Attack";
    [SerializeField] private string block = "Block";

    [Header("Deadzone Values")]
    [SerializeField] private float leftStickDeadzoneValue;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction blockAction;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public float SprintValue { get; private set; }
    public bool AttackTriggered { get; private set; }
    public bool IsBlocking { get; private set; }

    public static PlayerInputHandler Instance { get; private set; }

    private void Awake()
    {
        InputActionMap mapReferance = playerControls.FindActionMap(actionMapName);
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

        InputActionMap mapReference = playerControls.FindActionMap(actionMapName);

        moveAction = mapReferance.FindAction(move);
        lookAction = mapReferance.FindAction(look);
        sprintAction = mapReferance.FindAction(sprint);
        attackAction = mapReference.FindAction(fire);
        blockAction = mapReference.FindAction(block);
        RegisterInputActions();

        InputSystem.settings.defaultDeadzoneMin = leftStickDeadzoneValue;
        PrintDevices();
    }

    void PrintDevices()
    {
        foreach (var device in InputSystem.devices)
        {
            if (device.enabled)
                Debug.Log("Active Devices: " + device.name);
        }
    }

    private void RegisterInputActions()
    {
        moveAction.performed += context => MoveInput = context.ReadValue<Vector2>();
        moveAction.canceled += context => MoveInput = Vector2.zero;

        lookAction.performed += context => LookInput = context.ReadValue<Vector2>();
        lookAction.canceled += context => LookInput = Vector2.zero;

        sprintAction.performed += context => SprintValue = context.ReadValue<float>();
        sprintAction.canceled += context => SprintValue = 0f;

        attackAction.performed += context => AttackTriggered = true;
        attackAction.canceled += context => AttackTriggered = false;

        // Block: true while held, false on release
        blockAction.performed += context => IsBlocking = true;
        blockAction.canceled += context => IsBlocking = false;
    }

    private void OnEnable()
    {
        playerControls.FindActionMap(actionMapName).Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        playerControls.FindActionMap(actionMapName).Disable();
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
}
