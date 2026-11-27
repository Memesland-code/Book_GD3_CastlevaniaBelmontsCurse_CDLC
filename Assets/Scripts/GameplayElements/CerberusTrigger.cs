using System;
using UnityEngine;

public class CerberusTrigger : MonoBehaviour
{
    [SerializeField] private GameObject cerberusSpikes;
    [SerializeField] private float triggerReactivationCooldown = 3f;
    
    private Vector3 cerberusBasePosition;
    private BoxCollider cerberusCollider;

    private float triggerReactivationTimer;
    
        
    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        if (cerberusSpikes == null)
        {
            Debug.LogError("CerberusSpikes was not found in world!");
            return;
        }

        cerberusSpikes.GetComponent<CerberusSpikes>().cerberusTrigger = this;
        cerberusCollider = cerberusSpikes.GetComponent<BoxCollider>();
        cerberusBasePosition = cerberusSpikes.transform.position;
        ResetCerberusSpikes();
    }


    
    private void Update()
    {
        if (triggerReactivationTimer >= 0f)
        {
            triggerReactivationTimer -= Time.deltaTime;
        }
    }

    

    private void StartCerberusChase()
    {
        ResetCerberusSpikes();
        cerberusSpikes.SetActive(true);
        cerberusCollider.enabled = true;
        cerberusSpikes.GetComponent<CerberusSpikes>().StartChase();
    }



    public void ResetCerberusSpikes()
    {
        cerberusCollider.enabled = false;
        cerberusSpikes.SetActive(false);
        cerberusSpikes.transform.position = cerberusBasePosition;
        triggerReactivationTimer = 0f;
    }


    
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController player) && triggerReactivationTimer < 0f)
        {
            StartCerberusChase();
        }
    }
}
