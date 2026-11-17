using JetBrains.Annotations;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(LedgeGrabPlayerSystem))]
[RequireComponent(typeof(SafePositionTrackerPlayerSystem))]
[RequireComponent(typeof(SlidePlayerSystem))]
[RequireComponent(typeof(WallInteractionPlayerSystem))]
[RequireComponent(typeof(GridInteractionPlayerSystem))]
[RequireComponent(typeof(WallRunPlayerSystem))]
[RequireComponent(typeof(WhipPlayerSystem))]
public class PlayerController : MonoBehaviour
{
	public readonly int isRunning = Animator.StringToHash("IsRunning");
	public readonly int animJump = Animator.StringToHash("Jump");

	//* Exposed Settings
	
	[Header("Movement Settings")]
	[SerializeField] private float groundSpeed = 7.5f;
	[SerializeField] private float airSpeed = 8.5f;
	
	
	[Space(10), Header("Jump Settings")]
	[SerializeField] private float upForce = 2.5f;
	[SerializeField] private int maxAirJumpsNumber;
	[Tooltip("Grace windows in which player can still jump after leaving a wall (s)")]
	[SerializeField] private float coyoteDuration = 0.14f;
	[Tooltip("Horizontal input ignore time")]
	[SerializeField] private float wallJumpInputLockDuration = 0.14f;
	
	
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
	
	
	[Header("Environment & Collisions")]
	[SerializeField] private LayerMask solidObstacleLayers;
	public LayerMask SolidObstacleLayers => solidObstacleLayers;
	
	
	
	//* Internal Components
	private CinemachineCamera camera;
	private GroundDetectorPlayerSystem groundDetector;
	public CapsuleCollider CapsuleCollider { get; private set; }
	private RigidbodyConstraints originalConstraints;
	public Animator Animator { get; private set; }
	
	
	//* Variables Getters for PlayerState
	public InputManager Input { get; private set; }
	public Rigidbody Rigidbody { get; private set; }
	public LedgeGrabPlayerSystem LedgeGrabSystem { get; private set; }
	public SafePositionTrackerPlayerSystem SafePositionTrackerSystem { get; private set; }
	public SlidePlayerSystem SlideSystem { get; private set; }
	public WallInteractionPlayerSystem WallInteractionSystem { get; private set; }
	public GridInteractionPlayerSystem GridInteractionSystem { get; private set; }
	public WallRunPlayerSystem WallRunSystem { get; private set; }
	public WhipPlayerSystem WhipSystem { get; private set; }

	// Jump Settings
	public float GroundSpeed => groundSpeed;
	public float AirSpeed => airSpeed;
	private int currentJumpsCombo;
	public bool CanJump => currentJumpsCombo < maxAirJumpsNumber;
	private float coyoteTimer;
	private System.Action pendingCoyoteJumpAction;
	public bool CanCoyoteJump => coyoteTimer > 0f && pendingCoyoteJumpAction != null;
	public void StartWallJumpInputLock() => WallInteractionSystem.WallJumpInputLockTimer = wallJumpInputLockDuration;

	// Gravity Related
	public float GravityScale => gravityScale;
	public float FallGravityScale => fallGravityScale;
	public float LowJumpGravityScale => lowJumpGravityScale;
	

	//* General
	public bool IsGrounded => groundDetector != null && groundDetector.touched;
	
	public bool CanGrabWall => WallInteractionSystem.WallGrabCooldownTimer <= 0f;
	
	public bool IsWallJumpInputLocked => WallInteractionSystem.WallJumpInputLockTimer > 0f;

	private float gridGrabLockTimer;
	public bool IsGridGrabLocked => gridGrabLockTimer > 0f;
	
	private bool isFacingRight = true;
	public bool IsFacingRight => isFacingRight;
	public int FacingDirection => IsFacingRight ? 1 : -1;
	
	public int CurrentWallDirection { get; set; }
	
	private float jumpGraceTimer;
	public bool IsJumpGraceActive => jumpGraceTimer > 0f;
	
	private float defaultColliderHeight;
	public float DefaultColliderHeight => defaultColliderHeight;
	private Vector3 defaultColliderCenter;
	public Vector3 DefaultColliderCenter => defaultColliderCenter;
	

	
	//* State Machine & states instances (FSM)
	public PlayerStateMachine StateMachine { get; private set; }
	public PlayerGroundedState GroundedState { get; private set; }
	public PlayerAirState AirState { get; private set; }
	public PlayerLedgeHangingState LedgeHangingState { get; private set; }
	public PlayerLedgeClimbingState LedgeClimbingState { get; private set; }
	public PlayerRespawnState RespawnState { get; private set; }
	public PlayerSlideState SlideState { get; private set; }
	public PlayerWallJumpState WallJumpState { get; private set; }
	public PlayerGridHangingState GridHangingState { get; private set; }
	public PlayerWallRunState WallRunState { get; private set; }
	public PlayerPurpleHookState PurpleHookState { get; private set; }
	public PlayerYellowHookState YellowHookState { get; private set; }
	
	
	
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
		LedgeGrabSystem = GetComponent<LedgeGrabPlayerSystem>();
		SafePositionTrackerSystem = GetComponent<SafePositionTrackerPlayerSystem>();
		SlideSystem = GetComponent<SlidePlayerSystem>();
		WallInteractionSystem = GetComponent<WallInteractionPlayerSystem>();
		GridInteractionSystem = GetComponent<GridInteractionPlayerSystem>();
		Animator = GetComponent<Animator>();
		WallRunSystem = GetComponent<WallRunPlayerSystem>();
		WhipSystem = GetComponent<WhipPlayerSystem>();
		
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
		WallJumpState = new PlayerWallJumpState(this, StateMachine);
		GridHangingState = new PlayerGridHangingState(this, StateMachine);
		WallRunState = new PlayerWallRunState(this, StateMachine);
		PurpleHookState = new PlayerPurpleHookState(this, StateMachine);
		YellowHookState = new PlayerYellowHookState(this, StateMachine);

