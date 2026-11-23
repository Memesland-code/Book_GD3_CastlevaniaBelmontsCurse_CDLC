using UnityEngine;

public class PlayerPurpleHookState : PlayerState
{
	private HookTarget targetHook;
	private Vector3 startPosition;
	private Vector3 hookPosition;
	private Vector3 finalDestination;
	private Vector3 travelDirection;

	private float totalDistance;
	private float distanceToHook;
	private float traveledDistance;
	private float currentSpeed;
	private bool hasPassedHook;
	
	public PlayerPurpleHookState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		targetHook = player.WhipSystem.ActiveHook;
		if (targetHook == null)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		player.ClearCoyoteTime();
		player.ResetJumpsCombo();
		player.FreezePhysicsForLedge();
		
		startPosition = player.transform.position;
		hookPosition = targetHook.AnchorPosition;

		Vector3 toHook = hookPosition - startPosition;
		toHook.z = 0f;
		distanceToHook = toHook.magnitude;

		if (distanceToHook < 0.05f)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		travelDirection = toHook.normalized;

		if (Mathf.Abs(travelDirection.x) > 0.05f)
		{
			player.SetFacingDirection(travelDirection.x > 0f);
		}
		
		// Calculate final destination past hook
		float overshoot = player.WhipSystem.PurpleOvershootDistance;
		Vector3 desiredDetination = hookPosition + travelDirection * overshoot;
		
		// Security to avoid goign in a wall
		float plannedTotalDistance = distanceToHook + overshoot;
		Vector3 castOrigin = player.CapsuleCollider.bounds.center;
		float checkRadius = player.CapsuleCollider.radius * 0.85f;

		if (Physics.SphereCast(
			    castOrigin,
			    checkRadius,
			    travelDirection,
			    out RaycastHit wallHit,
			    plannedTotalDistance,
			    player.SolidObstacleLayers,
			    QueryTriggerInteraction.Ignore))
		{
			float safeDistance = Mathf.Max(0f, wallHit.distance - player.WhipSystem.PurpleWallStopMargin);
			finalDestination = startPosition + travelDirection * safeDistance;
		}
		else
		{
			finalDestination = desiredDetination;
		}

		totalDistance = Vector3.Distance(startPosition, finalDestination);
		traveledDistance = 0f;
		currentSpeed = player.WhipSystem.PurplePullStartSpeed;
		hasPassedHook = false;
		
		player.WhipSystem.ShowRope(targetHook.AnchorPosition);
	}


	public override void LogicUpdate()
	{
		if (totalDistance <= 0.01f)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		float progress = Mathf.Clamp01(traveledDistance / totalDistance);
		
		currentSpeed = Mathf.Lerp(player.WhipSystem.PurplePullStartSpeed, player.WhipSystem.PurplePullEndSpeed, progress * progress);
		
		float step = currentSpeed * Time.deltaTime;
		traveledDistance += step;
		
		// Once hook passed shorten whip visual
		if (!hasPassedHook && traveledDistance >= totalDistance)
		{
			hasPassedHook = true;
			player.WhipSystem.HideRope();
		}

		if (traveledDistance >= totalDistance)
		{
			player.transform.position = finalDestination;

			int wallDir = Mathf.Abs(travelDirection.x) > 0.05f
				? (travelDirection.x > 0 ? 1 : -1)
				: player.FacingDirection;

			float checkReach = player.WhipSystem.PurpleWallStopMargin + 1f;

			if (player.WallRunSystem != null &&
			    player.WallRunSystem.TryDetectWallRunAuto(wallDir, checkReach, out RaycastHit wallHit))
			{
				stateMachine.ChangeState(player.WallRunState);
				player.WallRunSystem.BeginWallRun(wallDir, wallHit);
				return;
			}
			
			stateMachine.ChangeState(player.AirState);
			return;
		}

		player.transform.position = startPosition + travelDirection * traveledDistance;
	}


	public override void Exit()
	{
		player.WhipSystem.HideRope();
		player.WhipSystem.StartWhipCooldown();
		player.UnfreezePhysicsFromLedge();
		player.SetPhysicsMaterial(false);
		
		// Avoid from grabbing grid platform
		player.LockGridGrab(player.WhipSystem.PostPurpleGridIgnoreDuration);
		player.WhipSystem.StartPostPurpleLowGravity();
		
		// Keep exit velocity controlled
		Vector3 exitVelocity = travelDirection * (player.WhipSystem.PurplePullEndSpeed * player.WhipSystem.PurpleExitVelocityMultiplier);
		player.Rigidbody.linearVelocity = new Vector3(exitVelocity.x, exitVelocity.y, 0f);
	}
}
