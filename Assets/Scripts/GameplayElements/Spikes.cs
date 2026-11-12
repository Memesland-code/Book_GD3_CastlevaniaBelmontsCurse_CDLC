using System;
using UnityEngine;

public class Spikes : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		if (other.TryGetComponent(out PlayerController player))
		{
			player.TriggerTrapRespawn();
		}
	}
}
