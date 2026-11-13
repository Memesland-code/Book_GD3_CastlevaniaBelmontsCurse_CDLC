using UnityEngine;

public class SafePositionTrackerPlayerSystem : MonoBehaviour
{
    [Header("Validation Filters")]
    [SerializeField] private LayerMask safeGroundLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [Range(0.5f, 1f)] [SerializeField] private float minGroundFlatness = 0.85f;
    
    [Header("Raycast Geometry")]
    [SerializeField] private float rayOriginHeightOffset = 0.5f;
    [SerializeField] private float downwardRayDistance = 1.2f;
    [SerializeField] private float sideCheckOffset = 0.5f;
    [SerializeField] private float updateInterval = 0.1f;
    
    public Vector3 LastSafePosition { get; private set; }
    public bool HasValidSafePosition { get; private set; }

    private float checkTimer;
    private CapsuleCollider capsuleCollider;
    /*!
    private float pivotToFeetOffset;
    
    private void Awake()
    {
	    capsuleCollider = GetComponent<CapsuleCollider>();

	    if (capsuleCollider != null)
	    {
		    pivotToFeetOffset = (capsuleCollider.height * 0.5f) - capsuleCollider.center.y;
	    }
    */


    
    private void Start()
    {
	    LastSafePosition = transform.position;
	    HasValidSafePosition = true;
    }



    /// <summary>
    /// Called continuously by PlayerGroundedState.LogicUpdate()
    /// </summary>
    public void TickTracker()
    {
	    checkTimer -= Time.deltaTime;
	    if (checkTimer > 0f) return;

	    checkTimer = updateInterval;
	    EvaluateCurrentGround();
    }



    private void EvaluateCurrentGround()
    {
	    Vector3 centerOrigin = transform.position + Vector3.up * rayOriginHeightOffset;
	    
	    // Central Raycast: check under platform is valid
	    if (!IsPointSafe(centerOrigin, out RaycastHit centerHit))
	    {
		    return; // Invalid platform, keep old one
	    }

	    Vector3 leftOrigin = centerOrigin + Vector3.left * sideCheckOffset;
	    Vector3 rightOrigin = centerOrigin + Vector3.right * sideCheckOffset;

	    bool leftSafe = IsPointSafe(leftOrigin, out RaycastHit leftHit);
	    bool rightSafe = IsPointSafe(rightOrigin, out RaycastHit rightHit);
	    
	    // Case 1 - 2 rays hit -> player is in the middle of the platform
	    if (leftSafe && rightSafe)
	    {
		    LastSafePosition = centerHit.point;
		    HasValidSafePosition = true;
		    return;
	    }
	    
	    // Case 2 - only 1 ray hit -> move towards safe point
	    if (leftSafe)
	    {
		    LastSafePosition = IsPathClear(centerOrigin, Vector3.left, sideCheckOffset)
			    ? leftHit.point
			    : centerHit.point;
		    HasValidSafePosition = true;
		    return;
	    }
	    
	    if (rightSafe)
	    {
		    LastSafePosition = IsPathClear(centerOrigin, Vector3.right, sideCheckOffset)
			    ? rightHit.point
			    : centerHit.point;
		    HasValidSafePosition = true;
		    return;
	    }
	    
	    // Case 3 - no ray hit -> small platform, keep center point
	    LastSafePosition = centerHit.point;
	    HasValidSafePosition = true;
    }



    private bool IsPointSafe(Vector3 origin, out RaycastHit hit)
    {
	    bool hasHit = Physics.Raycast(
			origin,
			Vector3.down,
			out hit,
			downwardRayDistance,
			safeGroundLayers,
			QueryTriggerInteraction.Ignore
		);

	    if (!hasHit) return false;
	    
	    // Check that surface is not too sloped
	    return hit.normal.y >= minGroundFlatness;
    }



    private bool IsPathClear(Vector3 origin, Vector3 direction, float distance)
    {
	    // Horizontal Raycast for wall check
	    return !Physics.Raycast(
		    origin,
		    direction,
		    distance + 0.2f,
		    obstacleLayers,
		    QueryTriggerInteraction.Ignore
		);
    }


    private void OnDrawGizmosSelected()
    {
	    Vector3 centerOrigin = transform.position + Vector3.up * rayOriginHeightOffset;
	    Vector3 leftOrigin = centerOrigin + Vector3.left * sideCheckOffset;
	    Vector3 rightOrigin = centerOrigin + Vector3.right * sideCheckOffset;

	    Gizmos.color = Color.cyan;
	    Gizmos.DrawRay(centerOrigin, Vector3.down * downwardRayDistance);

	    Gizmos.color = Color.yellow;
	    Gizmos.DrawRay(leftOrigin, Vector3.down * downwardRayDistance);
	    Gizmos.DrawRay(rightOrigin, Vector3.down * downwardRayDistance);

	    if (HasValidSafePosition)
	    {
		    Gizmos.color = Color.green;
		    Gizmos.DrawWireSphere(LastSafePosition, 0.25f);
	    }
    }
}
