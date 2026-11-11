using System.Collections;
using UnityEngine;

public class LedgeGrab : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private Transform wallCheckOrigin; // Chest / eye level
    [SerializeField] private Transform ledgeCheckOrigin; // 30 cm above head
    [SerializeField] private float wallCheckDistance = 0.7f;
    [SerializeField] private float ledgeCheckDistance = 1.5f; 
    
    [Header("Geometry Filters")]
    [SerializeField] private LayerMask ignoreLayers; // Raycast will ignore player or other unwanted objects
    [Range(0f, 0.5f)] [SerializeField] private float maxWallAngleY = 0.3f; // Max allowed wall angle to consider as ledge
    [Range(0.5f, 1f)] [SerializeField] private float minLedgeFlatness = 0.7f; // Minimum flatness for top surface
    
    [Header("Climb Offset & Timing")]
    [SerializeField] private Vector3 climbOffset = new Vector3(0f, 1.8f, 0.6f);
    [SerializeField] private float climbDuration = 0.35f;
    
    [Header("Drop Settings")]
    [SerializeField] private float ledgeReleaseCooldown = 0.5f; // Prevents instant re-grab while dropping
    [SerializeField] private float hangTimerCooldown = 0.2f;
    
    private PlayerController playerController;
    private InputManager inputManager;
    private Rigidbody rb;
    
    private float releaseTimer; // Avoids character from grabbing back ledge immediately after going off
    private float hangTimer; // Avoids characters from going off of ledge right immediately after grabbing
    private RigidbodyConstraints originalConstraints;

    public bool IsHanging { get; private set; }
    public bool IsClimbing { get; private set; }
    public bool JumpRequestedFromLedge { get; private set; }

    
    
    private void Awake()
    {
	    playerController = GetComponent<PlayerController>();
	    rb = GetComponent<Rigidbody>();
	    inputManager = GetComponent<InputManager>();

	    if (rb != null) originalConstraints = rb.constraints;
    }


    private void Update()
    {
	    if (releaseTimer > 0)
	    {
		    releaseTimer -= Time.deltaTime;
	    }
	    
	    if (IsClimbing) return;

	    if (IsHanging)
	    {
		    if (hangTimer > 0f)
		    {
			    hangTimer -= Time.deltaTime;
		    }
		    else
		    {
				HandleHangingMovementInput();
		    }
	    }
	    else
	    {
		    CheckForLedge();
	    }
    }


    private void CheckForLedge()
    {
	    // Skip check if on cooldown or grounded
	    if (releaseTimer > 0f) return;
	    if (playerController != null && playerController.IsGrounded) return;

	    int layerMask = ~ignoreLayers;
	    
	    // Forward raycast for walls detection
	    bool hitWall = Physics.Raycast(
		    wallCheckOrigin.position,
		    transform.forward,
		    out RaycastHit wallHit,
		    wallCheckDistance,
		    layerMask,
		    QueryTriggerInteraction.Ignore
	    );

	    if (!hitWall) return;

	    if (Mathf.Abs(wallHit.normal.y) > maxWallAngleY) return; // Checking wall angle
	    
	    // Downward raycast for ledge top surface detection
	    Vector3 ledgeRayStart = ledgeCheckOrigin.position + (transform.forward * (wallHit.distance + 0.1f)); // Aims right after ledge
	    bool hitLedge = Physics.Raycast(
		    ledgeRayStart,
		    Vector3.down,
		    out RaycastHit ledgeHit,
		    ledgeCheckDistance,
		    layerMask,
		    QueryTriggerInteraction.Ignore
	    );

	    if (!hitLedge) return;

	    if (ledgeHit.normal.y < minLedgeFlatness) return; // Check top surface angle

	    StartHanging(wallHit, ledgeHit);
    }



    private void StartHanging(RaycastHit wallHit, RaycastHit ledgeHit)
    {
	    IsHanging = true;
	    JumpRequestedFromLedge = false;
	    hangTimer = 0.2f;

	    rb.linearVelocity = Vector3.zero;
	    rb.isKinematic = true;
	    rb.constraints = RigidbodyConstraints.FreezeAll;
	    
	    // Position player flush to the ledge
	    Vector3 hangPosition = ledgeHit.point - (transform.forward * 0.4f);
	    hangPosition.y -= (ledgeCheckOrigin.position.y - transform.position.y);

	    transform.position = hangPosition;
	    transform.rotation = Quaternion.LookRotation(-wallHit.normal); // Make player watch towards wall
    }



    private void HandleHangingMovementInput()
    {
	    if (inputManager == null) return;

	    float verticalInput = inputManager.Move.y;
	    float horizontalInput = inputManager.Move.x;

	    float forwardDir = transform.forward.x; // Character forward facing

	    bool wantsToClimb = verticalInput > 0.1f || (horizontalInput * forwardDir > 0.1f); // Climb when up or direction-facing input pressed

	    bool wantsToDrop = verticalInput < -0.1f || (horizontalInput * forwardDir < -0.1f); // Drop when down or non-direction-facing input pressed
	    
	    if (wantsToClimb)
	    {
		    StartCoroutine(ClimbLedgeRoutine());
		    return;
	    }
	    
	    if (wantsToDrop)
	    {
		    ReleaseLedge();
	    }
    }



    public void OnJumpInput()
    {
	    if (!IsHanging || IsClimbing) return;

	    JumpRequestedFromLedge = true;
	    ReleaseLedge();
    }



    public void ReleaseLedge()
    {
	    IsHanging = false;
	    IsClimbing = false;
	    hangTimer = hangTimerCooldown;
	    releaseTimer = ledgeReleaseCooldown;
	    
	    rb.isKinematic = false;
	    rb.constraints = originalConstraints;
    }



    public void ClearJumpRequest()
    {
	    JumpRequestedFromLedge = false;
    }



    private IEnumerator ClimbLedgeRoutine()
    {
	    IsClimbing = true;
	    IsHanging = false;

	    Vector3 startPosition = transform.position;
	    Vector3 endPosition = transform.position + (transform.forward * climbOffset.z) + (Vector3.up * climbOffset.y);

	    float elapsedTime = 0f;
	    while (elapsedTime < climbDuration)
	    {
		    transform.position = Vector3.Lerp(startPosition, endPosition, elapsedTime / climbDuration);
		    elapsedTime += Time.deltaTime;
		    yield return null;
	    }

	    transform.position = endPosition;
	    
	    ReleaseLedge();
    }


    private void OnDrawGizmosSelected()
    {
	    Gizmos.color = Color.red;
	    Gizmos.DrawRay(wallCheckOrigin.position, transform.forward * wallCheckDistance);

	    Gizmos.color = Color.green;
	    Vector3 ledgeRayStart = ledgeCheckOrigin.position + (transform.forward * wallCheckDistance);
	    Gizmos.DrawRay(ledgeRayStart, Vector3.down * ledgeCheckDistance);
    }
}
