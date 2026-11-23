using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class LightPlatform : MonoBehaviour
{
    public enum PlatformVisualState {
	    IdleFloating,			// player is far
	    SolidifiedNearPlayer,	// player is near without halo
	    DissolvedByHalo			// player is near with halo
    }
    
    public static readonly List<LightPlatform> ActivePlatforms = new();
    public static IReadOnlyList<LightPlatform> ActiveLightPlatforms => ActivePlatforms;

    [Header("Proximity & Transition")]
    [SerializeField] private float solidifyDetectionRange = 4.5f;
    
    [Header("Floating Rocks Settings")]
    [SerializeField] private GameObject rockFragmentPrefab;
    [SerializeField] private float rockDensity = 1.5f;
    [SerializeField] private int maxRockCount = 25;
    [SerializeField] private Vector2 rockScaleRange = new(0.65f, 1.25f);
    [Min(0f)] [SerializeField] private float boundsMargin = 0.2f;
    
    [Header("Rock State Scales & Motion")]
    [Range(1f, 3f)][SerializeField] private float enlargedRockScaleMultiplier = 1.65f;
    [Range(0.05f, 0.8f)][SerializeField] private float shrunkRockScaleMultiplier = 0.25f;
    [SerializeField] private float rockScaleTransitionSpeed = 14f;
    [SerializeField] private float floatAmplitude = 0.08f;
    [SerializeField] private float floatSpeed = 1.6f;
    
    private static readonly int SolidifyAmountId = Shader.PropertyToID("_SolidifyAmount");
    
    private BoxCollider boxCollider;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private int defaultSolidLayer;
    private int intangibleLayer;
    private PlatformVisualState currentState = PlatformVisualState.IdleFloating;
    private Vector3 lastPlayerCenter;
    
    private readonly List<Transform> spawnedRocks = new();
    private readonly List<Vector3> rockBaseLocalPositions = new();
    private readonly List<Vector3> rockBaseLocalScales = new();
    private readonly List<float> rockPhaseOffsets = new();

    public BoxCollider Collider => boxCollider;
    public float SolidifyDetectionRange => solidifyDetectionRange;


    private void Awake()
    {
	    boxCollider = GetComponent<BoxCollider>();
	    meshRenderer = GetComponent<MeshRenderer>();
	    propertyBlock = new MaterialPropertyBlock();

	    defaultSolidLayer = gameObject.layer;
	    int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
	    intangibleLayer = ignoreRaycast >= 0 ? ignoreRaycast : defaultSolidLayer;
	    
	    meshRenderer.GetPropertyBlock(propertyBlock);
	    propertyBlock.SetFloat(SolidifyAmountId, 0f);
	    meshRenderer.SetPropertyBlock(propertyBlock);
	    
	    CacheExistingRocksOrGenerate();
	    ApplyVisuals(PlatformVisualState.IdleFloating, transform.position, 0f, instant: true);
    }


    private void OnEnable()
    {
	    if (!ActivePlatforms.Contains(this))
	    {
		    ActivePlatforms.Add(this);
	    }
    }


    private void OnDisable()
    {
	    ActivePlatforms.Remove(this);
	    SetIntangibleLayer(false);
    }


    private void Update()
    {
	    AnimateFloatingRocks();
    }



    public void SetIntangibleLayer(bool isIntangible)
    {
	    int targetLayer = isIntangible ? intangibleLayer : defaultSolidLayer;
	    if (gameObject.layer != targetLayer)
	    {
		    gameObject.layer = targetLayer;
	    }
    }



    public void UpdateRuntimeState(PlatformVisualState state, Vector3 playerCenter, float haloRadius)
    {
	    currentState = state;
	    lastPlayerCenter = playerCenter;
	    ApplyVisuals(state, playerCenter, haloRadius, instant: false);
    }



    private void CacheExistingRocksOrGenerate()
    {
	    Transform existingContainer = transform.Find("RocksContainer");
	    if (existingContainer != null && existingContainer.childCount > 0)
	    {
		    spawnedRocks.Clear();
		    rockBaseLocalPositions.Clear();
		    rockBaseLocalScales.Clear();
		    rockPhaseOffsets.Clear();

		    for (int i = 0; i < existingContainer.childCount; i++)
		    {
			    Transform rock = existingContainer.GetChild(i);
			    spawnedRocks.Add(rock);
			    rockBaseLocalPositions.Add(rock.localPosition);
			    rockBaseLocalScales.Add(rock.localScale);
			    rockPhaseOffsets.Add(Random.Range(0f, Mathf.PI * 2f));
		    }
		    
		    return;
	    }
	    
	    GenerateFloatingRocks();
    }



    [ContextMenu("Regenerate Floating Rocks")]
    public void GenerateFloatingRocks()
    {
#if UNITY_EDITOR
	    Undo.RegisterCompleteObjectUndo(this, "Regenerate LightPlatform Rocks");
	    
	    // Clean potential old fragments
	    for (int i = transform.childCount - 1; i >= 0; i--)
	    {
		    Transform child = transform.GetChild(i);
		    if (child.name == "RocksContainer" || child.name.StartsWith("RockFragment_"))
		    {
			    if (Application.isPlaying) Destroy(child.gameObject);
			    else DestroyImmediate(child.gameObject);
		    }
	    }

	    spawnedRocks.Clear();
	    rockBaseLocalPositions.Clear();
	    rockBaseLocalScales.Clear();
	    rockPhaseOffsets.Clear();

	    if (rockFragmentPrefab == null || rockDensity <= 0) return;
	    if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
	    
	    // Container that cancels non-uniform parent scaling and rotation to avoid visual problems
	    GameObject container = new("RocksContainer");
	    container.transform.SetParent(transform, false);
	    Vector3 parentScale = transform.lossyScale;
	    container.transform.localScale = new Vector3(
		    1f / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
		    1f / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
		    1f / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f)
	    );
	    
	    Vector3 fullWorldSize = Vector3.Scale(boxCollider.size, parentScale);
	    Vector3 worldCenterOffset = Vector3.Scale(boxCollider.center, parentScale);
	    
	    // Apply user-defined margin on both sides of each axis
	    Vector3 totalMargin = new(boundsMargin * 2f, (boundsMargin + floatAmplitude) * 2f, boundsMargin * 2f);
	    
	    Vector3 safeWorldSize = new(
		    Mathf.Max(fullWorldSize.x - totalMargin.x, fullWorldSize.x * 0.2f),
		    Mathf.Max(fullWorldSize.y - totalMargin.y, fullWorldSize.y * 0.2f),
		    Mathf.Max(fullWorldSize.z - totalMargin.z, fullWorldSize.z * 0.2f)
	    );
	    Vector3 halfSafeSize = safeWorldSize * 0.5f;

	    // Subdivie volume into proportionnal 3D cells for even rocks coverage in volume
	    float volume = safeWorldSize.x * safeWorldSize.y * safeWorldSize.z;
	    int targetRockCount = Mathf.Clamp(Mathf.RoundToInt(volume * rockDensity), 1, maxRockCount);
	    float cellUnit = Mathf.Pow(volume / targetRockCount, 1f / 3f);

	    int countX = Mathf.Max(1, Mathf.RoundToInt(safeWorldSize.x / cellUnit));
	    int countY = Mathf.Max(1, Mathf.RoundToInt(safeWorldSize.y / cellUnit));
	    int countZ = Mathf.Max(1, Mathf.RoundToInt(safeWorldSize.z / cellUnit));

	    Vector3 cellSize = new Vector3(
		    safeWorldSize.x / countX,
		    safeWorldSize.y / countY,
		    safeWorldSize.z / countZ
	    );
	    
		Vector3 startCorner = worldCenterOffset - halfSafeSize;
        int index = 0;

        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    Vector3 cellCenter = startCorner + new Vector3(
                        (x + 0.5f) * cellSize.x,
                        (y + 0.5f) * cellSize.y,
                        (z + 0.5f) * cellSize.z
                    );

                    // Offset within cell bounds to break grid uniformity without escaping safe bounds
                    Vector3 jitter = new Vector3(
                        Random.Range(-cellSize.x * 0.35f, cellSize.x * 0.35f),
                        Random.Range(-cellSize.y * 0.35f, cellSize.y * 0.35f),
                        Random.Range(-cellSize.z * 0.35f, cellSize.z * 0.35f)
                    );

                    Vector3 offsetFromCenter = cellCenter + jitter - worldCenterOffset;
                    offsetFromCenter = new Vector3(
                        Mathf.Clamp(offsetFromCenter.x, -halfSafeSize.x, halfSafeSize.x),
                        Mathf.Clamp(offsetFromCenter.y, -halfSafeSize.y, halfSafeSize.y),
                        Mathf.Clamp(offsetFromCenter.z, -halfSafeSize.z, halfSafeSize.z)
                    );

                    Vector3 localPosInContainer = worldCenterOffset + offsetFromCenter;

                    GameObject rock = Instantiate(rockFragmentPrefab, container.transform);
                    Undo.RegisterCreatedObjectUndo(rock, "Create Rock");
                    
                    rock.name = $"RockFragment_{index++}";
                    rock.transform.localPosition = localPosInContainer;
                    rock.transform.localRotation = Random.rotation;

                    Vector3 baseScale = Vector3.one * Random.Range(rockScaleRange.x, rockScaleRange.y);
                    rock.transform.localScale = baseScale;

                    spawnedRocks.Add(rock.transform);
                    rockBaseLocalPositions.Add(localPosInContainer);
                    rockBaseLocalScales.Add(baseScale);
                    rockPhaseOffsets.Add(Random.Range(0f, Mathf.PI * 2f));
                }
            } 
        }
        
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
	        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }



    private void AnimateFloatingRocks()
    {
	    bool checkProximityFreeze = currentState == PlatformVisualState.SolidifiedNearPlayer;
	    float solidifyRangeSqrt = solidifyDetectionRange * solidifyDetectionRange;

	    for (int i = 0; i < spawnedRocks.Count; i++)
	    {
		    Transform rock = spawnedRocks[i];
		    if (rock == null || !rock.gameObject.activeSelf) continue;

		    bool isRockFrozen = checkProximityFreeze && (rock.position - lastPlayerCenter).sqrMagnitude <= solidifyRangeSqrt;
		    
		    if (isRockFrozen)
		    {
			    // Lock rocks back to their anchor position to show wall can't be passed
			    rock.localPosition = Vector3.MoveTowards(rock.localPosition, rockBaseLocalPositions[i], floatSpeed * Time.deltaTime);
		    }
		    else
		    {
			    float offsetY = Mathf.Sin(Time.time * floatSpeed + rockPhaseOffsets[i]) * floatAmplitude;
			    rock.localPosition = rockBaseLocalPositions[i] + Vector3.up * offsetY;
		    }
	    }
    }



    private void ApplyVisuals(PlatformVisualState state, Vector3 playerCenter, float haloRadius, bool instant)
    {
	    if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

	    // Hide translucent bounding volume while the halo dissolves the platform
	    bool isDissolved = state == PlatformVisualState.DissolvedByHalo;
	    if (meshRenderer.enabled == isDissolved)
	    {
		    meshRenderer.enabled = !isDissolved;
	    }

	    float haloRadiusSqr = haloRadius * haloRadius;
	    float solidifyRangeSqrt = solidifyDetectionRange * solidifyDetectionRange;

	    for (int i = 0; i < spawnedRocks.Count; i++)
	    {
		    Transform rock = spawnedRocks[i];
		    if (rock == null) continue;

		    if (!rock.gameObject.activeSelf) rock.gameObject.SetActive(true);

		    float sqrtDistToPlayer = (rock.position - playerCenter).sqrMagnitude;

		    // Enlarge all rocks when solidifed
		    Vector3 targetScale = state switch
		    {
			    PlatformVisualState.SolidifiedNearPlayer when sqrtDistToPlayer <= solidifyRangeSqrt
					=> rockBaseLocalScales[i] * enlargedRockScaleMultiplier,
			    PlatformVisualState.DissolvedByHalo when sqrtDistToPlayer <= haloRadiusSqr
				    => rockBaseLocalScales[i] * shrunkRockScaleMultiplier,
					_ => rockBaseLocalScales[i]
		    };

		    rock.localScale = instant
			    ? targetScale
			    : Vector3.MoveTowards(rock.localScale, targetScale, rockScaleTransitionSpeed * Time.deltaTime);
	    }
    }

    
    
#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		BoxCollider col = GetComponent<BoxCollider>();
		if (col == null) return;

		Gizmos.color = new Color(0.8f, 0.8f, 0.9f, 0.25f);
		Gizmos.DrawWireCube(col.bounds.center, col.bounds.size + Vector3.one * (solidifyDetectionRange * 2f));
	}
#endif
}
