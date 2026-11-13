using UnityEngine;

public class CameraFollowPlayerSystem : MonoBehaviour
{
	[SerializeField] private Transform playerTransform;
	[SerializeField] private Vector3 offset;
	[SerializeField] private float smoothSpeed;

	private void LateUpdate()
	{
		transform.position = new Vector3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z) + offset;
	}
}
