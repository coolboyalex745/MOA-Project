using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset playerControls;

    [Header("Action Map Name References")]
    [SerializeField] private string actionMapName = "Action";

    [Header("Action Name References")]
    [SerializeField] private string fire = "Attack";
    [SerializeField] private string block = "Block";

    [Header("Deadzone Values")]
    [SerializeField] private float leftStickDeadzoneValue;

    private InputAction attackAction;
    private InputAction blockAction;

    public bool AttackTriggered { get; private set; }
    public bool IsBlocking { get; private set; }

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

        InputActionMap mapReference = playerControls.FindActionMap(actionMapName);

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
