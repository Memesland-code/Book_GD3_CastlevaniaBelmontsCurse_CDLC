using UnityEngine;

public class PlayerLedgeHangingState : PlayerState
{
	private float hangTimer;
	
	public PlayerLedgeHangingState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

	
	public override void Enter()
	{
		hangTimer = player.LedgeGrab.HangTimer;
		player.FreezePhysicsForLedge();
		player.AlignToLedge();
	}


	public override void HandleInput()
	{
		if (hangTimer > 0f) return;
		
		// Jump impulse from ledge grab
		if (player.Input.JumpPressedThisFrame)
		{
			player.UnfreezePhysicsFromLedge();
			player.ExecuteJump();
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		float vertical = player.Input.Move.y;
		float horizontal = player.Input.Move.x;
		float forwardDir = player.transform.forward.x;
		
		// Climb when up or direction-facing input pressed
		if (vertical > 0.1f || (horizontal * forwardDir > 0.1f))
		{
			stateMachine.ChangeState(player.LedgeClimbingState);
			return;
		}
		
		// Drop when down or non-direction-facing input pressed
		if (vertical < -0.1f || (horizontal * forwardDir < -0.1f))
		{
			stateMachine.ChangeState(player.AirState);
		}
	}


	public override void LogicUpdate()
	{
		if (hangTimer > 0f)
		{
			hangTimer -= Time.deltaTime;
		}
	}


	public override void Exit()
	{
		player.LedgeGrab.TriggerReleaseCooldown();
		player.UnfreezePhysicsFromLedge();
	}
}
