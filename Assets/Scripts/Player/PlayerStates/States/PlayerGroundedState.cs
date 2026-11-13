using UnityEngine;

public class PlayerGroundedState : PlayerState
{
    public PlayerGroundedState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    
    public override void Enter()
    {
	    player.ResetJumpsCombo();
	    player.SetPhysicsMaterial(isGrounded: true);
    }

    
    
    public override void HandleInput()
    {
	    // Pass through grid platform
	    if (player.Input.Move.y < -0.5f && player.Input.JumpPressedThisFrame)
	    {
		    GridPlatform standingGrid = player.GetStandingGridPlatform();
		    if (standingGrid != null)
		    {
			    player.StartGridDropThrough(standingGrid);
			    stateMachine.ChangeState(player.AirState);
			    return;
		    }
	    }
	    
	    if (player.Input.JumpPressedThisFrame)
	    {
		    player.ExecuteJump();
		    stateMachine.ChangeState(player.AirState);
		    return;
	    }
	    
	    // Manage only slide when player pushes slide & right/left at the same time
	    if (player.Input.SlidePressedThisFrame && Mathf.Abs(player.Input.Move.x) > 0.1f)
	    {
		    stateMachine.ChangeState(player.SlideState);
		    return;
	    }
    }



    public override void LogicUpdate()
    {
	    if (player.SafePositionTracker != null)
	    {
		    player.SafePositionTracker.TickTracker();
	    }
	    
	    if (!player.IsGrounded)
	    {
		    player.StartAirCoyoteTime();
		    stateMachine.ChangeState(player.AirState);
		    return;
	    }

	    player.ApplyHorizontalMovement(player.GroundSpeed);
	    player.SetFacingDirection();
    }
}
