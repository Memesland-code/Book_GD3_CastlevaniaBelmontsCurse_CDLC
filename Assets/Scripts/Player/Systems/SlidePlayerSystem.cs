using System;
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


	private void Awake()
	{
		player = GetComponent<PlayerController>();

		if (visualMesh != null)
		{
			defaultMeshLocalRotation = visualMesh.localRotation;
			defaultMeshLocalPosition = visualMesh.localPosition;
		}
	}


	
	private void Update()
	{
		if (visualMesh is null) return;
		
		Quaternion targetRot = isSlidingVisual
			? defaultMeshLocalRotation * Quaternion.Euler(slideLocalRotationEuler)
			: defaultMeshLocalRotation;

		Vector3 targetPos = isSlidingVisual
			? defaultMeshLocalPosition + slideLocalPositionOffset
			: defaultMeshLocalPosition;

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



	public void SetSlideVisual(bool sliding)
	{
		isSlidingVisual = sliding;
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
}
