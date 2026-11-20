using System;
using UnityEngine;

public class GroundDetectorPlayerSystem : MonoBehaviour
{
	[SerializeField] private float distance;
	[SerializeField] private LayerMask groundLayers;

	private PlayerController player;
	
	public bool touched;


	private void Awake()
	{
		player = GetComponentInParent<PlayerController>();
	}

	
	
	private void Update()
	{
		touched = Physics.Raycast(transform.position + new Vector3(0, 1, 0), Vector3.down, distance + 1f, groundLayers, QueryTriggerInteraction.Ignore);
	}

	
	
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		PlayerController pc = GetComponent<PlayerController>();
		if (pc == null || !pc.IsGizmoEnabled(PlayerDebugGizmos.GroundDetector)) return;
		
		Gizmos.color = touched ? Color.green : Color.yellow;
		Gizmos.DrawRay(transform.position, Vector3.down * distance);
	}
#endif
}
