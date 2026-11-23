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
    [SerializeField] private float downwardAimAngle = 50f;
    [Tooltip("Angle pernalty (degrees) applied to downward hooks so upward hooks take priority when both are in range")]
    [SerializeField] private float downwardAimPenalty = 12f;
    
    [Header("Blocked States")] [PlayerStateSelector] [SerializeField]
    private List<string> blockedStates = new()
    {
	    nameof(PlayerRespawnState),
	    nameof(PlayerLedgeClimbingState)
    };
    
    [Header("Visual Rope & Throw Settings")]
    [SerializeField] private Transform whipOrigin;
    [SerializeField] private LineRenderer whipLineRenderer;
    [SerializeField] private float whipThrowSpeed = 65f;
    [Tooltip("How fast player velocity is damped to zero during the throw")]
    [SerializeField] private float throwVelocityDamping = 28f;
    [SerializeField] private float throwGravityScale = 0.02f;
    
    [Space(20), Header("Purple Hook Settings")]
    [SerializeField] private float purplePullStartSpeed = 28f;
	[SerializeField] private float purplePullEndSpeed = 12f;
	[SerializeField] private float purpleOvershootDistance = 2.2f;
	[SerializeField] private float purpleExitVelocityMultiplier = 0.35f;
	[SerializeField] private float postPurpleGridIgnoreDuration = 0.25f;
	[SerializeField] private float purpleWallStopMargin = 0.55f;
	[SerializeField] private float postPurpleLowGravityDuration = 0.65f;
	[SerializeField] private float postPurpleGravityMultiplier = 0.35f;

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

	private float throwDistance;
	private float throwTraveled;
	private float postPurpleLowGravityTimer;
	
	public bool IsThrowing { get; private set; }
	public bool IsPostPurpleLowGravityActive => postPurpleLowGravityTimer > 0f;
	public float PostPurpleGravityMultiplier => postPurpleGravityMultiplier;

	public void StartPostPurpleLowGravity() => postPurpleLowGravityTimer = postPurpleLowGravityDuration;
	public void ClearPostPurpleLowGravity() => postPurpleLowGravityTimer = 0f;
	
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
			if (IsThrowing && ActiveHook is not null)
			{
				float t = throwDistance > 0.001f ? Mathf.Clamp01(throwTraveled / throwDistance) : 1f;
				currentRopeTargetPos = Vector3.Lerp(whipOrigin.position, ActiveHook.AnchorPosition, t);
			}
			
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

		if (postPurpleLowGravityTimer > 0f)
		{
			postPurpleLowGravityTimer -= Time.deltaTime;
		}

		if (IsThrowing)
		{
			if (ActiveHook is null || IsCurrentStateBlocked())
			{
				CancelThrow();
				return;
			}

			throwTraveled += whipThrowSpeed * Time.deltaTime;

			if (throwTraveled >= throwDistance)
			{
				IsThrowing = false;
				currentRopeTargetPos = ActiveHook.AnchorPosition;

				if (ActiveHook.Type == HookType.Purple)
				{
					player.StateMachine.ChangeState(player.PurpleHookState);
				}
				else if (ActiveHook.Type == HookType.Yellow)
				{
					player.StateMachine.ChangeState(player.YellowHookState);
				}
			}

			return;
		}

		UpdateTargetDetectionAndHighlights();

		if (player.Input != null && player.Input.WhipPressedThisFrame)
		{
			TryActivateWhip();
		}
	}



	public void FixedTickThrowPhysics()
	{
		if (!IsThrowing || player.Rigidbody == null || player.Rigidbody.isKinematic) return;

		float dampT = 1f - Mathf.Exp(-throwVelocityDamping * Time.fixedDeltaTime);
		Vector3 dampedVel = Vector3.Lerp(player.Rigidbody.linearVelocity, Vector3.zero, dampT);
		dampedVel.z = 0f;
		player.Rigidbody.linearVelocity = dampedVel;

		if (throwGravityScale > 0f)
		{
			player.Rigidbody.AddForce(Physics.gravity * (throwGravityScale * player.Rigidbody.mass));
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



	private float EvaluateAimAngle(Vector2 hookDir, Vector2 moveInput)
	{
		// Explicit vertical input (up/down/diagonal): strict directional aim
		if (Mathf.Abs(player.Input.Move.y) >= 0.15f)
		{
			return Vector2.Angle(player.Input.Move.normalized, hookDir);
		}

		// Neutral vertical input: check both forward-up and forward-down cones
		float horizontalDir = Mathf.Abs(moveInput.x) > 0.05f
			? Mathf.Sign(moveInput.x)
			: player.FacingDirection;

		float upRad = neutralAimAngle * Mathf.Deg2Rad;
		Vector2 upAimDir = new Vector2(Mathf.Cos(upRad) * horizontalDir, Mathf.Sin(upRad)).normalized;
		float upAngle = Vector2.Angle(upAimDir, hookDir);

		float downRad = downwardAimAngle * Mathf.Deg2Rad;
		Vector2 downAimDir = new Vector2(Mathf.Cos(downRad) * horizontalDir, -Mathf.Sin(downRad)).normalized;
		float downAngle = Vector2.Angle(downAimDir, hookDir) + downwardAimPenalty;

		return Mathf.Min(upAngle, downAngle);
	}


	
    private void UpdateTargetDetectionAndHighlights()
    {
        HookTarget previousTargetedHook = CurrentTargetedHook;
        currentFrameHooks.Clear();
        CurrentTargetedHook = null;

        if (IsCurrentStateBlocked())
        {
            RefreshHookHighlights();
            return;
        }

        Vector3 playerCenter = player.CapsuleCollider.bounds.center;
        Vector2 moveInput = player.Input != null ? player.Input.Move : Vector2.zero;

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
            
            if (Physics.Raycast(playerCenter, toHook3D.normalized, distance, player.SolidObstacleLayers, QueryTriggerInteraction.Ignore)) continue;

            currentFrameHooks.Add(hook);

            float allowedAngle = (hook == previousTargetedHook)
                ? maxAimAssistAngle + aimAssistKeepBonusAngle
                : maxAimAssistAngle;

            float angle = EvaluateAimAngle(toHook2D.normalized, moveInput);
            if (angle <= allowedAngle)
            {
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
		foreach (HookTarget oldHook in activeHighlightedHooks)
		{
			if (oldHook != null && !currentFrameHooks.Contains(oldHook))
			{
				oldHook.SetHighlightState(HookHighlightState.None);
			}
		}

		activeHighlightedHooks.Clear();

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

		if (player.StateMachine.CurrentState == player.SlideState)
		{
			player.StateMachine.ChangeState(player.AirState);
		}
		player.SlideSystem?.ResetSlideImmediate();
		
		ActiveHook = CurrentTargetedHook;
		IsThrowing = true;
		
		Vector3 origin = whipOrigin != null ? whipOrigin.position : player.CapsuleCollider.bounds.center;
		throwDistance = Vector3.Distance(origin, ActiveHook.AnchorPosition);
		throwTraveled = 0f;

		float dx = ActiveHook.AnchorPosition.x - player.transform.position.x;
		if (Mathf.Abs(dx) > 0.05f)
		{
			player.SetFacingDirection(dx > 0f);
		}
		
		ShowRope(origin);
	}



	public void CancelThrow()
	{
		if (!IsThrowing) return;
		
		IsThrowing = false;
		ActiveHook = null;
		HideRope();
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


	
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		PlayerController pc = player != null ? player : GetComponent<PlayerController>();
		if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.Whip)) return;

		CapsuleCollider col = GetComponent<CapsuleCollider>();
		Vector3 center = col != null ? col.bounds.center : transform.position + Vector3.up;

		Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);
		Gizmos.DrawWireSphere(center, whipRange);
	}
#endif
}
