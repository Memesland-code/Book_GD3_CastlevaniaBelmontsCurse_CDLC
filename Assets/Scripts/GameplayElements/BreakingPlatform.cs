using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BreakingPlatform : MonoBehaviour
{
	[Header("Break Settings")]
	[Tooltip("Delay between player contact and break")]
	[SerializeField] private float breakDelay = 0.6f;

	[SerializeField] private bool canRespawn = true;

	[Tooltip("Respawn delay (if canRespawn is true)")]
	[SerializeField] private float respawnDelay = 3f;

	[Header("Visual Feedback")]
	[Tooltip("Child visual reference")]
	[SerializeField] private Transform visualRoot;

	[Tooltip("Shaking intensity when about to break")]
	[SerializeField] private float shakeIntensity = 0.05f;

	private BoxCollider platformCollider;
	private Vector3 initialVisualLocalPos;
	private bool isTriggered;


	private void Awake()
	{
		platformCollider = GetComponent<BoxCollider>();

		if (visualRoot != null)
		{
			initialVisualLocalPos = visualRoot.localPosition;
		}
	}


	private void OnCollisionStay(Collision other)
	{
		if (isTriggered) return;

		if (!other.gameObject.TryGetComponent(out PlayerController player)) return;
		
		float topSurfaceY = platformCollider.bounds.max.y;
		if (player.transform.position.y >= topSurfaceY - 0.08f)
		{
			StartCoroutine(BreakRoutine());
		}
	}



	private IEnumerator BreakRoutine()
	{
		isTriggered = true;
		float elapsed = 0f;

		// Delay with visual shaking
		while (elapsed < breakDelay)
		{
			elapsed += Time.deltaTime;

			if (visualRoot is not null && shakeIntensity > 0f)
			{
				Vector2 offset = Random.insideUnitCircle * shakeIntensity;
				visualRoot.localPosition = initialVisualLocalPos + new Vector3(offset.x, offset.y, 0f);
			}

			yield return null;
		}

		if (visualRoot is not null)
		{
			visualRoot.localPosition = initialVisualLocalPos;
		}
		
		// Destruction or temporary disabling
		if (!canRespawn)
		{
			Destroy(gameObject);
			yield break;
		}

		SetPlatformActive(false);
		
		yield return new WaitForSeconds(respawnDelay);
		
		SetPlatformActive(true);
		isTriggered = false;
	}



	private void SetPlatformActive(bool active)
	{
		platformCollider.enabled = active;

		if (visualRoot != null)
		{
			visualRoot.gameObject.SetActive(active);
		}
	}
}
