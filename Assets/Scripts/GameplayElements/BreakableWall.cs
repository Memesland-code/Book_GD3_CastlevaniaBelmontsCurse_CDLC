using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BreakableWall : MonoBehaviour, IDamageable
{
	[Header("VFX")]
	[SerializeField] private ParticleSystem smokeVfx;
	[SerializeField] private float vfxFallbackDestroyDelay = 3f;

	private Collider wallCollider;
	private bool isBroken;


	private void Awake()
	{
		wallCollider = GetComponent<Collider>();
	}


	
	public void TakeDamage(int amount)
	{
		if (isBroken || amount <= 0) return;
		isBroken = true;

		SpawnDestroyVfx();
		Destroy(gameObject);
	}



	private void SpawnDestroyVfx()
	{
		if (smokeVfx == null) return;

		Vector3 spawnPos = wallCollider != null ? wallCollider.bounds.center : transform.position;
		ParticleSystem vfxInstance = Instantiate(smokeVfx, spawnPos, Quaternion.identity);
		smokeVfx.Play();
		
		Destroy(vfxInstance, vfxFallbackDestroyDelay);
	}
}
