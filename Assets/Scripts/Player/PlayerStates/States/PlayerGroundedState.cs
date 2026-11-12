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
	    // Manage only slide when player pushes slide & right/left at the same time
	    if (player.Input.SlidePressedThisFrame && Mathf.Abs(player.Input.Move.x) > 0.1f)
	    {
		    stateMachine.ChangeState(player.SlideState);
		    return;
	    }
	    
	    if (player.Input.JumpPressedThisFrame)
	    {
		    player.ExecuteJump();
		    stateMachine.ChangeState(player.AirState);
	    }
    }



    public override void LogicUpdate()
    {
	    if (player.SafeTracker != null)
	    {
		    player.SafeTracker.TickTracker();
	    }
	    
	    if (!player.IsGrounded)
	    {
		    stateMachine.ChangeState(player.AirState);
		    return;
	    }

	    player.ApplyHorizontalMovement(player.GroundSpeed);
	    player.SetFacingDirection();
    }
}
