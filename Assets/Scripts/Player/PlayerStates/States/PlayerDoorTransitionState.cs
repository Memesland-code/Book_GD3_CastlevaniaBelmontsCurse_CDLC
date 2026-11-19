public class PlayerDoorTransitionState : PlayerState
{
	public PlayerDoorTransitionState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

	public override void Enter()
	{
		player.ClearCoyoteTime();
		player.FreezePhysicsForLedge();
		player.Animator.SetBool(player.isRunning, false);
	}

	public override void Exit()
	{
		player.UnfreezePhysicsFromLedge();
		player.SetPhysicsMaterial(player.IsGrounded);
	}
}
