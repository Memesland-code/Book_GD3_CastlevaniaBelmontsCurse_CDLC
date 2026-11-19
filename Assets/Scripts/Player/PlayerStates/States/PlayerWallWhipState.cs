using UnityEngine;

public class PlayerWallWhipState : PlayerState
{
	private Collider targetWallCollider;
	private Vector3 startPosition;
	private Vector3 destination;
	private Vector3 travelDirection;
	private Vector3 ropeAnchorPoint;

	private float totalDistance;
	private float traveledDistance;
	private float currentSpeed;
	private int wallDirection;
	private bool blockedBeforeWall;

	public PlayerWallWhipState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine)
	{
	}


	public override void Enter()
	{
		targetWallCollider = player.WhipSystem.ActiveWallCollider;
		ropeAnchorPoint = player.WhipSystem.ActiveWallPoint;

		if (targetWallCollider == null)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}

		player.ClearCoyoteTime();
		player.ResetJumpsCombo();
		player.FreezePhysicsForLedge();

		startPosition = player.transform.position;
		wallDirection = ropeAnchorPoint.x >= startPosition.x ? 1 : -1;
		player.SetFacingDirection(wallDirection > 0);

		// Aim capsule center at the exact surface point offset by capsule radius
		float wallOffset = player.CapsuleCollider.radius;
		Vector3 desiredDestination = new(
			ropeAnchorPoint.x - wallDirection * wallOffset,
			ropeAnchorPoint.y - player.CapsuleCollider.center.y,
			startPosition.z
		);

		Vector3 toDestination = desiredDestination - startPosition;
		toDestination.z = 0f;
		float plannedDistance = toDestination.magnitude;

		if (plannedDistance < 0.05f)
		{
			TryLatchOntoWall();
			return;
		}

		travelDirection = toDestination.normalized;
		blockedBeforeWall = false;

		// Check if another solid obstacle blocks the path before reaching the target wall
		Vector3 castOrigin = player.CapsuleCollider.bounds.center;
		float checkRadius = player.CapsuleCollider.radius * 0.85f;

		if (Physics.SphereCast(
			    castOrigin,
			    checkRadius,
			    travelDirection,
			    out RaycastHit obstacleHit,
			    plannedDistance,
			    player.SolidObstacleLayers,
			    QueryTriggerInteraction.Ignore) &&
		    obstacleHit.collider != targetWallCollider)
		{
			blockedBeforeWall = true;
			float safeDistance = Mathf.Max(0f, obstacleHit.distance - player.WhipSystem.PurpleWallStopMargin);
			destination = startPosition + travelDirection * safeDistance;
		}
		else
		{
			destination = desiredDestination;
		}

		totalDistance = Vector3.Distance(startPosition, destination);
		traveledDistance = 0f;
		currentSpeed = player.WhipSystem.PurplePullStartSpeed;

		player.WhipSystem.ShowRope(ropeAnchorPoint);
	}

	public override void LogicUpdate()
	{
		if (totalDistance <= 0.01f)
		{
			FinishPull();
			return;
		}

		float progress = Mathf.Clamp01(traveledDistance / totalDistance);
		currentSpeed = Mathf.Lerp(
			player.WhipSystem.PurplePullStartSpeed,
			player.WhipSystem.PurplePullEndSpeed,
			progress * progress
		);

		traveledDistance += currentSpeed * Time.deltaTime;

		if (traveledDistance >= totalDistance)
		{
			player.transform.position = destination;
			FinishPull();
			return;
		}

		player.transform.position = startPosition + travelDirection * traveledDistance;
	}

	private void FinishPull()
	{
		if (blockedBeforeWall)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}

		TryLatchOntoWall();
	}

	private void TryLatchOntoWall()
	{
		Vector3 rayOrigin = player.transform.position + Vector3.up * 0.5f;
		Vector3 rayDir = Vector3.right * wallDirection;

		if (Physics.Raycast(
			    rayOrigin,
			    rayDir,
			    out RaycastHit wallHit,
			    1.5f,
			    player.WallRunSystem.WallRunLayer,
			    QueryTriggerInteraction.Ignore))
		{
			// ChangeState first so Exit() unfreezes Rigidbody before BeginWallRun sets velocity
			stateMachine.ChangeState(player.WallRunState);
			player.WallRunSystem.BeginWallRun(wallDirection, wallHit);
			return;
		}

		stateMachine.ChangeState(player.AirState);
	}

	public override void Exit()
	{
		player.WhipSystem.HideRope();
		player.WhipSystem.StartWhipCooldown();
		player.UnfreezePhysicsFromLedge();
		player.SetPhysicsMaterial(false);
		player.LockGridGrab(player.WhipSystem.PostPurpleGridIgnoreDuration);
	}
}
