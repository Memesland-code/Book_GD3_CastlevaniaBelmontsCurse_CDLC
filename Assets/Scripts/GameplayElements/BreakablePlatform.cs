using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BreakablePlatform : MonoBehaviour
{
    [Header("Break Settings")]
    [Tooltip("Minimum fall distance (m) to break the platform")]
    [Min(0f)][SerializeField] private float breakHeightRequired = 4f;

    [Header("Destruction VFX")]
    [SerializeField] private ParticleSystem smokeVfxPrefab;
    [Tooltip("Density depending on platform volume")]
    [SerializeField] private float particlesPerCubicMeter = 8f;
    [SerializeField] private int minParticles = 15;
    [SerializeField] private int maxParticles = 120;
    
    private BoxCollider boxCollider;
    private bool isBroken;

    public float BreakHeightRequired => breakHeightRequired;
    public bool IsBroken => isBroken;
    public float TopY => boxCollider != null ? boxCollider.bounds.max.y : transform.position.y;


    private void Awake()
    {
	    boxCollider = GetComponent<BoxCollider>();
    }



    public bool CanBreakFromHeight(float groundPoundStartY)
    {
	    if (isBroken) return false;
	    float fallDistance = groundPoundStartY - TopY;
	    return fallDistance >= breakHeightRequired;
    }



    public void Break()
    {
	    if (isBroken) return;
	    isBroken = true;

	    SpawnAdaptedSmokeVfx();
	    
	    // Disables immediately the collider so PhysX does not blocks the character's physics
	    if (boxCollider != null) boxCollider.enabled = false;
	    Destroy(gameObject);
    }



    private void SpawnAdaptedSmokeVfx()
    {
	    if (smokeVfxPrefab == null || boxCollider == null) return;
	    
	    Bounds bounds = boxCollider.bounds;
	    ParticleSystem vfxInstance = Instantiate(smokeVfxPrefab, bounds.center, Quaternion.identity);
	    
	    // Match the box emitter shape to platform's world size
	    ParticleSystem.ShapeModule shape = vfxInstance.shape;
	    shape.scale = bounds.size;
	    
	    // Scale burst particle count proportionally to platform's volume
	    float volume = Mathf.Max(bounds.size.x * bounds.size.y * bounds.size.z, 0.1f);
	    short particleCount = (short)Mathf.Clamp(
		    Mathf.RoundToInt(volume * particlesPerCubicMeter),
		    minParticles,
		    maxParticles
		);
	    
	    ParticleSystem.EmissionModule emission = vfxInstance.emission;
	    if (emission.burstCount > 0)
	    {
		    ParticleSystem.Burst burst = emission.GetBurst(0);
		    burst.count = particleCount;
		    emission.SetBurst(0, burst);
	    }
	    
	    vfxInstance.Play();
    }
    
    
    
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		BoxCollider col = GetComponent<BoxCollider>();
		if (col == null) return;

		// Visualize the required start height above the platform
		Vector3 topCenter = new(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z);
		Vector3 requiredHeightPos = topCenter + Vector3.up * breakHeightRequired;

		Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.75f);
		Gizmos.DrawLine(topCenter, requiredHeightPos);
		Gizmos.DrawWireCube(requiredHeightPos, new Vector3(col.bounds.size.x, 0.05f, col.bounds.size.z));
	}
#endif
}
