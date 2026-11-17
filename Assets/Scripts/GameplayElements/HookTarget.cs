using UnityEngine;

public enum HookType
{
	Purple,
	Yellow
}

public enum HookHighlightState
{
	None,
	InRange,
	Targeted
}

[RequireComponent(typeof(SphereCollider))]
public class HookTarget : MonoBehaviour
{
	[Header("Identity")]
	[SerializeField] private HookType hookType = HookType.Purple;

	[Header("Visual Feedback References")]
	[SerializeField] private Renderer hookRenderer;

	[ColorUsage(false, true)]
	[SerializeField] private Color glowColor = new(0.7f, 0.2f, 1f, 1f);

	[Header("HDRP emission multipliers (using with bloom)")]
	[SerializeField] private float inRangeEmissionIntensity = 150f;
	[SerializeField] private float targetedEmissionIntensity = 350f;

#if UNITY_EDITOR
	[Header("Editor Preview")]
	[SerializeField] private HookHighlightState previewState = HookHighlightState.None;
#endif
	
	private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
	private MaterialPropertyBlock propertyBlock;
	private HookHighlightState currentState = (HookHighlightState)(-1);

	public HookType Type => hookType;
	public Vector3 AnchorPosition => transform.position;


	private void Awake()
	{
		propertyBlock = new MaterialPropertyBlock();
		SetHighlightState(HookHighlightState.None);
	}



	public void SetHighlightState(HookHighlightState newState)
	{
		if (currentState == newState) return;
		currentState = newState;

		ApplyVisualState(newState);
	}



	private void ApplyVisualState(HookHighlightState state)
	{
		if (hookRenderer == null) return;

		propertyBlock ??= new MaterialPropertyBlock();

		float emissionMult = state switch
		{
			HookHighlightState.InRange => inRangeEmissionIntensity,
			HookHighlightState.Targeted => targetedEmissionIntensity,
			_ => 0f
		};

		hookRenderer.GetPropertyBlock(propertyBlock);
		propertyBlock.SetColor(EmissiveColorId, glowColor * emissionMult);
		hookRenderer.SetPropertyBlock(propertyBlock);
	}
	
	
	
#if UNITY_EDITOR
	// allows to try 3 states directly in editor
	private void OnValidate()
	{
		ApplyVisualState(previewState);
	}
#endif
}
