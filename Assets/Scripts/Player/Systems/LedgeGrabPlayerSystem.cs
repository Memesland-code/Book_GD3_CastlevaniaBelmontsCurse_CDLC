using UnityEngine;

public class LedgeGrabPlayerSystem : MonoBehaviour
{
    [Header("Detection Setup")]
    [SerializeField] private Transform wallCheckOrigin; // Chest / eye level
    [SerializeField] private Transform ledgeCheckOrigin; // 30 cm above head
    [SerializeField] private float wallCheckDistance = 0.7f;
    [SerializeField] private float ledgeCheckDistance = 1.5f; 
    
    [Header("Geometry Filters")]
    [SerializeField] private LayerMask ignoreLayers; // Raycast will ignore player or other unwanted objects
    [Tooltip("Layers that block ledges above, but cannot be grabbed themselves (e.g. WallRun)")]
    [SerializeField] private LayerMask nonGrabbableLayers;
    [Range(0f, 0.5f)] [SerializeField] private float maxWallAngleY = 0.3f; // Max allowed wall angle to consider as ledge
    [Range(0.5f, 1f)] [SerializeField] private float minLedgeFlatness = 0.7f; // Minimum flatness for top surface
    [SerializeField] private float minLedgeDepth = 0.55f;
    
    [Header("Positionning & Timing Data")]
    [SerializeField] private float wallDistanceOffset = 0.4f; // keeps capsule off-wall
    [SerializeField] private Vector3 climbOffset = new Vector3(0f, 1.8f, 0.6f);
    [SerializeField] private float climbDuration = 0.3f;
    
    [Header("Drop Settings")]
    [SerializeField] private float ledgeReleaseCooldown = 0.5f; // Prevents instant re-grab while dropping
    [SerializeField] private float hangTimerCooldown = 0.2f; // Avoids characters from going off of ledge right immediately after grabbing
    
    private PlayerController player;
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
    
    
    private RaycastHit debugTopHit;
#if UNITY_EDITOR
    private bool debugHasWallHit;
    private RaycastHit debugWallHit;
    private bool debugTopBlocked;
    private bool debugHasLedgeHit;
    private RaycastHit debugLedgeHit;
    private Vector3 debugLedgeRayStart;
    private Vector3 debugCapsuleBottom;
    private Vector3 debugCapsuleTop;
    private float debugCapsuleRadius;
    private bool debugCapsuleBlocked;
    private Collider debugBlockingCollider;
    private string debugStatusText = "Idle";
#endif

    
    
    private void Awake()
    {
	    player = GetComponent<PlayerController>();
    }


