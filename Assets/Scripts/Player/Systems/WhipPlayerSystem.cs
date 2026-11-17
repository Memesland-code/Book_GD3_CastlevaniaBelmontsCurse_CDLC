using System.Collections.Generic;
using UnityEngine;

public class WhipPlayerSystem : MonoBehaviour
{
    [Header("General & Detection")]
    [SerializeField] private LayerMask hookLayer;
    [SerializeField] private float whipRange = 9f;
    [Tooltip("Max angle (degrees) around the aim point in which the whip can grab a hook")]
    [SerializeField] private float maxAimAssistAngle = 40f;
    [SerializeField] private float aimAssistKeepBonusAngle = 10f;
    [Tooltip("Default upwards angle (degrees) when no directionnal movement is hold")]
    [SerializeField] private float neutralAimAngle = 40f;
    [SerializeField] private float whipCooldownDuration = 0.12f;

    [Header("Blocked States")] [PlayerStateSelector] [SerializeField]
    private List<string> blockedStates = new()
    {
	    nameof(PlayerRespawnState),
	    nameof(PlayerLedgeClimbingState)
    };
    
    [Header("Visual Rope (LineRenderer)")]
    [SerializeField] private Transform whipOrigin;
    [SerializeField] private LineRenderer whipLineRenderer;
    
    [Space(20), Header("Purple Hook Settings")]
    [SerializeField] private float purplePullStartSpeed = 28f;
	[SerializeField] private float purplePullEndSpeed = 12f;
	[SerializeField] private float purpleOvershootDistance = 2.2f;
	[SerializeField] private float purpleExitVelocityMultiplier = 0.35f;
	[SerializeField] private float postPurpleGridIgnoreDuration = 0.25f;
	[SerializeField] private float purpleWallStopMargin = 0.55f;

	[Space(20), Header("Yellow Hook Settings")]
	[SerializeField] private float maxRopeLength = 5f;
	[SerializeField] private float swingSpeed = 14f;
	[Tooltip("Speed in which characters is brought in maxRopeLength radius")]
	[SerializeField] private float swingStartSpeedRatio = 0.35f;
	[SerializeField] private float swingAccelerationDuration = 0.25f;
	[Tooltip("Speed of correction with real swinging")]
	[SerializeField] private float ropeSettleSharpness = 8f;
	[Tooltip("Max angle where the character automatically ungrabs the hook (0° = perfectly above)")]
	[SerializeField] private float maxSwingUpwardAngle = 35f;
	[SerializeField] private float yellowExitVelocityMultiplier = 1f;
	
	private PlayerController player;
	private readonly Collider[] hitBuffer = new Collider[24];
	private readonly HashSet<HookTarget> activeHighlightedHooks = new();
	private readonly HashSet<HookTarget> currentFrameHooks = new();
	
	private float whipCooldownTimer;
	private bool isRopeVisible;
	private Vector3 currentRopeTargetPos;
	
	public HookTarget CurrentTargetedHook {get; private set;}
	public HookTarget ActiveHook {get; private set;}
	
	// Public getters for states
	public float PurplePullStartSpeed => purplePullStartSpeed;
	public float PurplePullEndSpeed => purplePullEndSpeed;
	public float PurpleOvershootDistance => purpleOvershootDistance;
	public float PurpleExitVelocityMultiplier => purpleExitVelocityMultiplier;
	public float PostPurpleGridIgnoreDuration => postPurpleGridIgnoreDuration;
	public float PurpleWallStopMargin => purpleWallStopMargin;
	
	public float MaxRopeLength => maxRopeLength;
	public float SwingSpeed => swingSpeed;
	public float SwingStartSpeedRatio => swingStartSpeedRatio;
	public float SwingAccelerationDuration => swingAccelerationDuration;
	public float RopeSettleSharpness => ropeSettleSharpness;
	public float MaxSwingUpwardAngle => maxSwingUpwardAngle;
	public float YellowExitVelocityMultiplier => yellowExitVelocityMultiplier;


	private void Awake()
	{
		player = GetComponent<PlayerController>();
		HideRope();
	}


	private void LateUpdate()
	{
		if (isRopeVisible && whipLineRenderer is not null && whipOrigin is not null)
		{
			whipLineRenderer.SetPosition(0, whipOrigin.position);
			whipLineRenderer.SetPosition(1, currentRopeTargetPos);
		}
	}



	public void TickWhipSystem()
	{
		if (whipCooldownTimer > 0f)
		{
			whipCooldownTimer -= Time.deltaTime;
		}

		UpdateHookDetectionAndHighlights();

		if (player.Input != null && player.Input.WhipPressedThisFrame)
		{
			TryActivateWhip();
		}
	}



	public void StartWhipCooldown()
	{
		whipCooldownTimer = whipCooldownDuration;
	}



	public bool IsCurrentStateBlocked()
	{
		if (player.StateMachine?.CurrentState == null) return true;
		string currentStateName = player.StateMachine.CurrentState.GetType().Name;
		return blockedStates.Contains(currentStateName);
	}



