using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ActionPromptIcon : MonoBehaviour
{
	[SerializeField] private InputActionReference actionReference;
	[SerializeField] private string keyboardGroup = "Keyboard&Mouse";
	[SerializeField] private string gamepadGroup = "Gamepad";
	
	private Image targetImage;


	private void Awake()
	{
		targetImage = GetComponent<Image>();
	}


	
	private void OnEnable()
	{
		if (InputDeviceTracker.Instance != null)
		{
			InputDeviceTracker.Instance.OnDeviceChanged += HandleDeviceChanged;
			RefreshIcon(InputDeviceTracker.Instance.CurrentDevice);
		}
	}

	

	private void OnDisable()
	{
		if (InputDeviceTracker.Instance != null)
		{
			InputDeviceTracker.Instance.OnDeviceChanged -= HandleDeviceChanged;
		}
	}
	
	
	
	private void HandleDeviceChanged(InputDeviceTracker.DeviceType deviceType) => RefreshIcon(deviceType);



	private void RefreshIcon(InputDeviceTracker.DeviceType deviceType)
	{
		if (actionReference == null || InputDeviceTracker.Instance == null) return;
		
		string targetGroup = deviceType == InputDeviceTracker.DeviceType.Gamepad ? gamepadGroup : keyboardGroup;
		
		InputAction action = actionReference.action;
		for (int i = 0; i < action.bindings.Count; i++)
		{
			InputBinding binding = action.bindings[i];
			if (binding.isComposite) continue;

			if (binding.groups.Contains(targetGroup))
			{
				Sprite sprite = InputDeviceTracker.Instance.IconDatabase.GetSprite(binding.effectivePath, InputDeviceTracker.Instance.CurrentDevice);
				if (sprite != null)
				{
					targetImage.sprite = sprite;
				}

				return;
			}
		}
	}
}
