using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputBindingRowUI : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI actionLabel;
	[SerializeField] private Image keyIcon;
	[SerializeField] private TextMeshProUGUI fallbackText;

	public void Setup(InputAction action, int bindingIndex, string displayLabel)
	{
		actionLabel.text = displayLabel;

		string path = action.bindings[bindingIndex].effectivePath;
		Sprite icon = InputDeviceTracker.Instance.IconDatabase.GetSprite(path, InputDeviceTracker.Instance.CurrentDevice);

		if (icon != null)
		{
			keyIcon.sprite = icon;
			keyIcon.enabled = true;
			fallbackText.gameObject.SetActive(false);
		}
		else
		{
			keyIcon.enabled = false;
			fallbackText.gameObject.SetActive(true);
			fallbackText.text =
				InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
		}
	}
}
