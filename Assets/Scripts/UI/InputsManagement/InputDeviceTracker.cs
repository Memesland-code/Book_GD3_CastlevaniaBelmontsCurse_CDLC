using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class InputDeviceTracker : MonoBehaviour
{
    public enum DeviceType { KeyboardMouse, Gamepad }
    
    public static InputDeviceTracker Instance { get; private set; }

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputIconDatabase iconDatabase;

    public DeviceType CurrentDevice { get; private set; } = DeviceType.KeyboardMouse;
    public InputActionAsset Actions => inputActions;
    public InputIconDatabase IconDatabase => iconDatabase;
    
    public event Action<DeviceType> OnDeviceChanged;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
	    if (Instance != null) return;

	    InputDeviceTracker prefab = Resources.Load<InputDeviceTracker>("InputManager");
	    if (prefab != null)
	    {
		    Instantiate(prefab);
	    }
    }
    


    private void Awake()
    {
	    if (Instance != null && Instance != this)
	    {
		    Destroy(gameObject);
		    return;
	    }

	    Instance = this;
	    transform.SetParent(null);
	    DontDestroyOnLoad(gameObject);
    }

    

    private void OnEnable()
    {
	    InputSystem.onActionChange += OnActionChange;
    }


    
    private void OnDisable()
    {
	    InputSystem.onActionChange -= OnActionChange;
    }



    private void OnActionChange(object obj, InputActionChange change)
    {
	    if (change != InputActionChange.ActionStarted & change != InputActionChange.ActionPerformed) return;

	    if (obj is not InputAction action || action.activeControl == null) return;

	    InputDevice device = action.activeControl.device;
	    DeviceType detected = device is Gamepad ? DeviceType.Gamepad : DeviceType.KeyboardMouse;

	    if (detected != CurrentDevice)
	    {
		    CurrentDevice = detected;
		    OnDeviceChanged?.Invoke(CurrentDevice);
	    }
    }
}
