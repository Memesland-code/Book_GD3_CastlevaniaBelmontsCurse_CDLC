using UnityEngine;

public class PlayerSlide : MonoBehaviour
{
	[Space(10), Header("Slide Settings")]
	[Tooltip("Constant speed at start (m/s)")]
	[SerializeField] private float slideSpeed = 14f;
	[Tooltip("Lowest speed at very end of slide (m/s)")]
	[SerializeField] private float slideEndSpeed = 8.5f;
	[Tooltip("Total duration of basic slide without tunnel (s)")]
	[SerializeField] private float slideDuration = 0.35f;
	[Tooltip("Proportion of full speed during slide (%/100)")]
	[Range(0.1f, 1f)]
	[SerializeField] private float slideConstantRatio = 0.4f;
	
	[Header("Hitbox & Ceiling Detection")]
	[SerializeField] private float slidingHeight = 1f;
	[Tooltip("Layer of solid walls and ceilings")]
	[SerializeField] private LayerMask ceilingObstacleLayers;

	public float SlidingHeight => slidingHeight;
	public float SlideSpeed => slideSpeed;
	public float SlideEndSpeed => slideEndSpeed;
	public float SlideDuration => slideDuration;
	public float SlideConstantRatio => slideConstantRatio;
	
	private PlayerController player;


	private void Awake()
	{
		player = GetComponent<PlayerController>();
	}

	

	public void SetSlideHitbox(bool isSliding)
	{
		float minAllowedHeight = player.CapsuleCollider.radius * 2f;
		float targetHeight = isSliding
			? Mathf.Max(slidingHeight, minAllowedHeight)
			: Mathf.Max(player.StandingHeight, minAllowedHeight);
		
		player.CapsuleCollider.height = targetHeight;
		player.CapsuleCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);
	}



	public bool CanStandUp()
	{
		// Error margin to avoid detecting a wall
		float checkRadius = player.CapsuleCollider.radius * 0.85f;
		float minAllowedHeight = player.CapsuleCollider.radius * 2f;
		float effectiveSlideHeight = Mathf.Max(slidingHeight, minAllowedHeight);
		
		Vector3 bottomSphere = transform.position + Vector3.up * effectiveSlideHeight;
		Vector3 topSphere = transform.position + Vector3.up * (player.StandingHeight - checkRadius);
		
		if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

		return !Physics.CheckCapsule(
			bottomSphere,
			topSphere,
			checkRadius,
			ceilingObstacleLayers,
			QueryTriggerInteraction.Ignore
		);
	}
}
