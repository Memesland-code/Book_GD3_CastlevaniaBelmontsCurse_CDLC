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
	    if (player.Input.JumpPressedThisFrame)
	    {
		    player.ExecuteJump();
		    stateMachine.ChangeState(player.AirState);
	    }
    }



    public override void LogicUpdate()
    {
	    if (!player.IsGrounded)
	    {
		    stateMachine.ChangeState(player.AirState);
		    return;
	    }

	    player.ApplyHorizontalMovement(player.GroundSpeed);
	    player.UpdateFacingDirection();
    }
}
