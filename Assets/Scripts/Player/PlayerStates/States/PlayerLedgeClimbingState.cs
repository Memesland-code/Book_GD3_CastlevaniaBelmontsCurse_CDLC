using System.Collections;
using UnityEngine;

public class PlayerLedgeClimbingState : PlayerState
{
	private bool isClimbingFinished;
	private Coroutine climbCoroutine; // Keep track of coroutine to cancel mid-execution
	
	public PlayerLedgeClimbingState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

	
	public override void Enter()
	{
		isClimbingFinished = false;
		climbCoroutine = player.StartCoroutine(ClimbRoutine());
	}


	public override void HandleInput()
	{
		if (player.Input.JumpPressedThisFrame)
		{
			player.UnfreezePhysicsFromLedge();
			player.ExecuteJump();
			stateMachine.ChangeState(player.AirState);
		}
	}


	public override void LogicUpdate()
	{
		if (isClimbingFinished) stateMachine.ChangeState(player.GroundedState);
	}



	private IEnumerator ClimbRoutine()
	{
		Vector3 startPos = player.transform.position;
		Vector3 endPos = player.transform.position + (player.transform.forward * player.LedgeDetector.ClimbOffset.z) + (Vector3.up * player.LedgeDetector.ClimbOffset.y);

		float elapsed = 0f;
		float duration = player.LedgeDetector.ClimbDuration;
		
		while (elapsed < duration)
		{
			player.transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
			elapsed += Time.deltaTime;
			yield return null;
		}

		player.transform.position = endPos;
		isClimbingFinished = true;
	}


	public override void Exit()
	{
		if (climbCoroutine != null)
		{
			player.StopCoroutine(climbCoroutine);
			climbCoroutine = null;
		}
		
		player.LedgeDetector.TriggerReleaseCooldown();
		player.UnfreezePhysicsFromLedge();
	}
}
