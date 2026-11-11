using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(LedgeGrab))]
public class PlayerController : MonoBehaviour
{
	private Rigidbody rb;
	private InputManager inputs;
	private CinemachineCamera camera;
	private LedgeGrab ledgeGrab;
	private GroundDetector groundDetector;
	private CapsuleCollider capsuleCollider;
	
	public bool IsGrounded => groundDetector != null && groundDetector.touched;
	
	[Space(10), Header("Movement")]
	private bool isFacingRight = true;
	[SerializeField] private float groundSpeed = 7.5f;
	[SerializeField] private float airSpeed = 8.5f;
	private const float FastFallingFactor = 1.0f;
	
	[Space(10), Header("Jump")]
	[SerializeField] private float upForce = 2.5f;
	[SerializeField] private float gravityScale = 4.5f;
	private int currentJumpsCombo;
	[SerializeField] private int maxJumpsNumber = 0;
	
	[Space(10), Header("AirMaterials")]
	[SerializeField] private PhysicsMaterial groundMaterial;
	[SerializeField] private PhysicsMaterial airMaterial;
	
	// Cheat codes
	public bool noClip;

	

	private void Awake()
	{
		capsuleCollider = GetComponent<CapsuleCollider>();
		inputs = GetComponent<InputManager>();
		rb = GetComponent<Rigidbody>();
		ledgeGrab = GetComponent<LedgeGrab>();
		groundDetector = GetComponentInChildren<GroundDetector>();

		GameObject camObj = GameObject.FindWithTag("MainCamera");
		if (camObj != null) camera = camObj.GetComponent<CinemachineCamera>();
	}

	
	
	private void Start()
	{
		if (camera != null)
		{
			camera.Target.TrackingTarget = transform;
		}
	}

	
	
	private void Update()
	{
		// Detach character from ledge and perform a jump
		if (ledgeGrab.JumpRequestedFromLedge)
		{
			ledgeGrab.ClearJumpRequest();
			ExecuteJump();
		}
		
		// Handle ledge grab & climb states
		if (ledgeGrab.IsHanging || ledgeGrab.IsClimbing) return;
		
		
		if (transform.rotation.eulerAngles.y == 0) transform.rotation = Quaternion.Euler(0, 90, 0);
		    
		// Check player facing direction and change on movement direction 
		if (inputs.Move.x < 0 && isFacingRight)
		{
			isFacingRight = false;
			transform.rotation = Quaternion.Euler(0, -90, 0);
		}
		else if (inputs.Move.x > 0 && !isFacingRight)
		{
			isFacingRight = true;
			transform.rotation = Quaternion.Euler(0, 90, 0);
		}
		
		
		// Ground check & materials
		if (!IsGrounded)
		{
			if (rb.linearVelocity.magnitude > 0.0f)
			{
				capsuleCollider.sharedMaterial = airMaterial;
			}
		}
		else
		{
			capsuleCollider.sharedMaterial = groundMaterial;
			currentJumpsCombo = 0;
		}
		
		
		// Velocity & Gravity processing
		float realGravity = rb.linearVelocity.y >= 0 ? rb.linearVelocity.y : rb.linearVelocity.y * FastFallingFactor;
		realGravity = Mathf.Max(realGravity, -15);
		    
		float realSpeed = IsGrounded ? groundSpeed : airSpeed;
		Vector3 horizontalMove = new(inputs.Move.x * realSpeed, 0, 0);
		
		if (noClip)
		{
			transform.position += new Vector3(inputs.Move.x / 2, inputs.Move.y / 2, 0);
		}
		else
		{
			rb.linearVelocity = new Vector3(0, realGravity, 0) + horizontalMove;
		}
	}
	
	
	
	private void FixedUpdate()
	{
		if (rb.isKinematic || noClip) return;
		
		rb.AddForce(Physics.gravity * ((gravityScale - 1f) * rb.mass));
	}

	
	
	public void Jump()
	{
		if (ledgeGrab != null && (ledgeGrab.IsHanging || ledgeGrab.IsClimbing)) return;

		if (IsGrounded || currentJumpsCombo < maxJumpsNumber)
		{
			ExecuteJump();
		}
    }

	private void ExecuteJump()
	{
		float jumpForce = Mathf.Sqrt(upForce * -3 * (Physics.gravity.y * gravityScale));
		
		rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, 0f);
		rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
		currentJumpsCombo++;
	}
}
