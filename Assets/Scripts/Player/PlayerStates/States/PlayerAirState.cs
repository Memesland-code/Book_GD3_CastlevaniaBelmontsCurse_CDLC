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
			if (player.CanWallCoyoteJump)
			{
				if (player.LastWallDirection == 0)
				{
					player.ExecuteJump();
					return;
				}
				player.PerformWallJump(player.LastWallDirection);
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
		// Ledge checking during air time
		if (player.LedgeGrab.CheckForLedge(out _, out _))
		{
			stateMachine.ChangeState(player.LedgeHangingState);
			return;
		}
		
		// Grid platform check
		if (player.TryGrabGridPlatform(out GridPlatform gridPlatform))
		{
			player.EnterGridHang(gridPlatform);
			return;
		}
		
		// Wall slide check
		if (player.Rigidbody.linearVelocity.y <= player.MaxWallGrabUpwardVelocity && player.CanGrabWall && player.IsTouchingWall())
		{
			stateMachine.ChangeState(player.WallState);
			return;
		}
		
		// Back to ground
		/*
		 if (player.IsGrounded && player.Rigidbody.linearVelocity.y <= 0.1f)
		{
			stateMachine.ChangeState(player.GroundedState);
			return;
		}
		*/
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
		
		float activeGravityScale;

		if (player.Rigidbody.linearVelocity.y <= 0f)
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
