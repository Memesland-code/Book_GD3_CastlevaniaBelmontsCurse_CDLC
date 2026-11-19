using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SwordHitbox : MonoBehaviour
{
	private readonly HashSet<IDamageable> hitTargetsThisSwing = new();
	private BoxCollider triggerCollider;
	private int currentDamage = 1;


	private void Awake()
	{
		triggerCollider = GetComponent<BoxCollider>();
		triggerCollider.isTrigger = true;
		triggerCollider.enabled = false;
	}



	public void ActivateHitbox(int damage)
	{
		currentDamage = damage;
		hitTargetsThisSwing.Clear();
		triggerCollider.enabled = true;
	}



	public void DeactivateHitbox()
	{
		triggerCollider.enabled = false;
		hitTargetsThisSwing.Clear();
	}


	private void OnTriggerEnter(Collider other)
	{
		IDamageable target = other.GetComponentInParent<IDamageable>();
		if (target == null || !hitTargetsThisSwing.Add(target)) return;
		
		target.TakeDamage(currentDamage);
	}
}
