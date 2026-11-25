using UnityEngine;

public class BreakableDoor : MonoBehaviour, IDamageable
{
	[SerializeField] private bool isPlankRight = true;
	[SerializeField] private Transform plankPivot;

	private BoxCollider collider;
	private bool isBroken;
	
	private Transform player;


	private void Awake()
	{
		collider = GetComponent<BoxCollider>();
		player = GameObject.FindGameObjectWithTag("Player").transform;
		SetPlankSide();
	}


	
	public void TakeDamage(int amount)
	{
		Vector3 center = collider.bounds.center;
		float centerX = center.x;

		if (isPlankRight && player.position.x > centerX || !isPlankRight && player.position.x < centerX)
		{
			Break();
		}
	}



	private void Break()
	{
		if (isBroken) return;
		isBroken = true;
		
		Destroy(gameObject);
	}



	private void SetPlankSide()
	{
		if (isPlankRight)
		{
			plankPivot.rotation = Quaternion.Euler(0f, -180f, 0f);
		}
		else
		{
			plankPivot.rotation = Quaternion.Euler(0f, 0f, 0f);
		}
	}

	

	private void OnValidate()
	{
		SetPlankSide();
	}
}