	private Vector2 GetAimDirection()
	{
		Vector2 moveInput = player.Input != null ? player.Input.Move : Vector2.zero;
		float rad = neutralAimAngle * Mathf.Deg2Rad;

		// Player not aiming -> auto aim with angle
		if (moveInput.sqrMagnitude <= 0.01f)
		{
			return new Vector2(Mathf.Cos(rad) * player.FacingDirection, Mathf.Sin(rad)).normalized;
		}
		
		// Only up/down aim -> keep base angle mixed with Move.x
		if (Mathf.Abs(moveInput.y) < 0.15f)
		{
			float horizontalDir = Mathf.Sign(moveInput.x);
			return new Vector2(Mathf.Cos(rad) * horizontalDir, Mathf.Sin(rad)).normalized;
		}
		
		// Complete directionnal aiming
		return moveInput.normalized;
	}



	private void UpdateHookDetectionAndHighlights()
	{
		HookTarget previousTargeted = CurrentTargetedHook;
		currentFrameHooks.Clear();
		CurrentTargetedHook = null;

		if (IsCurrentStateBlocked())
		{
			RefreshHookHighlights();
			return;
		}

		Vector3 playerCenter = player.CapsuleCollider.bounds.center;
		Vector2 aimDir = GetAimDirection();

		int count = Physics.OverlapSphereNonAlloc(
			playerCenter,
			whipRange,
			hitBuffer,
			hookLayer,
			QueryTriggerInteraction.Collide
		);
		
		float bestScore = float.MaxValue;

		for (int i = 0; i < count; i++)
		{
			Collider col = hitBuffer[i];
			if (col == null) continue;
			
			HookTarget hook = col.GetComponentInParent<HookTarget>();
			if (hook == null) continue;
			
			Vector3 toHook3D = hook.AnchorPosition - playerCenter;
			Vector2 toHook2D = new(toHook3D.x, toHook3D.y);
			float distance = toHook2D.magnitude;
			
			if (distance < 0.05f || distance > whipRange) continue;
			
			// Check if any blocking element between player and hook
			if (Physics.Raycast(playerCenter, toHook3D.normalized, distance, player.SolidObstacleLayers, QueryTriggerInteraction.Ignore)) continue;
			
			currentFrameHooks.Add(hook);
			
			// Already aimed hook has a better angle to avoid untarget
			float allowedAngle = (hook == previousTargeted)
				? maxAimAssistAngle + aimAssistKeepBonusAngle
				: maxAimAssistAngle;

			float angle = Vector2.Angle(aimDir, toHook2D.normalized);
			if (angle <= allowedAngle)
			{
				// Score combining angular precision (priority) and proximity
				float score = (angle / maxAimAssistAngle) * 0.75f + (distance / whipRange) * 0.25f;
				if (score < bestScore)
				{
					bestScore = score;
					CurrentTargetedHook = hook;
				}
			}
		}

		RefreshHookHighlights();
	}



	private void RefreshHookHighlights()
	{
		// Turn off out of reach hooks
		foreach (HookTarget oldHook in activeHighlightedHooks)
		{
			if (oldHook != null && !currentFrameHooks.Contains(oldHook))
			{
				oldHook.SetHighlightState(HookHighlightState.None);
			}
		}
		
		activeHighlightedHooks.Clear();
		
		// Applies InRange or Targeted to valid hooks
		foreach (HookTarget hook in currentFrameHooks)
		{
			activeHighlightedHooks.Add(hook);
			hook.SetHighlightState(hook == CurrentTargetedHook
				? HookHighlightState.Targeted
				: HookHighlightState.InRange);
		}
	}



	private void TryActivateWhip()
	{
		if (whipCooldownTimer > 0f || IsCurrentStateBlocked() || CurrentTargetedHook == null) return;
		
		ActiveHook = CurrentTargetedHook;

		if (ActiveHook.Type == HookType.Purple)
		{
			player.StateMachine.ChangeState(player.PurpleHookState);
		}
		else if (ActiveHook.Type == HookType.Yellow)
		{
			player.StateMachine.ChangeState(player.YellowHookState);
		}
	}



	public void ShowRope(Vector3 targetWorldPosition)
	{
		currentRopeTargetPos = targetWorldPosition;
		isRopeVisible = true;

		if (whipLineRenderer != null)
		{
			whipLineRenderer.enabled = true;
			if (whipOrigin != null)
			{
				whipLineRenderer.SetPosition(0, whipOrigin.position);
				whipLineRenderer.SetPosition(1, currentRopeTargetPos);
			}
		}
	}



	public void HideRope()
	{
		isRopeVisible = false;
		if (whipLineRenderer != null)
		{
			whipLineRenderer.enabled = false;
		}
	}



	private void OnDrawGizmosSelected()
	{
		CapsuleCollider col = GetComponent<CapsuleCollider>();
		Vector3 center = col != null ? col.bounds.center : transform.position + Vector3.up;
		
		Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);
		Gizmos.DrawWireSphere(center, whipRange);
	}
}
