using System.Collections;
using UnityEngine;

public class SlidePlayerSystem : MonoBehaviour
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

	[Header("Visual Slide Tilt")]
	[SerializeField] private Transform visualMesh;
	[SerializeField] private Vector3 slideLocalRotationEuler = new(-90f, 0f, 0f);
	[SerializeField] private Vector3 slideLocalPositionOffset = new(0f, -0.45f, 0f);
	[SerializeField] private float visualTransitionSpeed = 16f;
	
	private Quaternion defaultMeshLocalRotation;
	private Vector3 defaultMeshLocalPosition;
	private bool isSlidingVisual;
	
	public float SlidingHeight => slidingHeight;
	public float SlideSpeed => slideSpeed;
	public float SlideEndSpeed => slideEndSpeed;
	public float SlideDuration => slideDuration;
	public float SlideConstantRatio => slideConstantRatio;
	
	private PlayerController player;
	private Coroutine resetVisualCoroutine;


	private void Awake()
	{
		player = GetComponent<PlayerController>();

		if (visualMesh != null)
		{
			defaultMeshLocalRotation = visualMesh.localRotation;
			defaultMeshLocalPosition = visualMesh.localPosition;
		}
	}

	

	public void SetSlideHitbox(bool isSliding)
	{
		float minAllowedHeight = player.CapsuleCollider.radius * 2f;
		float targetHeight = isSliding
			? Mathf.Max(slidingHeight, minAllowedHeight)
			: Mathf.Max(player.DefaultColliderHeight, minAllowedHeight);
		
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
		Vector3 topSphere = transform.position + Vector3.up * (player.DefaultColliderHeight - checkRadius);
		
		if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

		return !Physics.CheckCapsule(
			bottomSphere,
			topSphere,
			checkRadius,
			player.SolidObstacleLayers,
			QueryTriggerInteraction.Ignore
		);
	}
	
	
	

	public void BeginSlideVisual()
	{
	    CancelSlideVisualReset();
	}

	
	
	public void TickSlideVisual()
	{
	    if (visualMesh == null) return;

	    Quaternion targetRot = defaultMeshLocalRotation * Quaternion.Euler(slideLocalRotationEuler);
	    Vector3 targetPos = defaultMeshLocalPosition + slideLocalPositionOffset;

	    visualMesh.localRotation = Quaternion.Slerp(
	        visualMesh.localRotation,
	        targetRot,
	        visualTransitionSpeed * Time.deltaTime
	    );

	    visualMesh.localPosition = Vector3.Lerp(
	        visualMesh.localPosition,
	        targetPos,
	        visualTransitionSpeed * Time.deltaTime
	    );
	}

	
	
	public void EndSlideVisual()
	{
	    if (visualMesh == null) return;

	    CancelSlideVisualReset();
	    resetVisualCoroutine = StartCoroutine(ResetSlideVisualRoutine());
	}

	public void CancelSlideVisualReset()
	{
	    if (resetVisualCoroutine != null)
	    {
	        StopCoroutine(resetVisualCoroutine);
	        resetVisualCoroutine = null;
	    }
	}

	
	
	private IEnumerator ResetSlideVisualRoutine()
	{
	    while (visualMesh is not null)
	    {
	        var currentState = player.StateMachine.CurrentState;
	        if (currentState != player.SlideState &&
	            currentState != player.GroundedState &&
	            currentState != player.AirState)
	        {
	            resetVisualCoroutine = null;
	            yield break;
	        }

	        if (Quaternion.Angle(visualMesh.localRotation, defaultMeshLocalRotation) <= 0.5f &&
	            Vector3.Distance(visualMesh.localPosition, defaultMeshLocalPosition) <= 0.005f)
	        {
	            break;
	        }

	        visualMesh.localRotation = Quaternion.Slerp(
	            visualMesh.localRotation,
	            defaultMeshLocalRotation,
	            visualTransitionSpeed * Time.deltaTime
	        );

	        visualMesh.localPosition = Vector3.Lerp(
	            visualMesh.localPosition,
	            defaultMeshLocalPosition,
	            visualTransitionSpeed * Time.deltaTime
	        );

	        yield return null;
	    }

	    if (visualMesh is not null)
	    {
	        visualMesh.localRotation = defaultMeshLocalRotation;
	        visualMesh.localPosition = defaultMeshLocalPosition;
	    }

	    resetVisualCoroutine = null;
	}



	public void ResetSlideImmediate()
	{
		CancelSlideVisualReset();
		SetSlideHitbox(false);

		if (visualMesh != null)
		{
			visualMesh.localRotation = defaultMeshLocalRotation;
			visualMesh.localPosition = defaultMeshLocalPosition;
		}
	}
	
	
	
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		PlayerController pc = player != null ? player : GetComponent<PlayerController>();
		if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.Slide)) return;
		
		if (player.CapsuleCollider != null)
		{
			float checkRadius = player.CapsuleCollider.radius * 0.85f;
			float minAllowedHeight = player.CapsuleCollider.radius * 2f;
			float effectiveSlideHeight = Mathf.Max(SlidingHeight, minAllowedHeight);

			Vector3 bottomSphere = transform.position + Vector3.up * effectiveSlideHeight;
			Vector3 topSphere = transform.position + Vector3.up * (player.DefaultColliderHeight - checkRadius);
			if (topSphere.y < bottomSphere.y) topSphere = bottomSphere;

			Gizmos.color = Application.isPlaying && !CanStandUp() ? Color.red : Color.green;
			Gizmos.DrawWireSphere(bottomSphere, checkRadius);
			Gizmos.DrawWireSphere(topSphere, checkRadius);
			Gizmos.DrawLine(bottomSphere, topSphere);
		}
	}
#endif
}
