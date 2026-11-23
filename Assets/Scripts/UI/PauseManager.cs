using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
	public static PauseManager Instance {get; private set;}
	public static bool IsPaused {get; private set;}

	public static event Action<bool> OnPauseStateChanged;

	[Header("UI & Input")]
	[SerializeField] private GameObject pauseMenuRoot;
	[SerializeField] private GameObject optionsMenuRoot;
	[SerializeField] private InputActionReference pauseAction;
	
	[Header("Settings")]
	[SerializeField] private string mainMenuSceneName = "MainMenu";

	private PlayerController player;


	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		IsPaused = false;

		if (pauseMenuRoot != null)
		{
			pauseMenuRoot.SetActive(false);
		}
	}

	

	private void Start()
	{
		player = FindAnyObjectByType<PlayerController>();
	}
	
	
	
	private void OnEnable()
	{
		if (pauseAction != null && pauseAction.action != null)
		{
			pauseAction.action.Enable();
			pauseAction.action.performed += OnPauseInputPerformed;
		}
	}


	private void OnDisable()
	{
		if (pauseAction != null && pauseAction.action != null)
		{
			pauseAction.action.performed -= OnPauseInputPerformed;
		}
	}


	private void OnDestroy()
	{
		if (Instance == this)
		{
			Time.timeScale = 1f;
			IsPaused = false;
			Instance = null;
		}
	}



	private void OnPauseInputPerformed(InputAction.CallbackContext context)
	{
		TogglePause();
	}



	public void TogglePause()
	{
		if (IsPaused)
		{
			Resume();
		}
		else
		{
			Pause();
		}
	}



	public void Pause()
	{
		if (IsPaused || !CanPauseGame()) return;

		IsPaused = true;
		Time.timeScale = 0f;

		if (pauseMenuRoot != null)
		{
			pauseMenuRoot.SetActive(true);
		}

		OnPauseStateChanged?.Invoke(true);
	}
	
	
	
	public void Resume()
	{
		if (!IsPaused) return;

		IsPaused = false;
		Time.timeScale = 1f;

		if (pauseMenuRoot != null)
		{
			pauseMenuRoot.SetActive(false);
		}

		OnPauseStateChanged?.Invoke(false);
	}
	
	
	
	public void RestartLevel()
	{
		IsPaused = false;
		Time.timeScale = 1f;
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}
	
	
	
	public void LoadMainMenu()
	{
		IsPaused = false;
		Time.timeScale = 1f;
		SceneManager.LoadScene(mainMenuSceneName);
	}



	private bool CanPauseGame()
	{
		if (player == null || player.StateMachine?.CurrentState == null) return true;
		
		var currentState = player.StateMachine.CurrentState;
		return currentState != player.DoorTransitionState && currentState != player.RespawnState;
	}



	public void OpenOptionsMenu()
	{
		optionsMenuRoot.SetActive(true);
	}
}
