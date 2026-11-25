using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputIconDatabase", menuName = "Input/Icon Database")]
public class InputIconDatabase : ScriptableObject
{
	[Serializable]
	public struct IconEntry
	{
		[Tooltip("unity internal control name")]
		public string controlPath;
		public Sprite sprite;
	}

	[Header("Auto Populate List")]
	[SerializeField] private InputActionAsset sourceAsset;
	
	[Header("Keyboard & Mouse")]
	[SerializeField] private List<IconEntry> keyboardMouseIcons = new();
	
	[Header("Gamepad")]
	[SerializeField] private List<IconEntry> gamepadIcons = new();

	private Dictionary<string, Sprite> keyboardLookup;
	private Dictionary<string, Sprite> gamepadLookup;


	private void OnEnable()
	{
		keyboardLookup = null;
		gamepadLookup = null;
	}


	
	private void OnValidate()
	{
		keyboardLookup = null;
		gamepadLookup = null;
	}
	
	

	public Sprite GetSprite(string controlPath, InputDeviceTracker.DeviceType deviceType)
	{
		BuildLookupsIfNeeded();

		string cleanName = ExtractControlName(controlPath);
		if (string.IsNullOrEmpty(cleanName)) return null;

		Dictionary<string, Sprite> primary = deviceType == InputDeviceTracker.DeviceType.Gamepad
			? gamepadLookup
			: keyboardLookup;

		if (primary.TryGetValue(cleanName, out Sprite sprite))
		{
			return sprite;
		}

		Dictionary<string, Sprite> secondary = deviceType == InputDeviceTracker.DeviceType.Gamepad
			? keyboardLookup
			: gamepadLookup;
		
		return secondary.TryGetValue(cleanName, out sprite) ? sprite : null;
	}



	private void BuildLookupsIfNeeded()
	{
		if (keyboardLookup != null && gamepadLookup != null) return;

		keyboardLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry entry in keyboardMouseIcons)
		{
			string key = ExtractControlName(entry.controlPath);
			if (!string.IsNullOrEmpty(key) && entry.sprite != null)
				keyboardLookup[key] = entry.sprite;
		}

		gamepadLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry entry in gamepadIcons)
		{
			string key = ExtractControlName(entry.controlPath);
			if (!string.IsNullOrEmpty(key) && entry.sprite != null)
				gamepadLookup[key] = entry.sprite;
		}
	}



	private static string ExtractControlName(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		int slashIndex = path.LastIndexOf('/');
		return slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;
	}



	[ContextMenu("Import Inputs from Source Asset")]
	private void PopulateFromAsset()
	{
		if (sourceAsset == null) return;
	
		HashSet<string> existingKb = new(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry e in keyboardMouseIcons) existingKb.Add(ExtractControlName(e.controlPath));
	
		HashSet<string> existingGp = new(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry e in gamepadIcons) existingGp.Add(ExtractControlName(e.controlPath));

		foreach (InputActionMap map in sourceAsset.actionMaps)
		{
			foreach (InputAction action in map.actions)
			{
				foreach (InputBinding binding in action.bindings)
				{
					if (binding.isComposite || string.IsNullOrEmpty(binding.path) || string.IsNullOrEmpty(binding.groups))
						continue;
				
					string cleanName = ExtractControlName(binding.path);
					bool isGamepad = binding.groups.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
					bool isKeyboardMouse = binding.groups.IndexOf("Keyboard&Mouse", StringComparison.OrdinalIgnoreCase) >= 0;

					if (isGamepad && existingGp.Add(cleanName))
					{
						gamepadIcons.Add(new IconEntry { controlPath = binding.path });
					}
					else if (isKeyboardMouse && existingKb.Add(cleanName))
					{
						keyboardMouseIcons.Add(new IconEntry { controlPath = binding.path });
					}
				}
			}
		}
	
#if UNITY_EDITOR
		UnityEditor.EditorUtility.SetDirty(this);
#endif
	}
}
