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
	[SerializeField] private float lowJumpGravityScale = 4.5f;
	[SerializeField] private float gravityScale = 5f;
	[SerializeField] private float fastFallingFactor = 1.0f;
	[SerializeField] private int maxAirJumpsNumber;
	
	[Space(10), Header("Physic Materials")]
	[SerializeField] private PhysicsMaterial groundMaterial;
	[SerializeField] private PhysicsMaterial airMaterial;
	
	
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
	public float LowJumpGravityScale => lowJumpGravityScale;
	public float GravityScale => gravityScale;
	
	// Other
	public bool IsGrounded => groundDetector != null && groundDetector.touched;
	public RigidbodyConstraints OriginalConstraints => originalConstraints;


	//* State Machine & states instances
	public PlayerStateMachine StateMachine { get; private set; }
	public PlayerGroundedState GroundedState { get; private set; }
	public PlayerAirState AirState { get; private set; }
	public PlayerLedgeHangingState LedgeHangingState { get; private set; }
	public PlayerLedgeClimbingState LedgeClimbingState { get; private set; }
	public PlayerRespawnState RespawnState { get; private set; }
	
	
	//* General
	private int currentJumpsCombo;
	private bool isFacingRight = true;
	
	
	
	[Space(20), Header("Cheat Codes")]
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
		RespawnState = new PlayerRespawnState(this, StateMachine, 0.25f);
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
		StateMachine.CurrentState.HandleInput();
		StateMachine.CurrentState.LogicUpdate();
	}
	
	
	
	private void FixedUpdate()
	{
		StateMachine.CurrentState.PhysicsUpdate();
	}
	
	
	
	// ---- State machine functions ----
	
	
	
	public void ApplyHorizontalMovement(float speed)
	{
		if (Input == null || Rigidbody == null) return;

		// Fast falling & gravity clamp logic
		float realGravity = Rigidbody.linearVelocity.y >= 0 
			? Rigidbody.linearVelocity.y 
			: Rigidbody.linearVelocity.y * fastFallingFactor;
        
		realGravity = Mathf.Max(realGravity, -15f);

		Vector3 horizontalMove = new Vector3(Input.Move.x * speed, 0f, 0f);

		if (noClip)
		{
			transform.position += new Vector3(Input.Move.x / 2f, Input.Move.y / 2f, 0f);
		}
		else
		{
			Rigidbody.linearVelocity = new Vector3(0f, realGravity, 0f) + horizontalMove;
		}
	}


	
	public void UpdateFacingDirection()
	{
		if (Input == null) return;

		// Lock baseline Y rotation to 90 degrees if reset
		if (transform.rotation.eulerAngles.y == 0f)
		{
			transform.rotation = Quaternion.Euler(0f, 90f, 0f);
		}

		if (Input.Move.x < 0f && isFacingRight)
		{
			isFacingRight = false;
			transform.rotation = Quaternion.Euler(0f, -90f, 0f);
		}
		else if (Input.Move.x > 0f && !isFacingRight)
		{
			isFacingRight = true;
			transform.rotation = Quaternion.Euler(0f, 90f, 0f);
		}
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

	
	
	public void SetPhysicsMaterial(bool isGrounded)
	{
		capsuleCollider.sharedMaterial = isGrounded ? groundMaterial : airMaterial;
	}

	
	
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



	public void TriggerTrapRespawn()
	{
		if (StateMachine.CurrentState == RespawnState) return;
		
		StateMachine.ChangeState(RespawnState);
	}
}
