
public class PlayerWallRunState : PlayerState
{
	public PlayerWallRunState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		player.ResetJumpsCombo();
	}


	public override void HandleInput()
	{
		float inputX = player.Input.Move.x;
		float inputY = player.Input.Move.y;
		int wallDir = player.CurrentWallDirection;
		
		// Wall jump run
		if (player.Input.JumpPressedThisFrame)
		{
			player.WallRunSystem.ExecuteWallRunJump();
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Down or opposite wall x button pressed -> cancel wall run
		if (inputY < -0.5f || inputX * wallDir < -0.1f)
		{
			player.WallRunSystem.ExecuteDropOff(faceAwayFromWall: false);
			player.StartCoyoteTime(() => player.WallRunSystem.ExecuteWallRunJump());
			stateMachine.ChangeState(player.AirState);
			return;
		}
	}


	public override void LogicUpdate()
	{
		if (!player.WallRunSystem.IsStillOnWallRunSurface() || player.WallRunSystem.IsBlockedByCeiling())
		{
			player.WallRunSystem.ExecuteEndOfWallBoost();
			stateMachine.ChangeState(player.AirState);
			return;
		}
	}


	public override void PhysicsUpdate()
	{
		player.WallRunSystem.TickWallRunPhysics();
	}


	public override void Exit()
	{
		player.WallRunSystem.EndWallRun();
	}
}
