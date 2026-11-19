using System;
using UnityEngine;

public class BreakableStatue : MonoBehaviour, IDamageable
{
	[Header("VFX")]
	[SerializeField] private ParticleSystem xpOrbVfx;
	[SerializeField] private float vfxFallBackDestroyDelay = 3f;

	private Collider statueCollider;
	private bool isBroken;


	private void Awake()
	{
		statueCollider = GetComponent<Collider>();
	}


	public void TakeDamage(int amount)
	{
		if (isBroken || amount <= 0) return;
		isBroken = true;
		
		Vector2 center = statueCollider != null ? statueCollider.bounds.center : transform.position;
		SpawnVfx(xpOrbVfx, center);
		
		Destroy(gameObject);
	}


	private void SpawnVfx(ParticleSystem prefab, Vector3 center)
	{
		if (prefab == null) return;
		
		ParticleSystem instance = Instantiate(prefab, center, Quaternion.identity);
		instance.Play();
		Destroy(instance.gameObject, vfxFallBackDestroyDelay);
	}
}
