using System.Collections;
using UnityEngine;

public class PlayerSwordAttackSystem : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private SwordHitbox swordHitbox;

	[Header("Sword Settings")]
	[SerializeField] private int swordDamage = 1;
	[SerializeField] private float activeHitboxDuration = 0.14f;
	[SerializeField] private float attackCooldown = 0.35f;
	[SerializeField] private bool allowAirAttack = true;
	
	private static readonly int AttackAnim = Animator.StringToHash("Attack");

	private PlayerController player;
	private Coroutine attackCoroutine;
	private float cooldownTimer;
	private bool isAttacking;

	public bool IsAttacking => isAttacking;


	private void Awake()
	{
		player = GetComponent<PlayerController>();
	}


	private void Update()
	{
		if (cooldownTimer > 0f)
		{
			cooldownTimer -= Time.deltaTime;
		}
	}



	public bool TryAttack()
	{
		if (isAttacking || cooldownTimer > 0f) return false;
		if (!allowAirAttack && !player.IsGrounded) return false;
		
		// Block sword attack in some states
		var currentState = player.StateMachine.CurrentState;
		if (currentState == player.WallRunState ||
		    currentState == player.SlideState ||
		    currentState == player.GroundPoundState ||
		    currentState == player.WallJumpState)
		{
			return false;
		}
		
		if (attackCoroutine != null) StopCoroutine(attackCoroutine);
		attackCoroutine = StartCoroutine(PerformSwingRoutine());
		return true;
	}



	private IEnumerator PerformSwingRoutine()
	{
		isAttacking = true;
		cooldownTimer = attackCooldown;

		if (player.Animator is not null)
		{
			player.Animator.SetTrigger(AttackAnim);
		}

		if (swordHitbox is not null)
		{
			swordHitbox.ActivateHitbox(swordDamage);
		}
		
		yield return new WaitForSeconds(activeHitboxDuration);
		
		if (swordHitbox is not null)
		{
			swordHitbox.DeactivateHitbox();
		}
		
		isAttacking = false;
		attackCoroutine = null;
	}
}
