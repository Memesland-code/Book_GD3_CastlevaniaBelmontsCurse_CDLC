using System;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
	private Rigidbody rb;
	private InputManager inputs;
	private CinemachineCamera camera;
	
	[Space(10), Header("Movement")]
	private bool isFacingRight = true;
	[SerializeField] private float groundSpeed;
	[SerializeField] private float airSpeed;
	private const float FastFallingFactor = 1.05f;
	
	[Space(10), Header("Jump")]
	[SerializeField] private float upForce;
	[SerializeField] private float gravityScale;
	private int currentJumpsCombo;
	[SerializeField] private int maxJumpsNumber;
	private GroundDetector groundDetector;
	
	[Space(10), Header("AirMaterials")]
	[SerializeField] private PhysicsMaterial groundMaterial;
	[SerializeField] private PhysicsMaterial airMaterial;
	
	private CapsuleCollider capsuleCollider;
	
	
	// Cheat codes
	public bool noClip;


	private void Awake()
	{
		capsuleCollider = GetComponent<CapsuleCollider>();
		inputs = GetComponent<InputManager>();
		rb = GetComponent<Rigidbody>();
		groundDetector = GetComponentInChildren<GroundDetector>();
		camera = GameObject.FindWithTag("MainCamera").GetComponent<CinemachineCamera>();
	}

	private void Start()
	{
		camera.Target.TrackingTarget = transform;
	}

	private void Update()
	{
		if (transform.rotation.y == 0) transform.rotation = Quaternion.Euler(0, 90, 0);
		    
		// Check player facing direction and change on movement direction 
		if (inputs.move.x < 0 && isFacingRight)
		{
			isFacingRight = false;
			transform.rotation = Quaternion.Euler(0, -90, 0);
		}
		else if (inputs.move.x > 0 && !isFacingRight)
		{
			isFacingRight = true;
			transform.rotation = Quaternion.Euler(0, 90, 0);
		}
		
		if (!groundDetector.touched)
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
		
		// Process values
		float realGravity = rb.linearVelocity.y >= 0 ? rb.linearVelocity.y : rb.linearVelocity.y * FastFallingFactor;
		realGravity = Mathf.Max(realGravity, -15);
		    
		float realSpeed = groundDetector.touched ? groundSpeed : airSpeed;
		    
		Vector3 horizontalMove = new(inputs.move.x * realSpeed, 0, 0);
		
		// Physics update and noclip
		if (noClip)
		{
			transform.position += new Vector3(inputs.move.x / 2, inputs.move.y / 2, 0);
		}
		else
		{
			rb.linearVelocity = new Vector3(0, realGravity, 0) + horizontalMove;
		}
	}
	
	private void FixedUpdate()
	{
		rb.AddForce(Physics.gravity * ((gravityScale - 1) * rb.mass));
	}

	public void Jump()
    {
	    float jumpForce = Mathf.Sqrt(this.upForce * -3 * (Physics.gravity.y * gravityScale));
	    
	    if (groundDetector.touched || currentJumpsCombo < maxJumpsNumber)
	    {
		    rb.linearVelocity = Vector3.zero;
		    rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
		    currentJumpsCombo++;
	    }
    }
}
