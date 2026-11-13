using JetBrains.Annotations;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(LedgeGrabPlayerSystem))]
[RequireComponent(typeof(SafePositionTrackerPlayerSystem))]
[RequireComponent(typeof(SlidePlayerSystem))]
public class PlayerController : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField] private float groundSpeed = 7.5f;
	[SerializeField] private float airSpeed = 8.5f;
	
	
	[Space(10), Header("Jump Settings")]
	[SerializeField] private float upForce = 2.5f;
	[SerializeField] private int maxAirJumpsNumber;
	[Tooltip("Grace windows in which player can still jump after leaving a wall (s)")]
	[SerializeField] private float airCoyoteDuration = 0.14f;
	
	
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
	
	
	
	//* Internal Components
	private CinemachineCamera camera;
	private GroundDetectorPlayerSystem groundDetector;
	public CapsuleCollider CapsuleCollider { get; private set; }
	private RigidbodyConstraints originalConstraints;
	
	
	
	//* Variables Getters for PlayerState
	public InputManager Input { get; private set; }
	public Rigidbody Rigidbody { get; private set; }
	public LedgeGrabPlayerSystem LedgeGrab { get; private set; }
	public SafePositionTrackerPlayerSystem SafePositionTracker { get; private set; }
	public SlidePlayerSystem Slide { get; private set; }

	// Jump Settings
	public float GroundSpeed => groundSpeed;
	public float AirSpeed => airSpeed;
	private int currentJumpsCombo;
	public bool CanJump => currentJumpsCombo < maxAirJumpsNumber;

	// Gravity Related
	public float GravityScale => gravityScale;
	public float FallGravityScale => fallGravityScale;
	public float LowJumpGravityScale => lowJumpGravityScale;
	
	// Wall Cling
	public float MaxWallGrabUpwardVelocity => maxWallGrabUpwardVelocity;
	public float WallClingDuration => wallClingDuration;
	public float InitialWallSlideSpeed => initialWallSlideSpeed;
	public float WallSlideAcceleration => wallSlideAcceleration;
	public float MaxWallSlideSpeed => maxWallSlideSpeed;
	public Vector2 WallJumpForce => wallJumpForce;
	private float airCoyoteTimer;
	public bool CanWallCoyoteJump => airCoyoteTimer > 0f;
	private int lastWallDirection;
	public int LastWallDirection => lastWallDirection;
	
	// Grid platform
	public GridPlatform CurrentGridPlatform { get; private set; }
	public float GridGrabInputLockDuration => gridGrabInputLockDuration;
	public float GridClimbDuration => gridClimbDuration;
	public float GridClimbStepX => gridClimbStepX;
	private float gridDropThroughTimer;
	public bool CanGrabGrid => gridDropThroughTimer <= 0f;

	//* General
	public bool IsGrounded => groundDetector != null && groundDetector.touched;
	
	public bool CanGrabWall => wallGrabCooldownTimer <= 0f;
	
	public bool IsWallJumpInputLocked => wallJumpInputLockTimer > 0f;
	
	public bool IsFacingRight => isFacingRight;
	
	private float jumpGraceTimer;
	public bool IsJumpGraceActive => jumpGraceTimer > 0f;
	
	private bool isFacingRight = true;
	public int FacingDirection => IsFacingRight ? 1 : -1;
	
	private float standingHeight;
	public float StandingHeight => standingHeight;

	private GridPlatform ignoredGridPlatform;
	private readonly Collider[] nearbyGridBuffer = new Collider[8];
	

	
	//* State Machine & states instances (FSM)
	public PlayerStateMachine StateMachine { get; private set; }
	public PlayerGroundedState GroundedState { get; private set; }
	public PlayerAirState AirState { get; private set; }
	public PlayerLedgeHangingState LedgeHangingState { get; private set; }
	public PlayerLedgeClimbingState LedgeClimbingState { get; private set; }
	public PlayerRespawnState RespawnState { get; private set; }
	public PlayerSlideState SlideState { get; private set; }
	public PlayerWallState WallState { get; private set; }
	public PlayerGridHangingState GridHangingState { get; private set; }
	
	
	
	// Internal timers
	private float wallGrabCooldownTimer;
	private float wallJumpInputLockTimer;
	
	
	
	[Space(20), Header("Cheats")]
	public bool noClip;
	
	#if UNITY_EDITOR
	[Space(10), Header("Debug")]
	[SerializeField][UsedImplicitly] private string currentStateName;
	#endif

	

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		Input = GetComponent<InputManager>();
		LedgeGrab = GetComponent<LedgeGrabPlayerSystem>();
		SafePositionTracker = GetComponent<SafePositionTrackerPlayerSystem>();
		Slide = GetComponent<SlidePlayerSystem>();
		
		CapsuleCollider = GetComponent<CapsuleCollider>();
		groundDetector = GetComponentInChildren<GroundDetectorPlayerSystem>();
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
		GridHangingState = new PlayerGridHangingState(this, StateMachine);

		standingHeight = CapsuleCollider.height;
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
		#if UNITY_EDITOR
		currentStateName = StateMachine.CurrentState.GetType().Name;
		#endif
		
		RunTimersUpdate();
		StateMachine.CurrentState.HandleInput();
		StateMachine.CurrentState.LogicUpdate();
	}


	private void RunTimersUpdate()
	{
		if (wallGrabCooldownTimer > 0f) wallGrabCooldownTimer -= Time.deltaTime;
		if (wallJumpInputLockTimer > 0f) wallJumpInputLockTimer -= Time.deltaTime;
		if (airCoyoteTimer > 0f) airCoyoteTimer -= Time.deltaTime;
		if (jumpGraceTimer > 0f) jumpGraceTimer -= Time.deltaTime;
		
		if (gridDropThroughTimer > 0f)
		{
			gridDropThroughTimer -= Time.deltaTime;

			if (gridDropThroughTimer <= 0f)
			{
				ignoredGridPlatform = null;
			}
		}
	}
	
	
	
	private void FixedUpdate()
	{
		UpdateGridPlatformCollisions();
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
		ClearWallCoyoteTime();
		SetPhysicsMaterial(isGrounded: false);
		
		float jumpVelocity = Mathf.Sqrt(upForce * -3f * (Physics.gravity.y * gravityScale)) / Rigidbody.mass;
		Rigidbody.linearVelocity = new Vector3(Rigidbody.linearVelocity.x, jumpVelocity, 0);
		
		jumpGraceTimer = 0.08f;
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
		CapsuleCollider.sharedMaterial = isGrounded ? groundMaterial : airMaterial;
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
		Vector3 hangPosition = LedgeGrab.CurrentLedgeHit.point - (transform.forward * LedgeGrab.WallDistanceOffset);
		hangPosition.y -= (LedgeGrab.LedgeCheckOrigin.position.y - transform.position.y);
		transform.position = hangPosition;
		transform.rotation = Quaternion.LookRotation(-LedgeGrab.CurrentWallHit.normal);
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


	public void StartAirCoyoteTime(int? wallDir = 0)
	{
		lastWallDirection = wallDir ?? lastWallDirection;
		airCoyoteTimer = airCoyoteDuration;
	}


	public void ClearWallCoyoteTime()
	{
		airCoyoteTimer = 0f;
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
	
	
	// ---- Grid Platforms ----


	/// <summary>
	/// Enable or disables dynamically the collision with nearby GridPlatforms
	/// </summary>
	private void UpdateGridPlatformCollisions()
	{
		if (CapsuleCollider == null) return;
		
		// Check for full character surface
		Vector3 boxCenter = transform.position + Vector3.up * (standingHeight * 0.5f);
		Vector3 boxHalfExtents = new Vector3(1.5f, standingHeight * 0.75f, 1f);
		
		int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, boxHalfExtents, nearbyGridBuffer, Quaternion.identity, gridPlatformLayer, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < hitCount; i++)
		{
			if (!nearbyGridBuffer[i].TryGetComponent(out GridPlatform platform)) continue;

			bool isExplicitlyIgnored = (platform == ignoredGridPlatform && gridDropThroughTimer > 0f);
			bool isHangingOnThis = (StateMachine.CurrentState == GridHangingState && platform == CurrentGridPlatform);

			bool feetAboveSurface = transform.position.y >= (platform.TopSurfaceY - 0.08f);

			bool shouldIgnoreCollision = isExplicitlyIgnored || isHangingOnThis || !feetAboveSurface;
			Physics.IgnoreCollision(CapsuleCollider, platform.PlatformCollider, shouldIgnoreCollision);
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
		Vector3 grabHalfExtents = new Vector3(CapsuleCollider.radius * 0.8f, 0.2f, 0.5f);
		
		int hitCount = Physics.OverlapBoxNonAlloc(grabCenter, grabHalfExtents, nearbyGridBuffer, Quaternion.identity, gridPlatformLayer, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < hitCount; i++)
		{
			if (!nearbyGridBuffer[i].TryGetComponent(out GridPlatform platform)) continue;
			if (platform == ignoredGridPlatform && gridDropThroughTimer > 0f) continue;
			
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
		StateMachine.ChangeState(GridHangingState);
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
		
		ignoredGridPlatform = platform;
		gridDropThroughTimer = gridDropThroughDuration;
		Physics.IgnoreCollision(CapsuleCollider, platform.PlatformCollider, true);

		if (CurrentGridPlatform == platform)
		{
			CurrentGridPlatform = null;
		}

		float downSpeed = isFromAbove ? gridDropDownSpeed : gridDropDownSpeed / 3;
		
		if (Rigidbody != null)
		{
			UnfreezePhysicsFromLedge();
			Rigidbody.linearVelocity = new Vector3(Rigidbody.linearVelocity.x, -downSpeed, 0f);
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
			Physics.IgnoreCollision(CapsuleCollider, CurrentGridPlatform.PlatformCollider, false);
			CurrentGridPlatform = null;
		}
		
		UnfreezePhysicsFromLedge();
		Physics.SyncTransforms();
		
		// If player holds left/right character start moving right after climbing up
		float moveX = Input != null ? Input.Move.x : 0f;
		Rigidbody.linearVelocity = new Vector3(moveX * groundSpeed, 0f, 0f);
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
		if (CapsuleCollider == null) CapsuleCollider = GetComponent<CapsuleCollider>();
		if (Slide == null) Slide = GetComponent<SlidePlayerSystem>();
		if (CapsuleCollider != null && Slide != null)
		{
			if (CapsuleCollider == null) return;

			float checkRadius = CapsuleCollider.radius * 0.85f;
			float minAllowedHeight = CapsuleCollider.radius * 2f;
			float effectiveSlideHeight = Mathf.Max(Slide.SlidingHeight, minAllowedHeight);

			Vector3 bottomSphere = transform.position + Vector3.up * effectiveSlideHeight;
			Vector3 topSphere = transform.position + Vector3.up * (standingHeight - checkRadius);
			if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

			Gizmos.color = Application.isPlaying && !Slide.CanStandUp() ? Color.red : Color.green;
			Gizmos.DrawWireSphere(bottomSphere, checkRadius);
			Gizmos.DrawWireSphere(topSphere, checkRadius);
			Gizmos.DrawLine(bottomSphere, topSphere);
		}

		// Wall check for cling
		{
			Vector3 direction = Vector3.right * FacingDirection;
			Vector3 upperOrigin = transform.position + Vector3.up * wallCheckUpperHeight;
			Vector3 lowerOrigin = transform.position + Vector3.up * wallCheckLowerHeight;

			Gizmos.color = Application.isPlaying && IsTouchingWall() ? Color.cyan : Color.yellow;
			Gizmos.DrawLine(upperOrigin, upperOrigin + direction * wallCheckDistance);
			Gizmos.DrawLine(lowerOrigin, lowerOrigin + direction * wallCheckDistance);
		}
		
		
		// Grid platform grab check
		if (CapsuleCollider != null)
		{
			Vector3 grabCenter = transform.position + Vector3.up * gridHangHandHeight;
			Vector3 grabSize = new(CapsuleCollider.radius * 1.6f, 0.4f, 1f);

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireCube(grabCenter, grabSize);
		}
	}
}
