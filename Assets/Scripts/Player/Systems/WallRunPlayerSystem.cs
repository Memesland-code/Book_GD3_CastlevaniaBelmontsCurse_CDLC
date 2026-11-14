using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class WallRunPlayerSystem : MonoBehaviour
{
	[Header("Detection")]
	[SerializeField] private LayerMask wallRunLayer;
	[SerializeField] private float wallCheckDistance = 0.65f;
	[SerializeField] private float upperCheckHeight = 1.5f;
	[SerializeField] private float lowerCheckHeight = 0.5f;
	[Tooltip("Height checks for end of wall run wall")]
	[SerializeField] private float endCheckHeight = 1.2f;
	[SerializeField] private float grabCooldownDuration = 0.2f;

	[Header("Movement & Visuals")]
	[Tooltip("Upward speed (m/s)")]
	[SerializeField] private float wallRunSpeed = 11f;
	[Tooltip("Distance between center of capsule and wall surface (~capsule radius)")]
	[SerializeField] private float wallSurfaceOffset = 0.48f;
	[Tooltip("Offset with wall")]
	[SerializeField] private float visualWallStickOffset = 0.48f;
	[Tooltip("Height offset to align with new hitbox")]
	[SerializeField] private float visualWallHeightOffset = 0.5f;
	[SerializeField] private Transform visualRoot;

	[Header("Exits & Jumps")]
	[Tooltip("X = push force opposite from wall, Y = jump height")]
	[SerializeField] private Vector2 wallRunJumpForce = new(9f, 13f);
	[Tooltip("Automatic height jump on wall end")]
	[SerializeField] private float endOfWallUpwardBoost = 8.5f;
	[Tooltip("The time window in which the character can auto run wall after a jump (s)")]
	[SerializeField] private float chainAutoGrabDuration = 0.55f;

	private PlayerController player;
	private float grabCooldownTimer;
	private Quaternion defaultVisualLocationRotation;
	private Vector3 defaultVisualLocalPosition;

	private float autoGrabTimer;
	private int blockedWallDir;
	
	public bool CanWallRun => grabCooldownTimer <= 0f;


	private void Awake()
	{
		player = GetComponent<PlayerController>();

		if (visualRoot != null)
		{
			defaultVisualLocationRotation = visualRoot.localRotation;
			defaultVisualLocalPosition = visualRoot.localPosition;
		}
	}


	private void Update()
	{
		if (grabCooldownTimer > 0f)
		{
			grabCooldownTimer -= Time.deltaTime;
			if (grabCooldownTimer <= 0f)
			{
				blockedWallDir = 0;
			}
		}
		
		if (autoGrabTimer > 0f)
		{
			autoGrabTimer -= Time.deltaTime;
		}
	}



	public void StartGrabCooldown()
	{
		grabCooldownTimer = grabCooldownDuration;
	}



	/// <summary>
	/// Checks from AirState if player touches a wall & holds input towards wall
	/// Both upward and downward raycasts prevent activation if character is walking above the wall
	/// </summary>
	public bool TryDetectWallRun(out int wallDir, out RaycastHit wallHit)
	{
		wallDir = 0;
		wallHit = default;

		if (player.Input == null || player.IsGrounded) return false;

		float moveX = player.Input.Move.x;
		bool hasManualInput = Mathf.Abs(moveX) >= 0.1f;
		bool hasAutoGrab = autoGrabTimer > 0f;

		if (!hasManualInput && !hasAutoGrab) return false;
		
		//del if (Mathf.Abs(moveX) < 0.1f) return false;

		// Priority to manual input, else test to jump direction
		int targetDir = hasManualInput ? (moveX > 0f ? 1 : -1) : player.FacingDirection;

		if (grabCooldownTimer > 0f && (blockedWallDir == 0 || blockedWallDir == targetDir)) return false;
		
		Vector3 rayDir = Vector3.right * targetDir;
		Vector3 upperOrigin = transform.position + Vector3.up * upperCheckHeight;
		Vector3 lowerOrigin = transform.position + Vector3.up * lowerCheckHeight;
		
		bool upperHit = Physics.Raycast(upperOrigin, rayDir, out RaycastHit hitInfo, wallCheckDistance, wallRunLayer, QueryTriggerInteraction.Ignore);
		bool lowerHit = Physics.Raycast(lowerOrigin, rayDir, wallCheckDistance, wallRunLayer, QueryTriggerInteraction.Ignore);

		if (upperHit && lowerHit)
		{
			wallDir = targetDir;
			wallHit = hitInfo;
			return true;
		}
		
		return false;
	}



	/// <summary>
	/// Checks if wall run continues in front of character during ascension
	/// Returns false if void or classic wall above character
	/// </summary>
	public bool IsStillOnWallRunSurface()
	{
		Vector3 origin = transform.position + Vector3.up * upperCheckHeight;
		Vector3 rayDir = Vector3.right * player.CurrentWallDirection;
		
		return Physics.Raycast(origin, rayDir, wallCheckDistance + 0.15f, wallRunLayer, QueryTriggerInteraction.Ignore);
	}



	/// <summary>
	/// Checks if a ceiling is blocking the character
	/// </summary>
	public bool IsBlockedByCeiling()
	{
		Vector3 origin = transform.position + Vector3.up * upperCheckHeight;
		return Physics.Raycast(origin, Vector3.up, 0.5f, ~0, QueryTriggerInteraction.Ignore);
	}



	public void BeginWallRun(int wallDir, RaycastHit hit)
	{
		player.CurrentWallDirection = wallDir;
		autoGrabTimer = 0f;
		blockedWallDir = 0;
		
		// Orients root towards wall
		player.SetFacingDirection(wallDir > 0);
		
		// Sets capsule collider to exact distance of wall
		Vector3 pos = transform.position;
		pos.x = hit.point.x - (wallDir * wallSurfaceOffset);
		transform.position = pos;

		float compactHeight = player.CapsuleCollider.radius * 2;
		player.CapsuleCollider.height = compactHeight;
		player.CapsuleCollider.center = new Vector3(0f, compactHeight * 0.5f, 0f);
		
		// Sets character visual aligned with wall
		if (visualRoot != null)
		{
			visualRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
			visualRoot.localPosition = defaultVisualLocalPosition + new Vector3(0f, visualWallHeightOffset, visualWallStickOffset);
		}
		
		player.SetPhysicsMaterial(false);
		player.Rigidbody.linearVelocity = new Vector3(0f, wallRunSpeed, 0f);
	}



	/// <summary>
	/// Maintains a constant vertical speed
	/// </summary>
	public void TickWallRunPhysics()
	{
		player.Rigidbody.linearVelocity = new Vector3(0f, wallRunSpeed, 0f);
	}



	public void EndWallRun()
	{
		player.CapsuleCollider.height = player.DefaultColliderHeight;
		player.CapsuleCollider.center = player.DefaultColliderCenter;
		
		if (visualRoot != null)
		{
			visualRoot.localRotation = defaultVisualLocationRotation;
			visualRoot.localPosition = defaultVisualLocalPosition;
		}
	}



	public void ExecuteWallRunJump()
	{
		int previousWallDir = player.CurrentWallDirection;
		EndWallRun();
		StartGrabCooldown();
		
		// Only block just quitted wall
		blockedWallDir = previousWallDir;
		autoGrabTimer = chainAutoGrabDuration;
		
		int jumpAwayDir = -player.CurrentWallDirection;
		player.SetFacingDirection(jumpAwayDir > 0);
		player.StartWallJumpInputLock();

		player.Rigidbody.linearVelocity = new Vector3(jumpAwayDir * wallRunJumpForce.x, wallRunJumpForce.y, 0f);
	}



	public void ExecuteEndOfWallBoost()
	{
		EndWallRun();
		StartGrabCooldown();
		
		player.SetFacingDirection(player.CurrentWallDirection > 0);
		player.Rigidbody.linearVelocity = new Vector3(0f, endOfWallUpwardBoost, 0f);
	}



	public void ExecuteDropOff(bool faceAwayFromWall)
	{
		EndWallRun();
		StartGrabCooldown();

		if (faceAwayFromWall)
		{
			player.SetFacingDirection(player.CurrentWallDirection < 0);
		}

		player.Rigidbody.linearVelocity = Vector3.zero;
	}


	private void OnDrawGizmosSelected()
	{
		int dir = Application.isPlaying && player.CurrentWallDirection != 0 ? player.CurrentWallDirection : 1;
		Vector3 rayDir = Vector3.right * dir;

		Vector3 upperOrigin = transform.position + Vector3.up * upperCheckHeight;
		Vector3 lowerOrigin = transform.position + Vector3.up * lowerCheckHeight;
		Vector3 endOrigin = transform.position + Vector3.up * endCheckHeight;

		Gizmos.color = Color.green;
		Gizmos.DrawLine(upperOrigin, upperOrigin + rayDir * wallCheckDistance);
		Gizmos.DrawLine(lowerOrigin, lowerOrigin + rayDir * wallCheckDistance);

		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(endOrigin, endOrigin + rayDir * (wallCheckDistance + 0.15f));	}
}
