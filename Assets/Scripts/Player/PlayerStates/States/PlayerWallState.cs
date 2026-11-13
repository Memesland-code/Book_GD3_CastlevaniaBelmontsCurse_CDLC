using UnityEngine;

public class PlayerWallState : PlayerState
{
	private float clingTimer;
	private float currentSlideSpeed;
	private int wallDirection;
	
	public PlayerWallState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		clingTimer = player.WallInteraction.WallClingDuration;
		currentSlideSpeed = player.WallInteraction.InitialWallSlideSpeed;
		wallDirection = player.FacingDirection;
		
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
			player.WallInteraction.PerformWallJump(wallDirection);
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Down button -> character releases wall and fall
		if (inputY < -0.5f)
		{
			player.SetFacingDirection(wallDirection < 0);
			player.WallInteraction.StartWallGrabCooldown();
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Wall opposite direction -> go in opposite direction of wall
		if (inputX * wallDirection < -0.1f)
		{
			player.SetFacingDirection(inputX > 0f);
			player.WallInteraction.StartWallGrabCooldown();
			player.StartAirCoyoteTime(wallDirection);
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
		if (!player.WallInteraction.IsTouchingWall())
		{
			player.WallInteraction.StartWallGrabCooldown();
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
			currentSlideSpeed = Mathf.MoveTowards(currentSlideSpeed, player.WallInteraction.MaxWallSlideSpeed, player.WallInteraction.WallSlideAcceleration * Time.deltaTime);
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
