using System.Collections;
using UnityEngine;

public class GroundPoundPlayerSystem : MonoBehaviour
{
	[Header("Ground Pound Settings")]
	[SerializeField] private LayerMask breakablePlatformLayer;
	[SerializeField] private float groundPoundAcceleration = 85f;
	[SerializeField] private float groundPoundMaxSpeed = 34f;
	[SerializeField] private float groundPoundInputThreshold = -0.25f;

	[Header("Ground Pound Feedbacks")]
	[SerializeField] private Unity.Cinemachine.CinemachineImpulseSource groundPoundInpulse;
	[SerializeField] private float breakHitStopDuration = 0.045f;
	[Range(0f, 1f)] [SerializeField] private float breakHitStopTimeScale = 0.12f;

	private Coroutine hitStopRoutine;
	
	public LayerMask BreakablePlatformLayer => breakablePlatformLayer;
	public float GroundPoundAcceleration => groundPoundAcceleration;
	public float GroundPoundMaxSpeed => groundPoundMaxSpeed;
	public float GroundPoundInputThreshold => groundPoundInputThreshold;


	public void TriggerGroundPoundBreakFeedback()
	{
		if (groundPoundInpulse != null)
		{
			groundPoundInpulse.GenerateImpulse();
		}

		if (breakHitStopDuration > 0f)
		{
			if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
			hitStopRoutine = StartCoroutine(HitStopRoutine());
		}
	}



	private IEnumerator HitStopRoutine()
	{
		Time.timeScale = breakHitStopTimeScale;
		yield return new WaitForSecondsRealtime(breakHitStopDuration);
		Time.timeScale = 1f;
		hitStopRoutine = null;
	}
}
