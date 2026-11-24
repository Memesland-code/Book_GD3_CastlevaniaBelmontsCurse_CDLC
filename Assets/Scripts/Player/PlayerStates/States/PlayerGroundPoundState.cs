using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class PlayerGroundPoundState : PlayerState
{
	private float startY;
	private float currentDownwardSpeed;
	private readonly RaycastHit[] castHits = new RaycastHit[8];
	private readonly RaycastHit[] groundHits = new RaycastHit[4];
	
	public PlayerGroundPoundState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		startY = player.transform.position.y;
		
		// If moving upwards, reset velocity to 0
		float initialYVelocity = player.Rigidbody.linearVelocity.y;
		currentDownwardSpeed = initialYVelocity < 0f
			? Mathf.Min(Mathf.Abs(initialYVelocity), player.GroundPoundSystem.GroundPoundMaxSpeed)
			: 0f;
		
		// Lock horizontal velocity for vertical rocket feel
		player.Rigidbody.linearVelocity = new Vector3(0f, -currentDownwardSpeed, 0f);
	}
	


	public override void PhysicsUpdate()
	{
		// Accelerate downwards to velocityMax without exceeding it
		currentDownwardSpeed = Mathf.MoveTowards(
			currentDownwardSpeed,
			player.GroundPoundSystem.GroundPoundMaxSpeed,
			player.GroundPoundSystem.GroundPoundAcceleration
		);

		// Predictive check before hitting platform to avoid losing momentum
		CheckAndBreakPlatformsAhead();
		
		player.Rigidbody.linearVelocity = new Vector3(0f, -currentDownwardSpeed, 0f);

		if (HasHitAnySolidGround())
		{
			stateMachine.ChangeState(player.GroundedState);
		}
	}



	private void CheckAndBreakPlatformsAhead()
	{
		CapsuleCollider col = player.CapsuleCollider;
		Vector3 center = col.bounds.center;
		float radius = col.radius * 0.92f;
		float halfSegment = Mathf.Max(col.height * 0.5f - col.radius, 0f);
		
		Vector3 pointBottom = center + Vector3.down * halfSegment;
		Vector3 pointTop = center + Vector3.up * halfSegment;

		float castDistance = currentDownwardSpeed * Time.fixedDeltaTime + 0.12f;

		int hitCount = Physics.CapsuleCastNonAlloc(
			pointBottom,
			pointTop,
			radius,
			Vector3.down,
			castHits,
			castDistance,
			player.GroundPoundSystem.BreakablePlatformLayer,
			QueryTriggerInteraction.Ignore
		);

		bool brokeAtLeastOne = false;

		for (int i = 0; i < hitCount; i++)
		{
			LightBreakablePlatform platform = castHits[i].collider.GetComponentInParent<LightBreakablePlatform>();
			if (platform == null || platform.IsBroken) continue;

			if (platform.CanBreakFromHeight(startY))
			{
				platform.Break();
				brokeAtLeastOne = true;
			}
		}

		if (brokeAtLeastOne)
		{
			player.GroundPoundSystem.TriggerGroundPoundBreakFeedback();
		}
	}



	private bool HasHitAnySolidGround()
	{
		Bounds bounds = player.CapsuleCollider.bounds;
		float radius = player.CapsuleCollider.radius * 0.85f;
		float castDistance = (bounds.extents.y - radius) + 0.08f;

		// Ignore uniquement le layer Ignore Raycast (ex. LightPlatform traversable)
		int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
		int mask = ignoreRaycastLayer >= 0 ? ~(1 << ignoreRaycastLayer) : ~0;

		int hitCount = Physics.SphereCastNonAlloc(
			bounds.center,
			radius,
			Vector3.down,
			groundHits,
			castDistance,
			mask,
			QueryTriggerInteraction.Ignore
		);

		for (int i = 0; i < hitCount; i++)
		{
			Collider col = groundHits[i].collider;
			if (col == null || col.transform.IsChildOf(player.transform)) continue;

			return true;
		}

		return false;
	}
}
