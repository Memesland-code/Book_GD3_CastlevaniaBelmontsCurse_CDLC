public abstract class PlayerState
{
	protected PlayerController player;
	protected PlayerStateMachine stateMachine;

	protected PlayerState(PlayerController player, PlayerStateMachine stateMachine)
	{
		this.player = player;
		this.stateMachine = stateMachine;
	}
	
	// Executed once when character enters this state
	public virtual void Enter() { }
	
	// Executed once when character exits this state
	public virtual void Exit() { }
	
	// Handles user inputs (called at start of Update)
	public virtual void HandleInput() { }
	
	// Frame to frame logic called in Update
	public virtual void LogicUpdate() { }
	
	// Physics logic called in FixedUpdate
	public virtual void PhysicsUpdate() { }
}
