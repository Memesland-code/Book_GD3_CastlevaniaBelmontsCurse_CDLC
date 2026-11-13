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
	    if (player.Input.Move.x is < -0.1f or > 0.1f)
	    {
		    if (!player.Animator.GetBool(player.isRunning))
		    {
				player.Animator.SetBool(player.isRunning, true);
		    }
	    }
	    else
	    {
		    player.Animator.SetBool(player.isRunning, false);
	    }
	    
	    // Pass through grid platform
	    if (player.Input.Move.y < -0.5f && player.Input.JumpPressedThisFrame)
	    {
		    GridPlatform standingGrid = player.GridInteraction.GetStandingGridPlatform();
		    if (standingGrid != null)
		    {
			    player.GridInteraction.StartGridDropThrough(standingGrid);
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


    public override void Exit()
    {
	    player.Animator.SetBool(player.isRunning, false);
    }
}
