using UnityEngine;

public class GridInteractionPlayerSystem : MonoBehaviour
{
	[Space(10), Header("Grid Platform")]
	[SerializeField] private LayerMask gridPlatformLayer;
	[Tooltip("Hands height when suspended")]
	[SerializeField] private float gridHangHandHeight = 2.2f;
	
	[Tooltip("Short delay after grabbing platform (s)")]
	[SerializeField] private float gridGrabInputLockDuration = 0.1f;
	
	[Tooltip("Time to climb up on platform")]
	[SerializeField] private float gridClimbDuration = 0.14f;
	
	[Tooltip("Optionnal x offset when Move.x is pressed while climbing")]
	[SerializeField] private float gridClimbStepX = 0.35f;
	
	[Tooltip("Duration of grid platform to be ignored while passing through")]
	[SerializeField] private float gridDropThroughDuration = 0.25f;
	
	[Tooltip("Downwards impulse to go through grid (m/s)")]
	[SerializeField] private float gridDropDownSpeed = 8.5f;
	
	public GridPlatform CurrentGridPlatform { get; private set; }
	public float GridGrabInputLockDuration => gridGrabInputLockDuration;
	public float GridClimbDuration => gridClimbDuration;
	public float GridClimbStepX => gridClimbStepX;
	public float GridDropThroughTimer { get; set; }
	public bool CanGrabGrid => GridDropThroughTimer <= 0f;
	public float GridHangHandHeight => gridHangHandHeight;

	public GridPlatform IgnoredGridPlatform { get; set; }
	private readonly Collider[] nearbyGridBuffer = new Collider[8];


	private PlayerController player;


	private void Awake()
	{
		player = GetComponent<PlayerController>();
	}


	/// <summary>
	/// Enable or disables dynamically the collision with nearby GridPlatforms
	/// </summary>
	public void UpdateGridPlatformCollisions()
	{
		if (player.CapsuleCollider == null) return;
		
		// Check for full character surface
		Vector3 boxCenter = transform.position + Vector3.up * (player.StandingHeight * 0.5f);
		Vector3 boxHalfExtents = new Vector3(1.5f, player.StandingHeight * 0.75f, 1f);
		
		int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, boxHalfExtents, nearbyGridBuffer, Quaternion.identity, gridPlatformLayer, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < hitCount; i++)
		{
			if (!nearbyGridBuffer[i].TryGetComponent(out GridPlatform platform)) continue;

			bool isExplicitlyIgnored = (platform == IgnoredGridPlatform && GridDropThroughTimer > 0f);
			bool isHangingOnThis = (player.StateMachine.CurrentState == player.GridHangingState && platform == CurrentGridPlatform);

			bool feetAboveSurface = transform.position.y >= (platform.TopSurfaceY - 0.08f);

			bool shouldIgnoreCollision = isExplicitlyIgnored || isHangingOnThis || !feetAboveSurface;
			Physics.IgnoreCollision(player.CapsuleCollider, platform.PlatformCollider, shouldIgnoreCollision);
		}
	}


	/// <summary>
	/// Check while in AirState if character's hands touch a GridPlatform
	/// </summary>
	public bool TryGrabGridPlatform(out GridPlatform detectedPlatform)
	{
		detectedPlatform = null;
		if (!CanGrabGrid) return false;
		
		Vector3 grabCenter = transform.position + Vector3.up * gridHangHandHeight;
		Vector3 grabHalfExtents = new Vector3(player.CapsuleCollider.radius * 0.8f, 0.2f, 0.5f);
		
		int hitCount = Physics.OverlapBoxNonAlloc(grabCenter, grabHalfExtents, nearbyGridBuffer, Quaternion.identity, gridPlatformLayer, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < hitCount; i++)
		{
			if (!nearbyGridBuffer[i].TryGetComponent(out GridPlatform platform)) continue;
			if (platform == IgnoredGridPlatform && GridDropThroughTimer > 0f) continue;
			
			// Security to check that characters is under platform to avoid grabbing from above
			if (transform.position.y < platform.TopSurfaceY - 0.25f)
			{
				detectedPlatform = platform;
				return true;
			}
		}

		return false;
	}


	public void EnterGridHang(GridPlatform platform)
	{
		CurrentGridPlatform = platform;
		player.StateMachine.ChangeState(player.GridHangingState);
	}


	public void AlignToGridPlatform()
	{
		if (CurrentGridPlatform == null) return;

		Vector3 pos = transform.position;
		pos.y = CurrentGridPlatform.TopSurfaceY - gridHangHandHeight;
		transform.position = pos;
	}


	public void StartGridDropThrough(GridPlatform platform, bool isFromAbove = true)
	{
		if (platform == null) return;
		
		IgnoredGridPlatform = platform;
		GridDropThroughTimer = gridDropThroughDuration;
		Physics.IgnoreCollision(player.CapsuleCollider, platform.PlatformCollider, true);

		if (CurrentGridPlatform == platform)
		{
			CurrentGridPlatform = null;
		}

		float downSpeed = isFromAbove ? gridDropDownSpeed : gridDropDownSpeed / 3;
		
		if (player.Rigidbody != null)
		{
			player.UnfreezePhysicsFromLedge();
			player.Rigidbody.linearVelocity = new Vector3(player.Rigidbody.linearVelocity.x, -downSpeed, 0f);
		}
	}


	/// <summary>
	/// Detects if character is currently above a GridPlatform
	/// </summary>
	public GridPlatform GetStandingGridPlatform()
	{
		Vector3 rayOrigin = transform.position + Vector3.up * 0.15f;
		if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 0.35f, gridPlatformLayer, QueryTriggerInteraction.Ignore))
		{
			return hit.collider.GetComponent<GridPlatform>();
		}

		return null;
	}



	public void CompleteGridClimb()
	{
		if (CurrentGridPlatform != null)
		{
			Physics.IgnoreCollision(player.CapsuleCollider, CurrentGridPlatform.PlatformCollider, false);
			CurrentGridPlatform = null;
		}
		
		player.UnfreezePhysicsFromLedge();
		Physics.SyncTransforms();
		
		// If player holds left/right character start moving right after climbing up
		float moveX = player.Input != null ? player.Input.Move.x : 0f;
		player.Rigidbody.linearVelocity = new Vector3(moveX * player.GroundSpeed, 0f, 0f);
	}
}
