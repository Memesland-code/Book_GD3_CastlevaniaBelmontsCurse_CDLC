using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuScript : MonoBehaviour
{
	[SerializeField] private GameObject optionsMenu;
	
	public void OnClickStartLevelDesignDemo()
	{
		SceneManager.LoadScene("LD_Blocking_v2");
	}

	public void OnClickStartTechDemo()
	{
		SceneManager.LoadScene("Tech_FinalMap");
	}

	public void OnClickOptions()
	{
		optionsMenu.SetActive(true);
	}
	
	public void OnClickQuit()
	{
		Application.Quit();
	}
}
