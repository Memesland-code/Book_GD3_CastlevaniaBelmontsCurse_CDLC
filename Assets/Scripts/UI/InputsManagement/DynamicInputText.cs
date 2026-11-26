using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class DynamicInputText : MonoBehaviour
{
	[TextArea(2, 5)]
	[Tooltip("Use the name of an action between {} (ex. {Slide}")]
	[SerializeField] private string textTemplate;

	[SerializeField] private int iconScalePercent = 120;

#if UNITY_EDITOR
	[Header("Edit Mode Preview")]
	[SerializeField] private InputDeviceTracker.DeviceType editorPreviewDevice = InputDeviceTracker.DeviceType.KeyboardMouse;
	private static InputIconDatabase cachedEditorDb;
#endif

	private TMP_Text tmpText;
	private bool isSubscribed;

	private static readonly Regex TokenRegex = new(@"\{([^}]+)\}", RegexOptions.Compiled);

	
	private void Awake()
	{
		tmpText = GetComponent<TMP_Text>();
		if (string.IsNullOrEmpty(textTemplate))
			textTemplate = tmpText.text;
	}

	
	
	private void OnEnable()
	{
		if (Application.isPlaying)
			Subscribe();

		RefreshText();
	}

	
	
	private void Start()
	{
		if (Application.isPlaying)
		{
			Subscribe();
			RefreshText();
		}
	}
	
	

	private void OnDisable()
	{
		if (isSubscribed && InputDeviceTracker.Instance != null)
		{
			InputDeviceTracker.Instance.OnDeviceChanged -= HandleDeviceChanged;
			isSubscribed = false;
		}
	}
	
	

#if UNITY_EDITOR
	private void OnValidate()
	{
		if (Application.isPlaying) return;
		if (tmpText == null) tmpText = GetComponent<TMP_Text>();
		UnityEditor.EditorApplication.delayCall += () =>
		{
			if (this != null && !Application.isPlaying)
				RefreshText();
		};
	}
#endif
	
	

	private void Subscribe()
	{
		if (isSubscribed || InputDeviceTracker.Instance == null) return;
		InputDeviceTracker.Instance.OnDeviceChanged += HandleDeviceChanged;
		isSubscribed = true;
	}

	
	
	private void HandleDeviceChanged(InputDeviceTracker.DeviceType _) => RefreshText();

	
	
	public void SetTemplate(string newTemplate)
	{
		textTemplate = newTemplate;
		RefreshText();
	}
	
	

	public void RefreshText()
	{
		if (tmpText == null) tmpText = GetComponent<TMP_Text>();
		if (tmpText == null || string.IsNullOrEmpty(textTemplate)) return;

		InputActionAsset actions = null;
		InputIconDatabase db = null;
		InputDeviceTracker.DeviceType device = InputDeviceTracker.DeviceType.KeyboardMouse;

		if (Application.isPlaying && InputDeviceTracker.Instance != null)
		{
			actions = InputDeviceTracker.Instance.Actions;
			db = InputDeviceTracker.Instance.IconDatabase;
			device = InputDeviceTracker.Instance.CurrentDevice;
		}
#if UNITY_EDITOR
		else if (!Application.isPlaying)
		{
			db = GetEditorDatabase();
			actions = db != null ? db.SourceAsset : null;
			device = editorPreviewDevice;
		}
#endif

		if (db != null && db.TmpSpriteAsset != null && tmpText.spriteAsset != db.TmpSpriteAsset)
			tmpText.spriteAsset = db.TmpSpriteAsset;

		if (actions == null)
		{
			tmpText.text = textTemplate;
			return;
		}

		tmpText.text = TokenRegex.Replace(textTemplate, match =>
		{
			string actionName = match.Groups[1].Value.Trim();
			InputAction action = actions.FindAction(actionName);
			if (action == null) return match.Value;

			List<string> bindingPaths = FindBindingPathsForDevice(action, device);
			if (bindingPaths.Count == 0) return $"[{actionName}]";

			List<string> tokens = new(bindingPaths.Count);
			foreach (string path in bindingPaths)
			{
				Sprite sprite = db != null ? db.GetSprite(path, device) : null;
				if (sprite != null)
				{
					tokens.Add($"<size={iconScalePercent}%><sprite name=\"{sprite.name}\"></size>");
				}
				else
				{
					string readable = InputControlPath.ToHumanReadableString(
						path,
						InputControlPath.HumanReadableStringOptions.OmitDevice
					);
					tokens.Add($"[{readable}]");
				}
			}

			return string.Join(" ", tokens);
		});
	}
	
	

#if UNITY_EDITOR
	private static InputIconDatabase GetEditorDatabase()
	{
		if (cachedEditorDb != null) return cachedEditorDb;

		string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InputIconDatabase");
		if (guids.Length > 0)
		{
			string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
			cachedEditorDb = UnityEditor.AssetDatabase.LoadAssetAtPath<InputIconDatabase>(path);
		}
		return cachedEditorDb;
	}
#endif
	
	

	private static List<string> FindBindingPathsForDevice(InputAction action, InputDeviceTracker.DeviceType device)
	{
		string targetGroup = device == InputDeviceTracker.DeviceType.Gamepad ? "Gamepad" : "Keyboard&Mouse";
		List<string> paths = new();
		var bindings = action.bindings;

		for (int i = 0; i < bindings.Count; i++)
		{
			InputBinding binding = bindings[i];

			// Si c'est un composite (ex. 2D Vector ZQSD), on récupère tous ses enfants du bon schéma
			if (binding.isComposite)
			{
				for (int j = i + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++)
				{
					InputBinding part = bindings[j];
					if (!string.IsNullOrEmpty(part.effectivePath) && MatchesGroup(part.groups, targetGroup))
					{
						paths.Add(part.effectivePath);
					}
				}

				// On s'arrête au premier composite valide pour éviter de doubler avec les flèches directionnelles
				if (paths.Count > 0)
					return paths;

				continue;
			}

			// Binding simple (ex. Interact, Jump, Left Stick)
			if (!binding.isPartOfComposite && !string.IsNullOrEmpty(binding.effectivePath) && MatchesGroup(binding.groups, targetGroup))
			{
				paths.Add(binding.effectivePath);
				return paths;
			}
		}

		return paths;
	}
	
	

	private static bool MatchesGroup(string groups, string targetGroup)
	{
		return !string.IsNullOrEmpty(groups) &&
		       groups.IndexOf(targetGroup, StringComparison.OrdinalIgnoreCase) >= 0;
	}
}