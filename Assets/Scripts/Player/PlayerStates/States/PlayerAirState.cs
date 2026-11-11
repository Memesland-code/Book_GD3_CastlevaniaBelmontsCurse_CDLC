public class PlayerAirState : PlayerState
{
	public PlayerAirState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

	
	public override void Enter()
	{
		player.SetPhysicsMaterial(isGrounded: false);
	}


	public override void HandleInput()
	{
		if (player.Input.JumpPressedThisFrame && player.CanJump)
		{
			player.ExecuteJump();
		}
	}


	public override void LogicUpdate()
	{
		// Ledge checking during air time
		if (player.LedgeDetector.CheckForLedge(out _, out _))
		{
			stateMachine.ChangeState(player.LedgeHangingState);
			return;
		}
		
		// Back to ground
		if (player.IsGrounded && player.Rigidbody.linearVelocity.y <= 0.1f)
		{
			stateMachine.ChangeState(player.GroundedState);
			return;
		}

		player.ApplyHorizontalMovement(player.AirSpeed);
		player.UpdateFacingDirection();
	}


	public override void PhysicsUpdate()
	{
		float activeGravityScale = player.GravityScale;

		if (player.Rigidbody.linearVelocity.y > 0f && !player.Input.IsJumpHeld)
		{
			activeGravityScale = player.LowJumpGravityScale;
		}

		player.ApplyGravityForce(activeGravityScale);
	}
}
