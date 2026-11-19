using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DoorZone : MonoBehaviour
{
	[Header("Door Link")]
	[SerializeField] private DoorZone destinationDoor;
	[SerializeField] private Transform spawnPoint;
	[SerializeField] private bool exitFacingRight = true;

	[Header("Transition Timings")]
	[SerializeField] private float fadeOutDuration = 0.25f;
	[SerializeField] private float blackScreenHoldDuration = 0.1f;
	[SerializeField] private float fadeInDuration = 0.25f;

	private static bool isTransitionInProgress;
	
	public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
	public bool ExitFacingRight => exitFacingRight;


	private void Awake()
	{
		BoxCollider trigger = GetComponent<BoxCollider>();
		trigger.isTrigger = true;
	}



	private void OnTriggerEnter(Collider other)
	{
		if (isTransitionInProgress) return;

		PlayerController player = other.GetComponentInParent<PlayerController>();
		if (player == null || destinationDoor == null) return;
		if (player.StateMachine.CurrentState == player.RespawnState) return;

		StartCoroutine(TeleportSequence(player));
	}



	private IEnumerator TeleportSequence(PlayerController player)
	{
		isTransitionInProgress = true;
		player.StateMachine.ChangeState(player.DoorTransitionState);

		if (ScreenFader.Instance is not null)
		{
			yield return ScreenFader.Instance.FadeOut(fadeOutDuration);
		}
		
		player.TeleportTo(destinationDoor.spawnPoint.position, destinationDoor.ExitFacingRight);

		if (blackScreenHoldDuration > 0f)
		{
			yield return new WaitForSeconds(blackScreenHoldDuration);
		}

		if (ScreenFader.Instance is not null)
		{
			yield return ScreenFader.Instance.FadeIn(fadeInDuration);
		}
		
		player.StateMachine.ChangeState(player.IsGrounded ? player.GroundedState : player.AirState);
		isTransitionInProgress = false;
	}
	
	
	
#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		Transform point = SpawnPoint;
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(point.position, 0.2f);

		Vector3 dir = (exitFacingRight ? Vector3.right : Vector3.left) * 0.6f;
		Gizmos.DrawRay(point.position, dir);

		if (destinationDoor != null)
		{
			Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.5f);
			Gizmos.DrawLine(transform.position + Vector3.up, destinationDoor.transform.position + Vector3.up);
		}
	}
#endif
}
