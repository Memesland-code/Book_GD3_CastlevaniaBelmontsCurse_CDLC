using UnityEngine;

public class PlayerGridHangingState : PlayerState
{
	private float inputLockTimer;
	private bool isClimbing;
	private float climbTimer;
	private Vector3 climbStartPos;
	private Vector3 climbTargetPos;
	
	public PlayerGridHangingState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		isClimbing = false;
		climbTimer = 0f;
		inputLockTimer = player.GridGrabInputLockDuration;
		
		player.FreezePhysicsForLedge();
		player.AlignToGridPlatform();
	}


	public override void HandleInput()
	{
		if (inputLockTimer > 0 || isClimbing) return;

		float inputX = player.Input.Move.x;
		float inputY = player.Input.Move.y;

		// Down input -> Let platform go
		if (inputY < -0.5f)
		{
			player.StartGridDropThrough(player.CurrentGridPlatform, isFromAbove: false);
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Any other movement -> climb up on platform
		bool wantsToClimb = player.Input.JumpPressedThisFrame || Mathf.Abs(inputX) > 0.1f || inputY > 0.5f;

		if (wantsToClimb)
		{
			// Face character if x-axis input is moved
			if (Mathf.Abs(inputX) > 0.1f)
			{
				player.SetFacingDirection(inputX > 0f);
			}

			StartClimb(inputX);
			//? player.PerformGridClimbing(inputX);
			//? stateMachine.ChangeState(player.AirState);
		}
	}


	private void StartClimb(float inputX)
	{
		if (player.CurrentGridPlatform == null)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}

		isClimbing = true;
		climbTimer = 0f;
		climbStartPos = player.transform.position;
		
		float stepX = Mathf.Abs(inputX) > 0.1f ? Mathf.Sign(inputX) * player.GridClimbStepX : 0f;
		climbTargetPos = new Vector3(climbStartPos.x + stepX, player.CurrentGridPlatform.TopSurfaceY + 0.02f, climbStartPos.z);
	}


	public override void LogicUpdate()
	{
		if (player.CurrentGridPlatform == null)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}

		if (!isClimbing)
		{
			if (inputLockTimer > 0f)
			{
				inputLockTimer -= Time.deltaTime;
			}

			return;
		}
		
		climbTimer += Time.deltaTime;
		float progress = Mathf.Clamp01(climbTimer / player.GridClimbDuration);

		float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
		player.transform.position = Vector3.Lerp(climbStartPos, climbTargetPos, easedProgress);

		if (progress >= 1f)
		{
			FinishClimb();
		}
	}



	private void FinishClimb()
	{
		player.transform.position = climbTargetPos;
		player.CompleteGridClimb();
		stateMachine.ChangeState(player.GroundedState);
	}
	


	public override void Exit()
	{
		player.UnfreezePhysicsFromLedge();
	}
}
