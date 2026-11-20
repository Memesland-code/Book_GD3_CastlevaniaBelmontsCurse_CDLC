using System.Collections.Generic;
using UnityEngine;

public class LightHaloPlayerSystem : MonoBehaviour
{
	[Header("Halo Settings")]
	[SerializeField] private float haloRadius = 2.5f;
	[SerializeField] private Transform lightHaloVisual;

	[Header("Crush Tolerance")]
	[Range(0.2f, 1f)] [SerializeField] private float crushCheckRadiusRatio = 0.65f;
	
	[Header("Blocked States")]
	[PlayerStateSelector][SerializeField] private List<string> blockedStates = new()
	{
		nameof(PlayerRespawnState),
		nameof(PlayerLedgeClimbingState)
	};

	private PlayerController player;
	private readonly HashSet<LightPlatform> ignoredPlatforms = new();
	
	public bool IsHaloActive { get; private set; }
	public Vector3 HaloCenter => player != null && player.CapsuleCollider != null
		? player.CapsuleCollider.bounds.center
		: transform.position + Vector3.up;


	private void Awake()
	{
		player = GetComponent<PlayerController>();
		SyncVisualScale();
		
		if (lightHaloVisual != null)
		{
			lightHaloVisual.gameObject.SetActive(false);
		}
	}


	private void OnDisable()
	{
		ForceDeactivateWithoutCrushCheck();
	}

	
	void Update()
    {
	    TickLightHaloSystem();
    }



	public void TickLightHaloSystem()
	{
		if (IsCurrentStateBlocked())
		{
			if (IsHaloActive)
			{
				ForceDeactivateWithoutCrushCheck();
			}

			UpdateAllPlatformsVisualsAndCollisions();
			return;
		}

		bool wantsHalo = player.Input != null && player.Input.IsLightHaloHeld;

		if (wantsHalo && !IsHaloActive)
		{
			ActivateHalo();
		}
		else if (!wantsHalo && IsHaloActive)
		{
			DeactivateHalo();
		}

		UpdateAllPlatformsVisualsAndCollisions();
	}



	private void ActivateHalo()
	{
		IsHaloActive = true;
		SyncVisualScale();
		
		if (lightHaloVisual != null)
		{
			lightHaloVisual.position = HaloCenter;
			lightHaloVisual.gameObject.SetActive(true);
		}
	}



	private void DeactivateHalo()
	{
		IsHaloActive = false;

		if (lightHaloVisual != null)
		{
			lightHaloVisual.gameObject.SetActive(false);
		}

		RestoreAllPlatformCollisions();

		if (IsPlayerCrushedInsideAnyPlatform())
		{
			player.TriggerTrapRespawn();
		}
	}



	public void ForceDeactivateWithoutCrushCheck()
	{
		IsHaloActive = false;

		if (lightHaloVisual != null)
		{
			lightHaloVisual.gameObject.SetActive(false);
		}

		RestoreAllPlatformCollisions();
	}



	private void UpdateAllPlatformsVisualsAndCollisions()
	{
		Vector3 center = HaloCenter;

		if (IsHaloActive && lightHaloVisual != null)
		{
			lightHaloVisual.position = center;
		}

		IReadOnlyList<LightPlatform> platforms = LightPlatform.ActiveLightPlatforms;
		for (int i = 0; i < platforms.Count; i++)
		{
			LightPlatform platform = platforms[i];
			if (platform == null || platform.Collider == null) continue;

			Vector3 closestPoint = platform.Collider.ClosestPoint(center);
			float distance = Vector3.Distance(center, closestPoint);

			if (IsHaloActive && distance <= haloRadius)
			{
				// Halo overlaping platform -> can go through and visual cut
				SetPlatformPassable(platform, true);
				platform.UpdateRuntimeState(LightPlatform.PlatformVisualState.DissolvedByHalo, center, haloRadius);
			}
			else
			{
				// Outside of active halo -> state goes back to solid state
				SetPlatformPassable(platform, false);

				LightPlatform.PlatformVisualState targetState = distance <= platform.SolidifyDetectionRange
					? LightPlatform.PlatformVisualState.SolidifiedNearPlayer
					: LightPlatform.PlatformVisualState.IdleFloating;

				platform.UpdateRuntimeState(targetState, center, 0f);
			}
		}
	}