		defaultColliderHeight = CapsuleCollider.height;
		defaultColliderCenter = CapsuleCollider.center;
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
		WhipSystem.TickWhipSystem();
		
		StateMachine.CurrentState.HandleInput();
		StateMachine.CurrentState.LogicUpdate();
	}


	private void RunTimersUpdate()
	{
		if (WallInteractionSystem.WallGrabCooldownTimer > 0f) WallInteractionSystem.WallGrabCooldownTimer -= Time.deltaTime;
		
		if (WallInteractionSystem.WallJumpInputLockTimer > 0f) WallInteractionSystem.WallJumpInputLockTimer -= Time.deltaTime;
		
		if (jumpGraceTimer > 0f) jumpGraceTimer -= Time.deltaTime;
		
		if (gridGrabLockTimer > 0f) gridGrabLockTimer -= Time.deltaTime;
		
		if (coyoteTimer > 0f)
		{
			coyoteTimer -= Time.deltaTime;
			if (coyoteTimer <= 0f)
			{
				pendingCoyoteJumpAction = null;
			}
		}
		
		if (GridInteractionSystem.GridDropThroughTimer > 0f)
		{
			GridInteractionSystem.GridDropThroughTimer -= Time.deltaTime;
			if (GridInteractionSystem.GridDropThroughTimer <= 0f)
			{
				GridInteractionSystem.IgnoredGridPlatform = null;
			}
		}
	}
	
	
	
	private void FixedUpdate()
	{
		GridInteractionSystem.UpdateGridPlatformCollisions();
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
		Animator.SetTrigger(animJump);
		ClearCoyoteTime();
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
	
	
	
	public void StartCoyoteTime(System.Action jumpCallback, float customDuration = -1f)
	{
		pendingCoyoteJumpAction = jumpCallback;
		coyoteTimer = customDuration > 0f ? customDuration : coyoteDuration;
	}


	public void ClearCoyoteTime()
	{
		coyoteTimer = 0f;
	}



	public void ConsomeCoyoteJump()
	{
		System.Action actionToRun = pendingCoyoteJumpAction;
		ClearCoyoteTime();
		actionToRun?.Invoke();
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
		Vector3 hangPosition = LedgeGrabSystem.CurrentLedgeHit.point - (transform.forward * LedgeGrabSystem.WallDistanceOffset);
		hangPosition.y -= (LedgeGrabSystem.LedgeCheckOrigin.position.y - transform.position.y);
		transform.position = hangPosition;
		transform.rotation = Quaternion.LookRotation(-LedgeGrabSystem.CurrentWallHit.normal);
	}
	
	
	// ---- Utils ----


	public void LockGridGrab(float duration)
	{
		gridGrabLockTimer = Mathf.Max(gridGrabLockTimer, duration);
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
		if (SlideSystem == null) SlideSystem = GetComponent<SlidePlayerSystem>();
		
		if (CapsuleCollider != null && SlideSystem != null)
		{
			float checkRadius = CapsuleCollider.radius * 0.85f;
			float minAllowedHeight = CapsuleCollider.radius * 2f;
			float effectiveSlideHeight = Mathf.Max(SlideSystem.SlidingHeight, minAllowedHeight);

			Vector3 bottomSphere = transform.position + Vector3.up * effectiveSlideHeight;
			Vector3 topSphere = transform.position + Vector3.up * (defaultColliderHeight - checkRadius);
			if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

			Gizmos.color = Application.isPlaying && !SlideSystem.CanStandUp() ? Color.red : Color.green;
			Gizmos.DrawWireSphere(bottomSphere, checkRadius);
			Gizmos.DrawWireSphere(topSphere, checkRadius);
			Gizmos.DrawLine(bottomSphere, topSphere);
		}

		// Wall check for cling
		if (WallInteractionSystem == null) WallInteractionSystem = GetComponent<WallInteractionPlayerSystem>();
		
		if (WallInteractionSystem != null)
		{
			Vector3 direction = Vector3.right * FacingDirection;
			Vector3 upperOrigin = transform.position + Vector3.up * WallInteractionSystem.WallCheckUpperHeight;
			Vector3 lowerOrigin = transform.position + Vector3.up * WallInteractionSystem.WallCheckLowerHeight;

			Gizmos.color = Application.isPlaying && WallInteractionSystem.IsTouchingWall() ? Color.cyan : Color.yellow;
			Gizmos.DrawLine(upperOrigin, upperOrigin + direction * WallInteractionSystem.WallCheckDistance);
			Gizmos.DrawLine(lowerOrigin, lowerOrigin + direction * WallInteractionSystem.WallCheckDistance);
		}
		
		
		// Grid platform grab check
		if (GridInteractionSystem == null) GridInteractionSystem = GetComponent<GridInteractionPlayerSystem>();
		
		if (CapsuleCollider != null && GridInteractionSystem != null)
		{
			Vector3 grabCenter = transform.position + Vector3.up * GridInteractionSystem.GridHangHandHeight;
			Vector3 grabSize = new(CapsuleCollider.radius * 1.6f, 0.4f, 1f);

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireCube(grabCenter, grabSize);
		}
	}
}
