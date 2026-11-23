using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class CameraLookaheadPlayerSystem : MonoBehaviour
{
	[Serializable]
	public class StateLookaheadOverride
	{
		[PlayerStateSelector] public string stateName;
		public float velocityMultiplierX = 1f;
		public float velocityMultiplierY = 1f;
		[Tooltip("Additional fixed lookahead offset (ex. +3 Y in WallRun, -3.5 Y in GroundPound)")]
		public Vector2 additiveOffset;
		[Tooltip("Keep slight forward offset based on FacingDirection when horizontal velocity is low")]
		public bool keepIdleFacingOffset = true;
		[Tooltip("Custom sharpness for this state (<= 0 uses default lookaheadSharpness)")]
		public float customSharpness = -1f;
	}

	[Header("Cinemachine Reference")]
	[Tooltip("Leave empty to auto-find via MainCamera tag")]
	[SerializeField] private CinemachineCamera cinemachineCamera;

	[Header("Lookahead Distances")]
	[SerializeField] private float lookaheadSharpness = 5.5f;
	[SerializeField] private float idleFacingOffsetX = 1.1f;
	[SerializeField] private float maxLookaheadX = 3.2f;
	[SerializeField] private float maxLookaheadY = 3.5f;

	[Header("Velocity Mapping & Deadzones")]
	[SerializeField] private float referenceSpeedX = 8.5f;
	[SerializeField] private float referenceSpeedY = 16f;
	[SerializeField] private float velocityDeadzoneX = 1.2f;
	[SerializeField] private float velocityDeadzoneY = 6.5f;

	[Header("State Overrides")]
	[SerializeField] private List<StateLookaheadOverride> stateOverrides = new()
	{
		new StateLookaheadOverride
		{
			stateName = nameof(PlayerWallRunState),
			velocityMultiplierX = 0f,
			velocityMultiplierY = 0f,
			additiveOffset = new Vector2(0f, 3f),
			keepIdleFacingOffset = false
		},
		new StateLookaheadOverride
		{
			stateName = nameof(PlayerGroundPoundState),
			velocityMultiplierX = 0f,
			velocityMultiplierY = 0.4f,
			additiveOffset = new Vector2(0f, -3.5f),
			keepIdleFacingOffset = false
		},
		new StateLookaheadOverride
		{
			stateName = nameof(PlayerYellowHookState),
			velocityMultiplierX = 0.25f,
			velocityMultiplierY = 0f,
			additiveOffset = Vector2.zero,
			keepIdleFacingOffset = true
		}
	};

	private PlayerController player;
	private CinemachineFollow cinemachineFollow;
	private Vector3 baseFollowOffset;
	private Vector3 previousPosition;
	private Vector3 currentLookahead;


	private void Awake()
	{
		player = GetComponent<PlayerController>();

		if (cinemachineCamera == null)
		{
			GameObject camObj = GameObject.FindWithTag("MainCamera");
			if (camObj != null) cinemachineCamera = camObj.GetComponent<CinemachineCamera>();
		}

		if (cinemachineCamera != null)
		{
			cinemachineFollow = cinemachineCamera.GetComponent<CinemachineFollow>();
			if (cinemachineFollow != null)
			{
				baseFollowOffset = cinemachineFollow.FollowOffset;
			}
		}
		
		previousPosition = transform.position;
	}



	private void LateUpdate()
	{
		if (cinemachineFollow is null) return;
		
		float dt = Time.deltaTime;
		if (dt <= 0) return;
		
		Vector3 currentPos = transform.position;
		Vector3 deltaPos = currentPos - previousPosition;
		previousPosition = currentPos;
		
		// Auto-snap if player teleported
		if (deltaPos.sqrMagnitude > 25f)
		{
			ResetLookaheadImmediate();
			return;
		}

		Vector2 velocity = new(deltaPos.x / dt, deltaPos.y / dt);
		StateLookaheadOverride activeOverride = GetActiveStateOverride();
		
		float multX = activeOverride != null ? activeOverride.velocityMultiplierX : 1f;
		float multY = activeOverride != null ? activeOverride.velocityMultiplierY : 1f;
		bool keepFacing = activeOverride == null || activeOverride.keepIdleFacingOffset;
		Vector2 extraOffset = activeOverride != null ? activeOverride.additiveOffset : Vector2.zero;
		float sharpness = (activeOverride != null && activeOverride.customSharpness > 0f)
			? activeOverride.customSharpness
			: lookaheadSharpness;

		float targetX = 0f;
		if (Mathf.Abs(velocity.x) > velocityDeadzoneX && referenceSpeedX > 0.01f)
		{
			float ratioX = Mathf.Clamp(velocity.x / referenceSpeedX, -1, 1);
			targetX = ratioX * maxLookaheadX * multX;
		}
		else if (keepFacing)
		{
			targetX = player.FacingDirection * idleFacingOffsetX;
		}
		
		
		float targetY = 0f;
		if (Mathf.Abs(velocity.y) > velocityDeadzoneY && referenceSpeedY > 0.01f)
		{
			float ratioY = Mathf.Clamp(velocity.y / referenceSpeedY, -1, 1);
			targetY = ratioY * maxLookaheadY * multY;
		}
		
		Vector2 targetLookahead = new Vector2(targetX, targetY) + extraOffset;

		float t = 1f - Mathf.Exp(-sharpness * dt);
		currentLookahead = Vector2.Lerp(currentLookahead, targetLookahead, t);
		
		cinemachineFollow.FollowOffset = baseFollowOffset + new Vector3(currentLookahead.x, currentLookahead.y, 0f);
	}



	private StateLookaheadOverride GetActiveStateOverride()
	{
		if (player.StateMachine?.CurrentState == null) return null;

		string currentStateName = player.StateMachine.CurrentState.GetType().Name;
		for (int i = 0; i < stateOverrides.Count; i++)
		{
			if (stateOverrides[i].stateName == currentStateName)
			{
				return stateOverrides[i];
			}
		}

		return null;
	}



	/// <summary>
	/// Resets lookahead immediately (called in PlayerController.TeleportTo / Respawn)
	/// </summary>
	public void ResetLookaheadImmediate()
	{
		previousPosition = transform.position;
		currentLookahead = new Vector2(player.FacingDirection * idleFacingOffsetX, 0f);

		if (cinemachineFollow != null)
		{
			cinemachineFollow.FollowOffset = baseFollowOffset + new Vector3(currentLookahead.x, currentLookahead.y, 0f);
		}
	}
	
	
	
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		PlayerController pc = player != null ? player : GetComponent<PlayerController>();
		if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.GridInteraction)) return;
		
		Vector3 center = transform.position + Vector3.up;
		Vector3 lookTarget = center + new Vector3(currentLookahead.x, currentLookahead.y, 0f);

		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(center, lookTarget);
		Gizmos.DrawWireSphere(lookTarget, 0.25f);
	}
#endif
}