	private void SetPlatformPassable(LightPlatform platform, bool passable)
	{
		if (passable)
		{
			if (ignoredPlatforms.Add(platform))
			{
				Physics.IgnoreCollision(player.CapsuleCollider, platform.Collider, true);
				platform.SetIntangibleLayer(true);
			}
		}
		else
		{
			if (ignoredPlatforms.Remove(platform))
			{
				Physics.IgnoreCollision(player.CapsuleCollider, platform.Collider, false);
				platform.SetIntangibleLayer(false);
			}
		}
	}



	private void RestoreAllPlatformCollisions()
	{
		foreach (LightPlatform platform in ignoredPlatforms)
		{
			if (platform != null && platform.Collider != null)
			{
				Physics.IgnoreCollision(player.CapsuleCollider, platform.Collider, false);
				platform.SetIntangibleLayer(false);
			}
		}
		ignoredPlatforms.Clear();
	}



	private bool IsPlayerCrushedInsideAnyPlatform()
	{
		CapsuleCollider col = player.CapsuleCollider;

		var platforms = LightPlatform.ActiveLightPlatforms;
		for (int i = 0; i < platforms.Count; i++)
		{
			LightPlatform platform = platforms[i];
			if (platform == null || platform.Collider == null) continue;
			
			// Precise test between reduced collider and platform's boxCollider
			if (Physics.ComputePenetration(
				    col, transform.position, transform.rotation,
				    platform.Collider, platform.transform.position, platform.transform.rotation,
				    out _, out float penetrationDepth))
			{
				float allowedGrace = col.radius * (1f - crushCheckRadiusRatio);
				if (penetrationDepth > allowedGrace)
				{
					return true;
				}
			}
		}

		return false;
	}



	private bool IsCurrentStateBlocked()
	{
		if (player.StateMachine.CurrentState == null) return true;

		return blockedStates.Contains(player.StateMachine.CurrentState.GetType().Name);
	}



	private void SyncVisualScale()
	{
		if (lightHaloVisual == null) return;
		float diameter = haloRadius * 2f;
		lightHaloVisual.localScale = Vector3.one * diameter;
	}



	public bool IsSafeForRespawn()
	{
		// Prevents when halo is active
		if (IsHaloActive) return false;

		Vector3 center = HaloCenter;
		float checkRadius = player.CapsuleCollider.radius + 0.15f;
		
		// Prevents if the characters is in contact of a LightPlatform
		IReadOnlyList<LightPlatform> platforms = LightPlatform.ActiveLightPlatforms;
		for (int i = 0; i < platforms.Count; i++)
		{
			LightPlatform platform = platforms[i];
			if (platform == null || platform.Collider == null) continue;
			
			Vector3 closest = platform.Collider.ClosestPoint(center);
			if (Vector3.Distance(center, closest) <= checkRadius)
			{
				return false;
			}
		}
		
		// Prevents if ground under character is a LightPlatform
		if (Physics.Raycast(
			    transform.position + Vector3.up * 0.1f,
			    Vector3.down,
			    out RaycastHit groundHit,
			    0.35f,
			    ~0,
			    QueryTriggerInteraction.Ignore))
		{
			if (groundHit.collider.GetComponentInParent<LightPlatform>() != null)
			{
				return false;
			}
		}
		
		return true;
	}
	
	
	
#if UNITY_EDITOR
	private void OnValidate()
	{
		SyncVisualScale();
	}

	private void OnDrawGizmosSelected()
	{
		PlayerController pc = player != null ? player : GetComponent<PlayerController>();
		if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.LightHalo)) return;
		
		CapsuleCollider col = GetComponent<CapsuleCollider>();
		Vector3 center = col != null ? col.bounds.center : transform.position + Vector3.up;

		Gizmos.color = new Color(1f, 0.95f, 0.6f, 0.45f);
		Gizmos.DrawWireSphere(center, haloRadius);
	}
#endif
}
