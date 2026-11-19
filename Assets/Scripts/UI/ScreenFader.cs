using System;
using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
	public static ScreenFader Instance { get; private set; }

	[SerializeField] private CanvasGroup fadeCanvasGroup;


	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;

		if (fadeCanvasGroup != null)
		{
			fadeCanvasGroup.alpha = 0;
			fadeCanvasGroup.blocksRaycasts = false;
		}
	}



	public IEnumerator FadeOut(float duration)
	{
		yield return FadeRoutine(1f, duration);
	}

	public IEnumerator FadeIn(float duration)
	{
		yield return FadeRoutine(0f, duration);
	}



	private IEnumerator FadeRoutine(float targetAlpha, float duration)
	{
		if (fadeCanvasGroup == null) yield break;

		float startAlpha = fadeCanvasGroup.alpha;
		if (duration <= 0f)
		{
			fadeCanvasGroup.alpha = targetAlpha;
			yield break;
		}

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
			yield return null;
		}

		fadeCanvasGroup.alpha = targetAlpha;
	}
}
