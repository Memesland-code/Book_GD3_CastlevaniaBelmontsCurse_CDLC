using UnityEngine;

public class PlayerAirState : PlayerState
{
	public PlayerAirState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

	
	public override void Enter()
	{
		player.SetPhysicsMaterial(isGrounded: false);
	}


	public override void HandleInput()
	{
		if (player.Input.JumpPressedThisFrame)
		{
			if (player.CanCoyoteJump)
			{
				player.ConsomeCoyoteJump();
				return;
			}

			if (player.CanJump)
			{
				player.ExecuteJump();
			}
		}
	}


	public override void LogicUpdate()
	{
		if (player.WhipSystem.IsThrowing) return;
		
		// Ledge checking during air time
		if (player.LedgeGrabSystem.CheckForLedge(out _, out _))
		{
			stateMachine.ChangeState(player.LedgeHangingState);
			return;
		}
		
		// Grid platform check
		if (player.GridInteractionSystem.TryGrabGridPlatform(out GridPlatform gridPlatform))
		{
			player.GridInteractionSystem.EnterGridHang(gridPlatform);
			return;
		}
		
		// Wall run wall check
		if (player.WallRunSystem.TryDetectWallRun(out int wallRunDir, out RaycastHit wallRunHit))
		{
			player.WallRunSystem.BeginWallRun(wallRunDir, wallRunHit);
			stateMachine.ChangeState(player.WallRunState);
			return;
		}
		
		// Wall interaction check
		if (player.Rigidbody.linearVelocity.y <= player.WallInteractionSystem.MaxWallGrabUpwardVelocity
		    && player.CanGrabWall
		    && player.WallInteractionSystem.IsTouchingWall()
		    && !player.IsWallJumpInputLocked)
		{
			stateMachine.ChangeState(player.WallJumpState);
			return;
		}
		
		// Ground pound check
		if (player.Input.JumpPressedThisFrame)
		{
			if (player.Input.Move.y < player.GroundPoundSystem.GroundPoundInputThreshold)
			{
				stateMachine.ChangeState(player.GroundPoundState);
				return;
			}
		}
		
		// Back to ground
		if (!player.IsJumpGraceActive && player.IsGrounded && player.Rigidbody.linearVelocity.y <= 0.1f)
		{
			stateMachine.ChangeState(player.GroundedState);
			return;
		}
		
		player.SetFacingDirection();
	}


	public override void PhysicsUpdate()
	{
		if (!player.IsWallJumpInputLocked)
		{
			player.ApplyAirHorizontalMovement(player.AirSpeed);
		}

		bool hasWallLowGrav = player.WallRunSystem != null && player.WallRunSystem.IsEndOfWallLowGravityActive;
		bool hasPurpleLoGrav = player.WhipSystem != null && player.WhipSystem.IsPostPurpleLowGravityActive;
		
		float activeGravityScale;

		if (hasWallLowGrav || hasPurpleLoGrav)
		{
			float baseScale = player.Rigidbody.linearVelocity.y <= 0f
				? player.FallGravityScale
				: player.GravityScale;
			
			float multiplier = 1f;
			if (hasWallLowGrav) multiplier = Mathf.Min(multiplier, player.WallRunSystem.EndOfWallGravityMultiplier);
			if (hasPurpleLoGrav) multiplier = Mathf.Min(multiplier, player.WhipSystem.PostPurpleGravityMultiplier);

			activeGravityScale = baseScale * multiplier;
		}
		else if (player.Rigidbody.linearVelocity.y <= 0f)
		{
			activeGravityScale = player.FallGravityScale;
		}
		else if (!player.Input.IsJumpHeld)
		{
			activeGravityScale = player.LowJumpGravityScale;
		}
		else
		{
			activeGravityScale = player.GravityScale;
		}

		player.ApplyGravityForce(activeGravityScale);
		player.ClampMaxFallSpeed();
	}
}
