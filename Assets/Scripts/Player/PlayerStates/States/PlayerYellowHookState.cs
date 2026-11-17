using UnityEngine;

public class PlayerYellowHookState : PlayerState
{
	private HookTarget targetHook;
	private Vector3 centerOffset;
	private int swingDirection;

	private float currentRadius;
	private float currentAngle;
	private float elapsedSwingTime;
	private Vector3 currentVelocity;
	
	public PlayerYellowHookState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


	public override void Enter()
	{
		targetHook = player.WhipSystem.ActiveHook;
		if (targetHook == null)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}

		player.ClearCoyoteTime();
		player.ResetJumpsCombo();
		player.SetPhysicsMaterial(false);

		player.Rigidbody.linearVelocity = Vector3.zero;
		player.Rigidbody.isKinematic = true;

		centerOffset = Vector3.up * player.CapsuleCollider.center.y;

		float moveX = player.Input != null ? player.Input.Move.x : 0f;
		if (Mathf.Abs(moveX) > 0.05f)
		{
			swingDirection = moveX > 0f ? 1 : -1;
		}
		else
		{
			swingDirection = player.FacingDirection;
		}

		player.SetFacingDirection(swingDirection > 0);

		Vector2 radial = GetPlayerCenter2D() - GetHookPos2D();
		currentRadius = Mathf.Max(radial.magnitude, 0.35f);
		currentAngle = radial.sqrMagnitude > 0.0001f ? Mathf.Atan2(radial.y, radial.x) : -Mathf.PI * 0.5f;

		elapsedSwingTime = 0f;
		currentVelocity = Vector3.zero;

		player.WhipSystem.ShowRope(targetHook.AnchorPosition);
	}


	public override void HandleInput()
	{
		if (player.Input == null) return;
		
		// Let go when whip button released
		if (!player.Input.IsWhipHeld)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}
		
		// Let go on attack
		if (player.Input.AttackPressedThisFrame)
		{
			//TODO Make attack
			stateMachine.ChangeState(player.AirState);
			return;
		}
	}


	public override void PhysicsUpdate()
	{
		if (targetHook == null)
		{
			stateMachine.ChangeState(player.AirState);
			return;
		}

		float dt = Time.fixedDeltaTime;
		elapsedSwingTime += dt;

		float maxRadius = player.WhipSystem.MaxRopeLength;
		float targetSpeed = player.WhipSystem.SwingSpeed;
		float accelDuration = player.WhipSystem.SwingAccelerationDuration;
		
		// Ease-in at swing start
		float accelProgress = accelDuration > 0.001f ? Mathf.Clamp01(elapsedSwingTime / accelDuration) : 1f;
		float smoothAccel = Mathf.SmoothStep(0f, 1f, accelProgress);
		float currentTangentialSpeed = Mathf.Lerp(targetSpeed * player.WhipSystem.SwingStartSpeedRatio, targetSpeed, smoothAccel);
		
		// Smooth, simultaneous convergence of the radius toward maxRopeLength
		float settleFactor = 1f - Mathf.Exp(-player.WhipSystem.RopeSettleSharpness * dt);
		currentRadius = Mathf.Lerp(currentRadius, maxRadius, settleFactor);
		
		// Continunous rotation around hook
		float safeRadiusForAngle = Mathf.Max(currentRadius, 0.6f);
		float angularSpeed = currentTangentialSpeed / safeRadiusForAngle;
		currentAngle += swingDirection * angularSpeed * dt;

		Vector2 hookPos = GetHookPos2D();
		Vector2 playerCenter = GetPlayerCenter2D();
		Vector2 nextRadial = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * currentRadius;
		Vector2 nextCenter = hookPos + nextRadial;

		ApplyMotion(playerCenter, nextCenter, dt);
		
		// Auto exit at max angle
		float angleFromUp = Vector2.Angle(Vector2.up, nextRadial.normalized);
		if (elapsedSwingTime > 0.12f && angleFromUp <= player.WhipSystem.MaxSwingUpwardAngle && currentVelocity.y > 0f)
		{
			stateMachine.ChangeState(player.AirState);
		}
	}



	private void ApplyMotion(Vector2 currentCenter, Vector2 targetCenter, float dt)
	{
		Vector2 displacement = targetCenter - currentCenter;
		float stepDistance = displacement.magnitude;

		if (stepDistance > 0.0001f)
		{
			// If blocked by wall stop movement
			if (Physics.CapsuleCast(
				    player.transform.position + Vector3.up * player.CapsuleCollider.radius,
				    player.transform.position + Vector3.up * (player.CapsuleCollider.height - player.CapsuleCollider.radius),
				    player.CapsuleCollider.radius * 0.9f,
				    displacement.normalized,
				    stepDistance,
				    player.SolidObstacleLayers,
				    QueryTriggerInteraction.Ignore))
			{
				stateMachine.ChangeState(player.AirState);
				return;
			}
		}

		currentVelocity = new Vector3(displacement.x / dt, displacement.y / dt, 0f);
		
		Vector3 targetRootPos = new Vector3(targetCenter.x, targetCenter.y, player.transform.position.z) - centerOffset;
		player.Rigidbody.MovePosition(targetRootPos);
	}

	
	

	public override void Exit()
	{
		player.WhipSystem.HideRope();
		player.WhipSystem.StartWhipCooldown();
		
		player.Rigidbody.isKinematic = false;
		player.Rigidbody.linearVelocity = currentVelocity * player.WhipSystem.YellowExitVelocityMultiplier;
	}



	private Vector2 GetHookPos2D() => new(targetHook.AnchorPosition.x, targetHook.AnchorPosition.y);

	private Vector2 GetPlayerCenter2D()
	{
		Vector3 c = player.CapsuleCollider.bounds.center;
		return new Vector2(c.x, c.y);
	}
}
