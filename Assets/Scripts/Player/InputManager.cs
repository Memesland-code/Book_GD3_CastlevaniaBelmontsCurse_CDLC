using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
	private PlayerController playerController;
	private LedgeGrab ledgeGrab;
	
    public Vector2 Move { get; private set; }
	public bool jump;

	private void Awake()
	{
		playerController = GetComponent<PlayerController>();
		ledgeGrab = GetComponent<LedgeGrab>();
	}

	public void OnMove(InputValue value)
	{
		Move = value.Get<Vector2>();
	}

	public void OnJump(InputValue value)
	{
		if (!value.isPressed) return;

		if (ledgeGrab != null && ledgeGrab.IsHanging)
		{
			ledgeGrab.OnJumpInput();
		}
		else if (playerController != null)
		{
			playerController.Jump();
		}
	}
}
