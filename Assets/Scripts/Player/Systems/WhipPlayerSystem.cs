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

	[Header("Wall Run Whip System")]
	[SerializeField] private float minWallWhipDistance = 1.15f;
	[SerializeField] private Transform wallTargetIndicator;
	
	private PlayerController player;
	private readonly Collider[] hitBuffer = new Collider[24];
	private readonly HashSet<HookTarget> activeHighlightedHooks = new();
	private readonly HashSet<HookTarget> currentFrameHooks = new();
	
	private float whipCooldownTimer;
	private bool isRopeVisible;
	private Vector3 currentRopeTargetPos;
	
	public HookTarget CurrentTargetedHook {get; private set;}
	public HookTarget ActiveHook {get; private set;}
	
	public Collider CurrentTargetedWallCollider {get; private set;}
	public Vector3 CurrentTargetedWallPoint {get; private set;}
	public Collider ActiveWallCollider {get; private set;}
	public Vector3 ActiveWallPoint {get; private set;}
	
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
		if (wallTargetIndicator != null) wallTargetIndicator.gameObject.SetActive(false);
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

		UpdateTargetDetectionAndHighlights();

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



	private Vector2 GetPrimaryAimDirection(Vector2 moveInput)
	{
		if (Mathf.Abs(moveInput.y) >= 0.15f)
		{
			return moveInput.normalized;
		}

		float horizontalDir = Mathf.Abs(moveInput.x) > 0.05f
			? Mathf.Sign(moveInput.x)
			: player.FacingDirection;
		
		float upRad = neutralAimAngle * Mathf.Deg2Rad;
		return new Vector2(Mathf.Cos(upRad) * horizontalDir, Mathf.Sin(upRad)).normalized;
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



	private bool TryComputeWallAnchorPoint(Collider wallCol, Vector3 playerCenter, Vector2 moveInput, out Vector3 anchorPoint)
	{
		anchorPoint = Vector3.zero;

		Vector3 closestToPlayer = wallCol.ClosestPoint(playerCenter);
		// Fallback if collider is a non-convex MeshCollider
		if (Vector3.SqrMagnitude(closestToPlayer - playerCenter) < 0.0001f)
		{
			closestToPlayer = wallCol.ClosestPointOnBounds(playerCenter);
		}
		closestToPlayer.z = playerCenter.z;

		float distToWall = Vector2.Distance(playerCenter, closestToPlayer);
		if (distToWall < minWallWhipDistance || distToWall > whipRange) return false;

		Vector2 primaryAim = GetPrimaryAimDirection(moveInput);
		Vector3 rayDir3D = new(primaryAim.x, primaryAim.y, 0f);

		if (wallCol.Raycast(new Ray(playerCenter, rayDir3D), out RaycastHit directHit, whipRange))
		{
			anchorPoint = directHit.point;
		}
		else
		{
			Vector3 projectedSample = playerCenter + rayDir3D * distToWall;
			anchorPoint = wallCol.ClosestPoint(projectedSample);
			if (Vector3.SqrMagnitude(anchorPoint - projectedSample) < 0.0001f)
			{
				anchorPoint = wallCol.ClosestPointOnBounds(projectedSample);
			}
		}

		anchorPoint.z = playerCenter.z;

		Bounds b = wallCol.bounds;
		if (b.size.y > 1.4f)
		{
			anchorPoint.y = Mathf.Clamp(anchorPoint.y, b.min.y + 0.45f, b.max.y - 0.75f);
		}

		return true;
	}



	#if UNITY_EDITOR
    private Vector3 debugRejectedPoint;
    private string debugRejectReason;
#endif

    private void UpdateTargetDetectionAndHighlights()
    {
        HookTarget previousTargetedHook = CurrentTargetedHook;
        Collider previousTargetedWall = CurrentTargetedWallCollider;

        currentFrameHooks.Clear();
        CurrentTargetedHook = null;
        CurrentTargetedWallCollider = null;

#if UNITY_EDITOR
        debugRejectReason = null;
#endif

        if (IsCurrentStateBlocked())
        {
            RefreshVisualIndicators();
            return;
        }

        Vector3 playerCenter = player.CapsuleCollider.bounds.center;
        Vector2 moveInput = player.Input != null ? player.Input.Move : Vector2.zero;
        LayerMask wallRunMask = player.WallRunSystem != null ? player.WallRunSystem.WallRunLayer : 0;
        LayerMask combinedMask = hookLayer | wallRunMask;

        int count = Physics.OverlapSphereNonAlloc(
            playerCenter,
            whipRange,
            hitBuffer,
            combinedMask,
            QueryTriggerInteraction.Collide
        );

        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            int colLayerBit = 1 << col.gameObject.layer;

            // 1. Standard HookTarget check
            if ((hookLayer.value & colLayerBit) != 0)
            {
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
                        CurrentTargetedWallCollider = null;
                    }
                }
            }
            // 2. Wall Run surface check
            else if (!col.isTrigger && (wallRunMask.value & colLayerBit) != 0)
            {
                if (!TryComputeWallAnchorPoint(col, playerCenter, moveInput, out Vector3 anchorPoint))
                {
#if UNITY_EDITOR
                    debugRejectedPoint = col.ClosestPointOnBounds(playerCenter);
                    debugRejectReason = "Distance hors min/max";
#endif
                    continue;
                }

                Vector3 toWall3D = anchorPoint - playerCenter;
                Vector2 toWall2D = new(toWall3D.x, toWall3D.y);
                float distance = toWall2D.magnitude;

                if (distance < minWallWhipDistance || distance > whipRange) continue;

                // Allow hits belonging to the same wall hierarchy (parent/child colliders)
                if (Physics.Raycast(playerCenter, toWall3D.normalized, out RaycastHit blockHit, distance - 0.05f, player.SolidObstacleLayers, QueryTriggerInteraction.Ignore) &&
                    blockHit.collider.transform.root != col.transform.root)
                {
#if UNITY_EDITOR
                    debugRejectedPoint = blockHit.point;
                    debugRejectReason = $"Bloqué par {blockHit.collider.name}";
#endif
                    continue;
                }

                float allowedAngle = (col == previousTargetedWall)
                    ? maxAimAssistAngle + aimAssistKeepBonusAngle
                    : maxAimAssistAngle;

                float angle = EvaluateAimAngle(toWall2D.normalized, moveInput);
                if (angle <= allowedAngle)
                {
                    float score = (angle / maxAimAssistAngle) * 0.75f + (distance / whipRange) * 0.25f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        CurrentTargetedHook = null;
                        CurrentTargetedWallCollider = col;
                        CurrentTargetedWallPoint = anchorPoint;
                    }
                }
