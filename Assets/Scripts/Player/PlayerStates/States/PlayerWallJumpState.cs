using UnityEngine;

public class PlayerWallJumpState : PlayerState
{
	private float clingTimer;
	private float currentSlideSpeed;
	
	public PlayerWallJumpState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		clingTimer = player.WallInteractionSystem.WallClingDuration;
		currentSlideSpeed = player.WallInteractionSystem.InitialWallSlideSpeed;
		player.CurrentWallDirection = player.FacingDirection;
		
		player.Rigidbody.useGravity = false;
		player.Rigidbody.linearVelocity = Vector3.zero;
	}


	public override void HandleInput()
	{
		float inputX = player.Input.Move.x;
		float inputY = player.Input.Move.y;
		
		// Wall jump (must check at all time)
		if (player.Input.JumpPressedThisFrame)
		{
			player.WallInteractionSystem.PerformWallJump(player.CurrentWallDirection);
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Down button -> character releases wall and fall
		if (inputY < -0.5f)
		{
			player.SetFacingDirection(player.CurrentWallDirection < 0);
			player.WallInteractionSystem.StartWallGrabCooldown();
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Wall opposite direction -> go in opposite direction of wall
		if (inputX * player.CurrentWallDirection < -0.1f)
		{
			player.SetFacingDirection(inputX > 0f);
			player.WallInteractionSystem.StartWallGrabCooldown();
			int wallDir = player.CurrentWallDirection;
			player.StartCoyoteTime(() => player.WallInteractionSystem.PerformWallJump(wallDir));
			stateMachine.ChangeState(player.AirState);
		}
	}


	public override void LogicUpdate()
	{
		// If hit ground while sliding down -> go back to ground state
		if (player.IsGrounded)
		{
			stateMachine.ChangeState(player.GroundedState);
			return;
		}
		
		// If wall disapears (no more wall) -> AirState
		if (!player.WallInteractionSystem.IsTouchingWall())
		{
			player.WallInteractionSystem.StartWallGrabCooldown();
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Start delay before starting to slide down
		if (clingTimer > 0f)
		{
			clingTimer -= Time.deltaTime;
		}
		else
		{
			currentSlideSpeed = Mathf.MoveTowards(currentSlideSpeed, player.WallInteractionSystem.MaxWallSlideSpeed, player.WallInteractionSystem.WallSlideAcceleration * Time.deltaTime);
		}
	}


	public override void PhysicsUpdate()
	{
		if (clingTimer > 0f)
		{
			player.Rigidbody.linearVelocity = Vector3.zero;
		}
		else
		{
			player.Rigidbody.linearVelocity = new Vector3(0f, -currentSlideSpeed, 0f);
		}
	}


	public override void Exit()
	{
		player.Rigidbody.useGravity = true;
	}
}
