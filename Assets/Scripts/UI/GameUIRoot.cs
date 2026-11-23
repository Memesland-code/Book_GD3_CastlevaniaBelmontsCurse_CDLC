using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class GameUIRoot : MonoBehaviour
{
    public static GameUIRoot Instance { get; private set; }
    
    [Header("Sub-Systems")]
    [SerializeField] private PauseManager pauseManager;
    
    public PauseManager PauseManager => pauseManager;


    private void Awake()
    {
	    if (Instance != null && Instance != this)
	    {
		    Destroy(gameObject);
		    return;
	    }

	    Instance = this;
	    EnsureEventSystemExists();
    }



    private void OnDestroy()
    {
	    if (Instance == this)
	    {
		    Instance = null;
	    }    
    }



    private static void EnsureEventSystemExists()
    {
	    if (FindAnyObjectByType<EventSystem>() != null) return;
	    
	    GameObject eventSystemObj = new("EventSystem");
	    eventSystemObj.AddComponent<EventSystem>();
	    eventSystemObj.AddComponent<InputSystemUIInputModule>();
    }
}
