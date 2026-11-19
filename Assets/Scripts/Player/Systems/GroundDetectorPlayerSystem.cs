using System;
using UnityEngine;

public class GroundDetectorPlayerSystem : MonoBehaviour
{
	[SerializeField] private float distance;

	public bool touched;

	private void Update()
	{
		touched = Physics.Raycast(transform.position + new Vector3(0, 1, 0), Vector3.down, distance + 1);
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
