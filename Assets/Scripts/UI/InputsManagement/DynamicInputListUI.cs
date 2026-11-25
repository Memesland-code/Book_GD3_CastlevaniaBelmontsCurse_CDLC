using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;

public class DynamicInputListUI : MonoBehaviour
{
	[Header("Configuration")]
	[SerializeField] private string actionMapName = "Player";
	[SerializeField] private string keyboardGroupName = "Keyboard&Mouse";
	[SerializeField] private string gamepadGroupName = "Gamepad";

	[Header("UI References")]
	[SerializeField] private Transform rowsContainer;
	[SerializeField] private InputBindingRowUI rowPrefab;

	private readonly List<InputBindingRowUI> spawnedRows = new();


	private void OnEnable()
	{
		if (InputDeviceTracker.Instance != null)
		{
			InputDeviceTracker.Instance.OnDeviceChanged += HandleDeviceChanged;
			RebuildList(InputDeviceTracker.Instance.CurrentDevice);
		}
	}
	
	
	
	private void OnDisable()
	{
		if (InputDeviceTracker.Instance != null)
		{
			InputDeviceTracker.Instance.OnDeviceChanged -= HandleDeviceChanged;
		}
	}



	private void HandleDeviceChanged(InputDeviceTracker.DeviceType newDevice)
	{
		RebuildList(newDevice);
	}



	private void RebuildList(InputDeviceTracker.DeviceType deviceType)
	{
		for (int i = 0; i < spawnedRows.Count; i++)
		{
			if (spawnedRows[i] != null) Destroy(spawnedRows[i].gameObject);
		}
		spawnedRows.Clear();
		
		string targetGroup = deviceType == InputDeviceTracker.DeviceType.Gamepad ? gamepadGroupName : keyboardGroupName;
		
		InputActionMap map = InputDeviceTracker.Instance.Actions.FindActionMap(actionMapName);
		if (map == null) return;

		foreach (InputAction action in map.actions)
		{
			for (int i = 0; i < action.bindings.Count; i++)
			{
				InputBinding binding = action.bindings[i];

				if (binding.isComposite) continue;
				if (!binding.groups.Contains(targetGroup)) continue;
				
				string label = binding.isPartOfComposite
					? $"{action.name} ({FormatPartName(binding.name)})"
					: action.name;
				
				InputBindingRowUI rowInstance = Instantiate(rowPrefab, rowsContainer);
				rowInstance.Setup(action, i, label);
				spawnedRows.Add(rowInstance);
			}
		}
	}
	
	
	
	private static string FormatPartName([CanBeNull] string partName)
	{
		if (string.IsNullOrEmpty(partName)) return string.Empty;
		return char.ToUpper(partName[0]) + partName.Substring(1);
	}
}
