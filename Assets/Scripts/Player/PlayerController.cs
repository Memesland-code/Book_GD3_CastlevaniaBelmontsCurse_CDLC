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
public class PlayerController : MonoBehaviour
{
	public readonly int isRunning = Animator.StringToHash("IsRunning");
	public readonly int animJump = Animator.StringToHash("Jump");

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
	
	
	
	//* Internal Components
	private CinemachineCamera camera;
	private GroundDetectorPlayerSystem groundDetector;
	public CapsuleCollider CapsuleCollider { get; private set; }
	private RigidbodyConstraints originalConstraints;
	public Animator Animator { get; private set; }
	
	
	
	//* Variables Getters for PlayerState
	public InputManager Input { get; private set; }
	public Rigidbody Rigidbody { get; private set; }
	public LedgeGrabPlayerSystem LedgeGrab { get; private set; }
	public SafePositionTrackerPlayerSystem SafePositionTracker { get; private set; }
	public SlidePlayerSystem Slide { get; private set; }
	public WallInteractionPlayerSystem WallInteraction { get; private set; }
	public GridInteractionPlayerSystem GridInteraction { get; private set; }

	// Jump Settings
	public float GroundSpeed => groundSpeed;
	public float AirSpeed => airSpeed;
	private int currentJumpsCombo;
	public bool CanJump => currentJumpsCombo < maxAirJumpsNumber;
	private float airCoyoteTimer;
	public bool CanWallCoyoteJump => airCoyoteTimer > 0f;

	// Gravity Related
	public float GravityScale => gravityScale;
	public float FallGravityScale => fallGravityScale;
	public float LowJumpGravityScale => lowJumpGravityScale;
	

	//* General
	public bool IsGrounded => groundDetector != null && groundDetector.touched;
	
	public bool CanGrabWall => WallInteraction.WallGrabCooldownTimer <= 0f;
	
	public bool IsWallJumpInputLocked => WallInteraction.WallJumpInputLockTimer > 0f;
	
	public bool IsFacingRight => isFacingRight;

	public float AirCoyoteTimer { get; set; }
	public float AirCoyoteDuration => airCoyoteDuration;
	
	private float jumpGraceTimer;
	public bool IsJumpGraceActive => jumpGraceTimer > 0f;
	
	private bool isFacingRight = true;
	public int FacingDirection => IsFacingRight ? 1 : -1;
	
	private float standingHeight;
	public float StandingHeight => standingHeight;
	

	
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
		WallInteraction = GetComponent<WallInteractionPlayerSystem>();
		GridInteraction = GetComponent<GridInteractionPlayerSystem>();
		Animator = GetComponent<Animator>();
		
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
		if (WallInteraction.WallGrabCooldownTimer > 0f) WallInteraction.WallGrabCooldownTimer -= Time.deltaTime;
		if (WallInteraction.WallJumpInputLockTimer > 0f) WallInteraction.WallJumpInputLockTimer -= Time.deltaTime;
		if (airCoyoteTimer > 0f) airCoyoteTimer -= Time.deltaTime;
		if (jumpGraceTimer > 0f) jumpGraceTimer -= Time.deltaTime;
		
		if (GridInteraction.GridDropThroughTimer > 0f)
		{
			GridInteraction.GridDropThroughTimer -= Time.deltaTime;

			if (GridInteraction.GridDropThroughTimer <= 0f)
			{
				GridInteraction.IgnoredGridPlatform = null;
			}
		}
	}
	
	
	
	private void FixedUpdate()
	{
		GridInteraction.UpdateGridPlatformCollisions();
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
		ClearAirCoyoteTime();
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
	
	
	
	public void StartAirCoyoteTime(int? wallDir = 0)
	{
		WallInteraction.LastWallDirection = wallDir ?? WallInteraction.LastWallDirection;
		airCoyoteTimer = airCoyoteDuration;
	}


	public void ClearAirCoyoteTime()
	{
		airCoyoteTimer = 0f;
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
		if (WallInteraction == null) WallInteraction = GetComponent<WallInteractionPlayerSystem>();
		
		if (WallInteraction != null)
		{
			Vector3 direction = Vector3.right * FacingDirection;
			Vector3 upperOrigin = transform.position + Vector3.up * WallInteraction.WallCheckUpperHeight;
			Vector3 lowerOrigin = transform.position + Vector3.up * WallInteraction.WallCheckLowerHeight;

			Gizmos.color = Application.isPlaying && WallInteraction.IsTouchingWall() ? Color.cyan : Color.yellow;
			Gizmos.DrawLine(upperOrigin, upperOrigin + direction * WallInteraction.WallCheckDistance);
			Gizmos.DrawLine(lowerOrigin, lowerOrigin + direction * WallInteraction.WallCheckDistance);
		}
		
		
		// Grid platform grab check
		if (GridInteraction == null) GridInteraction = GetComponent<GridInteractionPlayerSystem>();
		
		if (CapsuleCollider != null && GridInteraction != null)
		{
			Vector3 grabCenter = transform.position + Vector3.up * GridInteraction.GridHangHandHeight;
			Vector3 grabSize = new(CapsuleCollider.radius * 1.6f, 0.4f, 1f);

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireCube(grabCenter, grabSize);
		}
	}
}
