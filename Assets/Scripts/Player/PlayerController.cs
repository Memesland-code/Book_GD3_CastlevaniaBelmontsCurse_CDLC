using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(LedgeGrab))]
[RequireComponent(typeof(SafePositionTracker))]
public class PlayerController : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField] private float groundSpeed = 7.5f;
	[SerializeField] private float airSpeed = 8.5f;
	
	
	[Space(10), Header("Jump Settings")]
	[SerializeField] private float upForce = 2.5f;
	[SerializeField] private int maxAirJumpsNumber;
	
	
	[Space(10), Header("Gravity & Fall")]
	[SerializeField] private float lowJumpGravityScale = 4.5f;
	[SerializeField] private float gravityScale = 5f;
	[SerializeField] private float fallGravityScale = 8f;
	[SerializeField] private float maxFallSpeed = 18f;
	[SerializeField] private float airAcceleration = 45f;
	[SerializeField] private float airDeceleration = 20f;
	
	
	[Space(10), Header("Physic Materials")]
	[SerializeField] private PhysicsMaterial groundMaterial;
	[SerializeField] private PhysicsMaterial airMaterial;
	
	
	[Space(10), Header("Slide Settings")]
	[Tooltip("Constant speed at start (m/s)")]
	[SerializeField] private float slideSpeed = 14f;
	
	[Tooltip("Lowest speed at very end of slide (m/s)")]
	[SerializeField] private float slideEndSpeed = 3f;
	
	[Tooltip("Total duration of basic slide without tunnel (s)")]
	[SerializeField] private float slideDuration = 0.45f;
	
	[Tooltip("Proportion of full speed during slide (%/100)")]
	[Range(0.1f, 1f)]
	[SerializeField] private float slideConstantRatio = 0.8f;
	
	
	[Header("Hitbox & Ceiling Detection")]
	[SerializeField] private float slidingHeight = 1.05f;
	[Tooltip("Layer of solid walls and ceilings")]
	[SerializeField] private LayerMask ceilingObstacleLayers;
	
	
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
	[Tooltip("Grace windows in which player can still jump after leaving a wall (s)")]
	[SerializeField] private float wallCoyoteDuration = 0.14f;
	
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
	
	
	
	//* Internal Components
	private CinemachineCamera camera;
	private GroundDetector groundDetector;
	private CapsuleCollider capsuleCollider;
	private RigidbodyConstraints originalConstraints;
	
	
	
	//* Public Getters for PlayerState
	public InputManager Input { get; private set; }
	public Rigidbody Rigidbody { get; private set; }
	public LedgeGrab LedgeDetector { get; private set; }
	public SafePositionTracker SafeTracker { get; private set; }

	// Jump Settings
	public float GroundSpeed => groundSpeed;
	public float AirSpeed => airSpeed;
	public bool CanJump => currentJumpsCombo < maxAirJumpsNumber;

	// Gravity Related
	public float GravityScale => gravityScale;
	public float FallGravityScale => fallGravityScale;
	public float LowJumpGravityScale => lowJumpGravityScale;
	
	// Sliding
	public float SlideSpeed => slideSpeed;
	public float SlideEndSpeed => slideEndSpeed;
	public float SlideDuration => slideDuration;
	public float SlideConstantRatio => slideConstantRatio;
	
	// Wall Cling
	public float MaxWallGrabUpwardVelocity => maxWallGrabUpwardVelocity;
	public float WallClingDuration => wallClingDuration;
	public float InitialWallSlideSpeed => initialWallSlideSpeed;
	public float WallSlideAcceleration => wallSlideAcceleration;
	public float MaxWallSlideSpeed => maxWallSlideSpeed;
	public Vector2 WallJumpForce => wallJumpForce;
	public bool CanWallCoyoteJump => wallCoyoteTimer > 0f;
	public int LastWallDirection => lastWallDirection;

	// Other
	public bool IsGrounded => groundDetector != null && groundDetector.touched;
	public RigidbodyConstraints OriginalConstraints => originalConstraints;
	public bool CanGrabWall => wallGrabCooldownTimer <= 0f;
	public bool IsWallJumpInputLocked => wallJumpInputLockTimer > 0f;
	public bool IsFacingRight => isFacingRight;
	public int FacingDirection => IsFacingRight ? 1 : -1;

	

	//* State Machine & states instances (FSM)
	public PlayerStateMachine StateMachine { get; private set; }
	public PlayerGroundedState GroundedState { get; private set; }
	public PlayerAirState AirState { get; private set; }
	public PlayerLedgeHangingState LedgeHangingState { get; private set; }
	public PlayerLedgeClimbingState LedgeClimbingState { get; private set; }
	public PlayerRespawnState RespawnState { get; private set; }
	public PlayerSlideState SlideState { get; private set; }
	public PlayerWallState WallState { get; private set; }
	
	
	
	//* General
	private bool isFacingRight = true;
	private int currentJumpsCombo;
	private float standingHeight;
	private float wallCoyoteTimer;
	private int lastWallDirection;
	
	// Internal timers
	private float wallGrabCooldownTimer;
	private float wallJumpInputLockTimer;
	
	
	
	[Space(20), Header("Cheats")]
	public bool noClip;

	

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		Input = GetComponent<InputManager>();
		LedgeDetector = GetComponent<LedgeGrab>();
		SafeTracker = GetComponent<SafePositionTracker>();
		
		capsuleCollider = GetComponent<CapsuleCollider>();
		groundDetector = GetComponentInChildren<GroundDetector>();
		originalConstraints = Rigidbody.constraints;

		GameObject camObj = GameObject.FindWithTag("MainCamera");
		if (camObj != null) camera = camObj.GetComponent<CinemachineCamera>();
		
		// State machine states init
		StateMachine = new PlayerStateMachine();
		GroundedState = new PlayerGroundedState(this, StateMachine);
		AirState = new PlayerAirState(this, StateMachine);
		LedgeHangingState = new PlayerLedgeHangingState(this, StateMachine);
		LedgeClimbingState = new PlayerLedgeClimbingState(this, StateMachine);
		RespawnState = new PlayerRespawnState(this, StateMachine);
		SlideState = new PlayerSlideState(this, StateMachine);
		WallState = new PlayerWallState(this, StateMachine);

		standingHeight = capsuleCollider.height;
	}

	
	
	private void Start()
	{
		if (camera != null)
		{
			camera.Target.TrackingTarget = transform;
		}
		
		StateMachine.Initialize(GroundedState);
	}

	
	
	private void Update()
	{
		if (wallGrabCooldownTimer > 0f) wallGrabCooldownTimer -= Time.deltaTime;
		if (wallJumpInputLockTimer > 0f) wallJumpInputLockTimer -= Time.deltaTime;
		if (wallCoyoteTimer > 0f) wallCoyoteTimer -= Time.deltaTime;
		
		StateMachine.CurrentState.HandleInput();
		StateMachine.CurrentState.LogicUpdate();
	}
	
	
	
	private void FixedUpdate()
	{
		StateMachine.CurrentState.PhysicsUpdate();
	}
	
	
	
	//* ###### State machine functions ######
	
	
	
	// ---- Basic movements ----
	
	
	public void ApplyHorizontalMovement(float speed)
	{
		if (Input == null || Rigidbody == null) return;
		
		if (noClip)
		{
			HandleNoClipMovement();
			return;
		}
		
		Rigidbody.linearVelocity = new Vector3(Input.Move.x * speed, Rigidbody.linearVelocity.y, 0f);
	}
	
	
	
	public void SetFacingDirection(bool? forceFaceRight = null)
	{
		if (Mathf.Approximately(transform.rotation.eulerAngles.y, 0f))
		{
			transform.rotation = Quaternion.Euler(0f, isFacingRight ? 90f : -90f, 0f);
		}

		bool targetFacing = isFacingRight;

		if (forceFaceRight.HasValue)
		{
			targetFacing = forceFaceRight.Value;
		}
		else if (Input != null)
		{
			if (Input.Move.x < -0.01f) targetFacing = false;
			else if (Input.Move.x > 0.01f) targetFacing = true;
		}
		
		if (isFacingRight == targetFacing) return;
		
		isFacingRight = targetFacing;
		transform.rotation = Quaternion.Euler(0f, isFacingRight ? 90f : -90f, 0f);
	}
	
	
	
	public void ExecuteJump()
	{
		float jumpForce = Mathf.Sqrt(upForce * -3 * (Physics.gravity.y * gravityScale));
		
		Rigidbody.linearVelocity = new Vector3(Rigidbody.linearVelocity.x, 0f, 0f);
		Rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
		currentJumpsCombo++;
	}

	
	
	public void ResetJumpsCombo()
	{
		currentJumpsCombo = 0;
	}

	
	
	public void ApplyGravityForce(float scale)
	{
		if (Rigidbody.isKinematic) return;
		Rigidbody.AddForce(Physics.gravity * ((scale - 1f) * Rigidbody.mass));
	}



	public void ClampMaxFallSpeed()
	{
		if (Rigidbody == null || noClip) return;

		if (Rigidbody.linearVelocity.y < -maxFallSpeed)
		{
			Rigidbody.linearVelocity = new Vector3(Rigidbody.linearVelocity.x, -maxFallSpeed, 0f);
		}
	}



	public void ApplyAirHorizontalMovement(float maxAirSpeed)
	{
		if (Input == null || Rigidbody == null) return;

		if (noClip)
		{
			HandleNoClipMovement();
			return;
		}
		
		float targetSpeed = Input.Move.x * maxAirSpeed;
		float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration;
		
		float newVelocityX = Mathf.MoveTowards(Rigidbody.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
		
		Rigidbody.linearVelocity = new Vector3(newVelocityX, Rigidbody.linearVelocity.y, 0f);
	}

	
	
	public void SetPhysicsMaterial(bool isGrounded)
	{
		capsuleCollider.sharedMaterial = isGrounded ? groundMaterial : airMaterial;
	}
	
	
	// ---- Sliding ----


	public void SetSlideHitbox(bool isSliding)
	{
		float minAllowedHeight = capsuleCollider.radius * 2f;
		float targetHeight = isSliding
			? Mathf.Max(slidingHeight, minAllowedHeight)
			: Mathf.Max(standingHeight, minAllowedHeight);
		
		capsuleCollider.height = targetHeight;
		capsuleCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);
	}



	public bool CanStandUp()
	{
		// Error margin to avoid detecting a wall
		float checkRadius = capsuleCollider.radius * 0.85f;
		float minAllowedHeight = capsuleCollider.radius * 2f;
		float effectiveSlideHeight = Mathf.Max(slidingHeight, minAllowedHeight);
		
		Vector3 bottomSphere = transform.position + Vector3.up * effectiveSlideHeight;
		Vector3 topSphere = transform.position + Vector3.up * (standingHeight - checkRadius);
		
		if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

		return !Physics.CheckCapsule(
			bottomSphere,
			topSphere,
			checkRadius,
			ceilingObstacleLayers,
			QueryTriggerInteraction.Ignore
		);
	}
	
	
	// ---- Ledge & Climbing ----
	
	
	public void FreezePhysicsForLedge()
	{
		Rigidbody.linearVelocity = Vector3.zero;
		Rigidbody.isKinematic = true;
		Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
	}

	
	
	public void UnfreezePhysicsFromLedge()
	{
		Rigidbody.isKinematic = false;
		Rigidbody.constraints = originalConstraints;
	}
	
	

	public void AlignToLedge()
	{
		Vector3 hangPosition = LedgeDetector.CurrentLedgeHit.point - (transform.forward * LedgeDetector.WallDistanceOffset);
		hangPosition.y -= (LedgeDetector.LedgeCheckOrigin.position.y - transform.position.y);
		transform.position = hangPosition;
		transform.rotation = Quaternion.LookRotation(-LedgeDetector.CurrentWallHit.normal);
	}
	
	
	// ---- Cling & Wall Jump ----
	
	
	public void StartWallGrabCooldown() => wallGrabCooldownTimer = wallGrabCooldownDuration;
	public void StartWallJumpInputLock() => wallJumpInputLockTimer = wallJumpInputLockDuration;
	

	/// <summary>
	/// Wall detection using chest and knees position to validate real platform
	/// </summary>
	public bool IsTouchingWall()
	{
		Vector3 direction = Vector3.right * FacingDirection;
		Vector3 upperOrigin = transform.position + Vector3.up * wallCheckUpperHeight;
		Vector3 lowerOrigin = transform.position + Vector3.up * wallCheckLowerHeight;
		
		bool upperHit = Physics.Raycast(upperOrigin, direction, wallCheckDistance, wallLayers, QueryTriggerInteraction.Ignore);
		bool lowerHit = Physics.Raycast(lowerOrigin, direction, wallCheckDistance, wallLayers, QueryTriggerInteraction.Ignore);
		
		return upperHit && lowerHit;
	}


	public void StartWallCoyoteTime(int wallDir)
	{
		lastWallDirection = wallDir;
		wallCoyoteTimer = wallCoyoteDuration;
	}


	public void ClearWallCoyoteTime()
	{
		wallCoyoteTimer = 0f;
	}


	public void PerformWallJump(int wallDir)
	{
		ClearWallCoyoteTime();
		int jumpAwayDirection = -wallDir;
			
		SetFacingDirection(jumpAwayDirection > 0);
			
		// Push player up + opposite of wall
		Rigidbody.linearVelocity = new Vector3(jumpAwayDirection * WallJumpForce.x, WallJumpForce.y, 0f);
			
		StartWallJumpInputLock();
	}
	
	
	// ---- Death & Respawn ----


	public void TriggerTrapRespawn()
	{
		if (StateMachine.CurrentState == RespawnState) return;
		
		StateMachine.ChangeState(RespawnState);
	}
	
	
	// ---- Gizmos & Debug ----


	private void HandleNoClipMovement()
	{
		transform.position += new Vector3(Input.Move.x / 2f, Input.Move.y / 2f, 0f);
	}


	private void OnDrawGizmosSelected()
	{
		// Slide checks
		if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>();
		if (capsuleCollider == null) return;

		float checkRadius = capsuleCollider.radius * 0.85f;
		float minAllowedHeight = capsuleCollider.radius * 2f;
		float effectiveSlideHeight = Mathf.Max(slidingHeight, minAllowedHeight);

		Vector3 bottomSphere = transform.position + Vector3.up * effectiveSlideHeight;
		Vector3 topSphere = transform.position + Vector3.up * (standingHeight - checkRadius);
		if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

		Gizmos.color = Application.isPlaying && !CanStandUp() ? Color.red : Color.green;
		Gizmos.DrawWireSphere(bottomSphere, checkRadius);
		Gizmos.DrawWireSphere(topSphere, checkRadius);
		Gizmos.DrawLine(bottomSphere, topSphere);
		
		
		// Wall check for cling
		Vector3 direction = Vector3.right * FacingDirection;
		Vector3 upperOrigin = transform.position + Vector3.up * wallCheckUpperHeight;
		Vector3 lowerOrigin = transform.position + Vector3.up * wallCheckLowerHeight;

		Gizmos.color = Application.isPlaying && IsTouchingWall() ? Color.cyan : Color.yellow;
		Gizmos.DrawLine(upperOrigin, upperOrigin + direction * wallCheckDistance);
		Gizmos.DrawLine(lowerOrigin, lowerOrigin + direction * wallCheckDistance);
	}
}
