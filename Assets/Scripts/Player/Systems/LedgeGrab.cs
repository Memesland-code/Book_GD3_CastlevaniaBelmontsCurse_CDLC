using UnityEngine;

public class LedgeGrab : MonoBehaviour
{
    [Header("Detection Setup")]
    [SerializeField] private Transform wallCheckOrigin; // Chest / eye level
    [SerializeField] private Transform ledgeCheckOrigin; // 30 cm above head
    [SerializeField] private float wallCheckDistance = 0.7f;
    [SerializeField] private float ledgeCheckDistance = 1.5f; 
    
    [Header("Geometry Filters")]
    [SerializeField] private LayerMask ignoreLayers; // Raycast will ignore player or other unwanted objects
    [Range(0f, 0.5f)] [SerializeField] private float maxWallAngleY = 0.3f; // Max allowed wall angle to consider as ledge
    [Range(0.5f, 1f)] [SerializeField] private float minLedgeFlatness = 0.7f; // Minimum flatness for top surface
    
    [Header("Positionning & Timing Data")]
    [SerializeField] private float wallDistanceOffset = 0.4f; // keeps capsule off-wall
    [SerializeField] private Vector3 climbOffset = new Vector3(0f, 1.8f, 0.6f);
    [SerializeField] private float climbDuration = 0.3f;
    
    [Header("Drop Settings")]
    [SerializeField] private float ledgeReleaseCooldown = 0.5f; // Prevents instant re-grab while dropping
    [SerializeField] private float hangTimerCooldown = 0.2f; // Avoids characters from going off of ledge right immediately after grabbing
    
    private PlayerController playerController;
    private float releaseTimer; // Avoids character from grabbing back ledge immediately after going off
    
    
    // Public properties
    public Transform LedgeCheckOrigin => ledgeCheckOrigin;
    public Vector3 ClimbOffset => climbOffset;
    public float WallDistanceOffset => wallDistanceOffset;
    public float ClimbDuration => climbDuration;
    public float LedgeReleaseCooldown => ledgeReleaseCooldown;
    public float HangTimer => hangTimerCooldown;
    
    
    // Cached hit data for last valid raycasts for state positionning
    public RaycastHit CurrentWallHit { get; private set; }
    public RaycastHit CurrentLedgeHit { get; private set; }

    
    
    private void Awake()
    {
	    playerController = GetComponent<PlayerController>();
    }


    private void Update()
    {
	    if (releaseTimer > 0)
	    {
		    releaseTimer -= Time.deltaTime;
	    }
    }


    /// <summary>
    /// Sensor Method called frame by frame by PlayerAirState to detect valid ledges
    /// </summary>
    public bool CheckForLedge()
    {
	    return CheckForLedge(out _, out _);
    }


	
    /// <summary>
    /// Overload returning out RaycastHits if detailed hit data is needed
    /// </summary>
    /// <param name="wallHit"></param>
    /// <param name="ledgeHit"></param>
    /// <returns></returns>
    public bool CheckForLedge(out RaycastHit wallHit, out RaycastHit ledgeHit)
    {
	    wallHit = default;
	    ledgeHit = default;
	    
	    // Skip check if on release cooldown or grounded
	    if (releaseTimer > 0f) return false;
	    if (playerController != null && playerController.IsGrounded) return false;

	    int layerMask = ~ignoreLayers;
	    
	    // Forward raycast for wall detection
	    bool hitWall = Physics.Raycast(
		    wallCheckOrigin.position,
		    transform.forward,
		    out wallHit,
		    wallCheckDistance,
		    layerMask,
		    QueryTriggerInteraction.Ignore
	    );
	    
	    if (!hitWall) return false;
	    if (Mathf.Abs(wallHit.normal.y) > maxWallAngleY) return false; // Checking wall angle
	    
	    // Downward raycast for ledge top surface detection
	    Vector3 ledgeRayStart = ledgeCheckOrigin.position + (transform.forward * (wallHit.distance + 0.1f));
	    bool hitLedge = Physics.Raycast(
		    ledgeRayStart,
		    Vector3.down,
		    out ledgeHit,
		    ledgeCheckDistance,
		    layerMask,
		    QueryTriggerInteraction.Ignore
	    );
	    
	    if (!hitLedge) return false;
	    if (ledgeHit.normal.y < minLedgeFlatness) return false; // Check top surface angle

	    CurrentWallHit = wallHit;
	    CurrentLedgeHit = ledgeHit;

	    return true;
    }



    /// <summary>
    /// Called when leaving hanging or climbing states to prevent instant re-grab
    /// </summary>
    public void TriggerReleaseCooldown()
    {
	    releaseTimer = LedgeReleaseCooldown;
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
