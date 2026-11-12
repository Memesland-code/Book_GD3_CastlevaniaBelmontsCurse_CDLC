using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class GridPlatform : MonoBehaviour
{
    public BoxCollider PlatformCollider { get; private set; }
    
    /// <summary>
    /// World y coord of top surface, reference point to position player
    /// </summary>
    public float TopSurfaceY => PlatformCollider.bounds.max.y;

    private void Awake()
    {
        PlatformCollider = GetComponent<BoxCollider>();
    }
}