#if UNITY_EDITOR
                else
                {
                    debugRejectedPoint = anchorPoint;
                    debugRejectReason = $"Angle trop grand ({angle:F0}° > {allowedAngle:F0}°)";
                }
#endif
            }
        }

        RefreshVisualIndicators();
    }



	private void RefreshVisualIndicators()
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

		if (wallTargetIndicator != null)
		{
			bool hasWallTarget = CurrentTargetedWallCollider != null;
			wallTargetIndicator.gameObject.SetActive(hasWallTarget);

			if (hasWallTarget)
			{
				if (wallTargetIndicator is RectTransform rectTransform)
				{
					Camera cam = Camera.main;
					if (cam != null)
					{
						rectTransform.position = cam.WorldToScreenPoint(CurrentTargetedWallPoint);
					}
				}
				else
				{
					wallTargetIndicator.position = CurrentTargetedWallPoint;
				}
			}
		}
	}



	private void TryActivateWhip()
	{
		if (whipCooldownTimer > 0f || IsCurrentStateBlocked()) return;

		if (CurrentTargetedHook != null)
		{
			ActiveHook = CurrentTargetedHook;
			ActiveWallCollider = null;

			if (ActiveHook.Type == HookType.Purple)
			{
				player.StateMachine.ChangeState(player.PurpleHookState);
			}
			else if (ActiveHook.Type == HookType.Yellow)
			{
				player.StateMachine.ChangeState(player.YellowHookState);
			}
		}
		else if (CurrentTargetedWallCollider != null)
		{
			ActiveHook = null;
			ActiveWallCollider = CurrentTargetedWallCollider;
			ActiveWallPoint = CurrentTargetedWallPoint;

			player.StateMachine.ChangeState(player.WallWhipState);
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


	
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		PlayerController pc = player != null ? player : GetComponent<PlayerController>();
		if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.Whip)) return;
		
		CapsuleCollider col = GetComponent<CapsuleCollider>();
		Vector3 center = col != null ? col.bounds.center : transform.position + Vector3.up;

		Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);
		Gizmos.DrawWireSphere(center, whipRange);

		if (!Application.isPlaying) return;

		Vector2 moveInput = player != null && player.Input != null ? player.Input.Move : Vector2.zero;
		Vector2 aimDir = GetPrimaryAimDirection(moveInput);
		Gizmos.color = Color.cyan;
		Gizmos.DrawRay(center, new Vector3(aimDir.x, aimDir.y, 0f) * whipRange);

		if (CurrentTargetedWallCollider != null)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(center, CurrentTargetedWallPoint);
			Gizmos.DrawSphere(CurrentTargetedWallPoint, 0.18f);
		}
		else if (!string.IsNullOrEmpty(debugRejectReason))
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(center, debugRejectedPoint);
			Gizmos.DrawSphere(debugRejectedPoint, 0.18f);
			UnityEditor.Handles.Label(debugRejectedPoint + Vector3.up * 0.3f, debugRejectReason);
#endif
		}
	}
}
