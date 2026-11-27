using UnityEngine;

public class SecretRoomCache : MonoBehaviour
{
    [SerializeField] private GameObject secretCacheRef;


    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController player) && secretCacheRef is not null)
        {
            secretCacheRef.SetActive(false);
            GetComponent<BoxCollider>().enabled = false;
        }
    }
}
