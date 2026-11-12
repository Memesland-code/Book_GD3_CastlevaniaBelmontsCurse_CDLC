using UnityEngine;

public class PlayerRespawnState : PlayerState
{
	private float respawnTimer;
	private readonly float respawnDuration;
	
	public PlayerRespawnState(PlayerController player, PlayerStateMachine stateMachine, float duration = 0.25f) : base(player, stateMachine)
	{
		respawnDuration = duration;
	}


	public override void Enter()
	{
		respawnTimer = respawnDuration;

		player.Rigidbody.linearVelocity = Vector3.zero;
		player.Rigidbody.isKinematic = true;

		if (player.SafeTracker != null && player.SafeTracker.HasValidSafePosition)
		{
			player.transform.position = player.SafeTracker.LastSafePosition + Vector3.up * 0.05f;
		}
	}


	public override void LogicUpdate()
	{
		respawnTimer -= Time.deltaTime;
		
		if (respawnTimer <= 0f)
		{
			stateMachine.ChangeState(player.AirState);
		}
	}


	public override void Exit()
	{
		player.Rigidbody.isKinematic = false;
		player.Rigidbody.linearVelocity = Vector3.zero;
	}
}
