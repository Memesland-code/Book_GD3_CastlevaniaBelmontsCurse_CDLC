using UnityEngine;

public class WallInteractionPlayerSystem : MonoBehaviour
{
	[Space(10), Header("Wall Cling & Slide")]
	[Tooltip("Max vertical velocity allowed to cling to wall")]
	[SerializeField] private float maxWallGrabUpwardVelocity = 2.5f;
	[Tooltip("How much time does the character stays frozen on the wall before starting to fall (s)")]
	[SerializeField] private float wallClingDuration = 0.6f;
	[Tooltip("Down slide start speed (m/s)")]
	[SerializeField] private float initialWallSlideSpeed = 1.5f;
	[Tooltip("Down slide acceleration (m/s²)")]
	[SerializeField] private float wallSlideAcceleration = 6f;
	[Tooltip("Down slide max speed (m/s)")]
	[SerializeField] private float maxWallSlideSpeed = 7f;
	
	
	[Header("Wall Jump")]
	[Tooltip("X = push force opposite from wall, Y = jump height")]
	[SerializeField] private Vector2 wallJumpForce = new(2f, 4f);
	[Tooltip("Horizontal input ignore time")]
	[SerializeField] private float wallJumpInputLockDuration = 0.14f;
	[Tooltip("Delay before being able to cling to a wall")]
	[SerializeField] private float wallGrabCooldownDuration = 0.15f;

	
	[Header("Wall Detection for Cling")]
	[SerializeField] private float wallCheckDistance = 0.6f;
	[SerializeField] private float wallCheckUpperHeight = 1.6f; // Chest
	[SerializeField] private float wallCheckLowerHeight = 0.5f; // knees
	[SerializeField] private LayerMask wallLayers;
	
	
	public float MaxWallGrabUpwardVelocity => maxWallGrabUpwardVelocity;
	public float WallClingDuration => wallClingDuration;
	public float InitialWallSlideSpeed => initialWallSlideSpeed;
	public float WallSlideAcceleration => wallSlideAcceleration;
	public float MaxWallSlideSpeed => maxWallSlideSpeed;
	public Vector2 WallJumpForce => wallJumpForce;
	//! private int lastWallDirection;
	
	public float WallCheckDistance => wallCheckDistance;
	public int LastWallDirection { get; set; }
	public float WallCheckUpperHeight => wallCheckUpperHeight;
	public float WallCheckLowerHeight => wallCheckLowerHeight;
	
	
	public float WallGrabCooldownTimer { get; set; }
	public float WallJumpInputLockTimer { get; set; }
	
	public void StartWallGrabCooldown() => WallGrabCooldownTimer = wallGrabCooldownDuration;
	public void StartWallJumpInputLock() => WallJumpInputLockTimer = wallJumpInputLockDuration;

	private PlayerController player;


	private void Awake()
	{
		player = GetComponent<PlayerController>();
	}


	/// <summary>
	/// Wall detection using chest and knees position to validate real platform
	/// </summary>
	public bool IsTouchingWall()
	{
		Vector3 direction = Vector3.right * player.FacingDirection;
		Vector3 upperOrigin = transform.position + Vector3.up * wallCheckUpperHeight;
		Vector3 lowerOrigin = transform.position + Vector3.up * wallCheckLowerHeight;
		
		bool upperHit = Physics.Raycast(upperOrigin, direction, wallCheckDistance, wallLayers, QueryTriggerInteraction.Ignore);
		bool lowerHit = Physics.Raycast(lowerOrigin, direction, wallCheckDistance, wallLayers, QueryTriggerInteraction.Ignore);
		
		return upperHit && lowerHit;
	}


	public void PerformWallJump(int wallDir)
	{
		player.Animator.SetTrigger(player.animJump);
		player.ClearAirCoyoteTime();
		int jumpAwayDirection = -wallDir;
			
		player.SetFacingDirection(jumpAwayDirection > 0);
			
		// Push player up + opposite of wall
		player.Rigidbody.linearVelocity = new Vector3(jumpAwayDirection * WallJumpForce.x, WallJumpForce.y, 0f);
			
		StartWallJumpInputLock();
	}
}
