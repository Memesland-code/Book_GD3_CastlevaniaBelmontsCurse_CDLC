using UnityEngine;

public class CerberusSpikes : MonoBehaviour
{
	[SerializeField] private float chaseSpeed = 5f;
	[HideInInspector] public CerberusTrigger cerberusTrigger;

	private bool canChase;
	
	
	private void Awake()
	{
		GetComponent<BoxCollider>().isTrigger = true;
	}



	private void Update()
	{
		if (canChase)
		{
			transform.position = new Vector3(transform.position.x, transform.position.y + chaseSpeed * Time.deltaTime, transform.position.z);
		}
	}

	
	
	public void StartChase()
	{
		canChase = true;
	}


	
	private void OnTriggerEnter(Collider other)
	{
		if (other.TryGetComponent(out PlayerController player))
		{
			player.TriggerTrapRespawn();
			cerberusTrigger.ResetCerberusSpikes();
		}
	}
}