    private void Update()
    {
	    if (releaseTimer > 0)
	    {
		    releaseTimer -= Time.deltaTime;
	    }
    }


	
    /// <summary>
    /// Overload returning out RaycastHits if detailed hit data is needed
    /// </summary>
    public bool CheckForLedge(out RaycastHit wallHit, out RaycastHit ledgeHit)
    {
	    wallHit = default;
	    ledgeHit = default;
	    
#if UNITY_EDITOR
	    debugHasWallHit = false;
	    debugTopBlocked = false;
	    debugHasLedgeHit = false;
	    debugCapsuleBlocked = false;
	    debugBlockingCollider = null;
#endif
	    
	    // Skip check if on release cooldown or grounded
	    if (releaseTimer > 0f)
	    {
#if UNITY_EDITOR
		    debugStatusText = $"Cooldown ({releaseTimer:F2}s)";
#endif
		    return false;
	    }
	    
	    if (player != null && player.IsGrounded)
	    {
#if UNITY_EDITOR
		    debugStatusText = "Grounded (skip)";
#endif
		    return false;
	    }

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
	    
#if UNITY_EDITOR
	    debugHasWallHit = hitWall;
	    debugWallHit = wallHit;
#endif
	    
	    if (!hitWall)
	    {
#if UNITY_EDITOR
		    debugStatusText = "No wall hit";
#endif
		    return false;
	    }
	    
	    if (((1 << wallHit.collider.gameObject.layer) & nonGrabbableLayers) != 0)
	    {
#if UNITY_EDITOR
		    debugStatusText = $"Non-grabbable wall ({wallHit.collider.name})";
#endif
		    return false;
	    }
	    
	    
	    if (Mathf.Abs(wallHit.normal.y) > maxWallAngleY)
	    {
#if UNITY_EDITOR
		    debugStatusText = $"Wall too steep (normal.y={wallHit.normal.y:F2})";
#endif
		    return false; // Checking wall angle
	    }

	    float topCheckDistance = wallHit.distance + minLedgeDepth;
	    bool topBlocked = Physics.CheckSphere(ledgeCheckOrigin.position, 0.05f, layerMask, QueryTriggerInteraction.Ignore) ||
	                      Physics.Raycast(ledgeCheckOrigin.position, transform.forward, out debugTopHit, topCheckDistance, layerMask, QueryTriggerInteraction.Ignore);

#if UNITY_EDITOR
	    debugTopBlocked = topBlocked;
#endif

	    if (topBlocked)
	    {
#if UNITY_EDITOR
		    string blockerName = debugTopHit.collider != null ? debugTopHit.collider.name : "Origin overlap";
		    debugStatusText = $"Blocked above by {blockerName}";
#endif
		    return false;
	    }
	    
	    // Downward raycast for ledge top surface detection
	    Vector3 ledgeRayStart = ledgeCheckOrigin.position + (transform.forward * (wallHit.distance + 0.1f));
#if UNITY_EDITOR
	    debugLedgeRayStart = ledgeRayStart;
#endif
	    
	    bool hitLedge = Physics.Raycast(
		    ledgeRayStart,
		    Vector3.down,
		    out ledgeHit,
		    ledgeCheckDistance,
		    layerMask,
		    QueryTriggerInteraction.Ignore
	    );
	    
#if UNITY_EDITOR
	    debugHasLedgeHit = hitLedge;
	    debugLedgeHit = ledgeHit;
#endif
	    
	    if (!hitLedge)
	    {
#if UNITY_EDITOR
		    debugStatusText = "No top surface found";
#endif
		    return false;
	    }
	    
	    if (ledgeHit.normal.y < minLedgeFlatness)
	    {
#if UNITY_EDITOR
		    debugStatusText = $"Top too sloped (normal.y={ledgeHit.normal.y:F2})";
#endif
		    return false; // Check top surface angle
	    }

	    float radius = player != null ? player.CapsuleCollider.radius * 0.9f : 0.35f;
	    float height = player != null ? player.DefaultColliderHeight : 1.8f;
	    Vector3 landingPoint = ledgeHit.point + transform.forward * Mathf.Max(0f, minLedgeDepth - radius);
	    Vector3 clearanceBottom = ledgeHit.point + Vector3.up * (radius + 0.05f);
	    Vector3 clearanceTop = ledgeHit.point + Vector3.up * Mathf.Max(radius + 0.06f, height - radius);
	    
	    Collider[] overlaps = Physics.OverlapCapsule(clearanceBottom, clearanceTop, radius, layerMask, QueryTriggerInteraction.Ignore);
	    bool capsuleBlocked = overlaps.Length > 0;
	    
#if UNITY_EDITOR
	    debugCapsuleBottom = clearanceBottom;
	    debugCapsuleTop = clearanceTop;
	    debugCapsuleRadius = radius;
	    debugCapsuleBlocked = capsuleBlocked;
	    debugBlockingCollider = capsuleBlocked ? overlaps[0] : null;
#endif

	    if (capsuleBlocked)
	    {
#if UNITY_EDITOR
		    debugStatusText = $"Landing blocked by {overlaps[0].name}";
#endif
		    return false;
	    }

#if UNITY_EDITOR
	    debugStatusText = $"VALID LEDGE ({ledgeHit.collider.name})";
#endif

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
    


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        PlayerController pc = player != null ? player : GetComponent<PlayerController>();
        if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.LedgeGrab)) return;
        if (wallCheckOrigin == null || ledgeCheckOrigin == null) return;

        // 1. Wall Ray
        Gizmos.color = debugHasWallHit ? Color.green : Color.red;
        Gizmos.DrawRay(wallCheckOrigin.position, transform.forward * wallCheckDistance);
        if (debugHasWallHit)
        {
            Gizmos.DrawSphere(debugWallHit.point, 0.06f);
        }

        // 2. Top Clearance Ray
        float topDist = debugHasWallHit ? debugWallHit.distance + minLedgeDepth : wallCheckDistance + minLedgeDepth;
        Gizmos.color = debugTopBlocked ? Color.magenta : Color.cyan;
        Gizmos.DrawRay(ledgeCheckOrigin.position, transform.forward * topDist);
        if (debugTopBlocked && debugTopHit.collider != null)
        {
            Gizmos.DrawSphere(debugTopHit.point, 0.07f);
        }

        // 3. Ledge Down Ray
        Vector3 downStart = Application.isPlaying && debugHasWallHit
            ? debugLedgeRayStart
            : ledgeCheckOrigin.position + (transform.forward * wallCheckDistance);
        Gizmos.color = debugHasLedgeHit ? Color.green : new Color(1f, 0.5f, 0f);
        Gizmos.DrawRay(downStart, Vector3.down * ledgeCheckDistance);
        if (debugHasLedgeHit)
        {
            Gizmos.DrawSphere(debugLedgeHit.point, 0.07f);
        }

        // 4. Landing Clearance Capsule
        if (Application.isPlaying && debugHasLedgeHit)
        {
            Gizmos.color = debugCapsuleBlocked ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(debugCapsuleBottom, debugCapsuleRadius);
            Gizmos.DrawWireSphere(debugCapsuleTop, debugCapsuleRadius);
            Gizmos.DrawLine(debugCapsuleBottom + Vector3.forward * debugCapsuleRadius, debugCapsuleTop + Vector3.forward * debugCapsuleRadius);
            Gizmos.DrawLine(debugCapsuleBottom - Vector3.forward * debugCapsuleRadius, debugCapsuleTop - Vector3.forward * debugCapsuleRadius);
            Gizmos.DrawLine(debugCapsuleBottom + Vector3.right * debugCapsuleRadius, debugCapsuleTop + Vector3.right * debugCapsuleRadius);
            Gizmos.DrawLine(debugCapsuleBottom - Vector3.right * debugCapsuleRadius, debugCapsuleTop - Vector3.right * debugCapsuleRadius);
        }

        UnityEditor.Handles.Label(ledgeCheckOrigin.position + Vector3.up * 0.35f, $"Ledge: {debugStatusText}");
    }
#endif
}
