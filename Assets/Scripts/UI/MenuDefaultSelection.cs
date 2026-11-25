using UnityEngine;
using UnityEngine.EventSystems;

public class MenuDefaultSelection : MonoBehaviour
{
	[SerializeField] private GameObject firstSelected;
	[SerializeField] private bool lockParentOnOpen;
	[SerializeField] private bool restorePreviousOnDisable;
	
	private CanvasGroup parentCanvasGroup;
	private GameObject previousSelected;
	
	
	private void Awake()
	{
		if (!lockParentOnOpen || transform.parent == null) return;

		parentCanvasGroup = transform.parent.GetComponentInParent<CanvasGroup>();
		if (parentCanvasGroup == null)
		{
			parentCanvasGroup = transform.parent.gameObject.AddComponent<CanvasGroup>();
		}
	}

	
	
	private void OnEnable()
	{
		if (EventSystem.current != null)
		{
			GameObject current = EventSystem.current.currentSelectedGameObject;
			if (current != null && current != firstSelected)
			{
				previousSelected = current;
			}
		}

		SetParentInteractable(false);
		SelectObject(firstSelected);

		if (InputDeviceTracker.Instance != null)
			InputDeviceTracker.Instance.OnDeviceChanged += HandleDeviceChanged;
	}

	
	
	private void OnDisable()
	{
		if (InputDeviceTracker.Instance != null)
			InputDeviceTracker.Instance.OnDeviceChanged -= HandleDeviceChanged;

		SetParentInteractable(true);

		if (!restorePreviousOnDisable || EventSystem.current == null) return;

		if (previousSelected != null && previousSelected.activeInHierarchy)
		{
			SelectObject(previousSelected);
		}
	}



	private void SetParentInteractable(bool state)
	{
		if (!lockParentOnOpen || parentCanvasGroup == null) return;

		parentCanvasGroup.interactable = state;
		parentCanvasGroup.blocksRaycasts = state;
	}
	
	
	
	private void HandleDeviceChanged(InputDeviceTracker.DeviceType device)
	{
		if (device != InputDeviceTracker.DeviceType.Gamepad || EventSystem.current == null) return;

		GameObject current = EventSystem.current.currentSelectedGameObject;
		if (current == null || !current.activeInHierarchy)
			SelectObject(firstSelected);
	}

	
	
	private static void SelectObject(GameObject target)
	{
		if (target == null || EventSystem.current == null) return;

		EventSystem.current.SetSelectedGameObject(null);
		EventSystem.current.SetSelectedGameObject(target);
	}
}
