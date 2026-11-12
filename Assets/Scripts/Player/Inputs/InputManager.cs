using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour, PlayerInputActions.IPlayerActions
{
	private PlayerInputActions inputActions;
	
    public Vector2 Move { get; private set; }
	public bool IsJumpHeld { get; private set; }
	public bool JumpPressedThisFrame { get; private set; }
	public bool SlidePressedThisFrame { get; private set; }

	public bool SlideJumpCancelAllowed => true;

	private void Awake()
	{
		inputActions = new PlayerInputActions();
		inputActions.Player.SetCallbacks(this);
	}

	private void OnEnable()
	{
		inputActions.Player.Enable();
	}

	private void OnDisable()
	{
		inputActions.Player.Disable();
	}

	private void LateUpdate()
	{
		JumpPressedThisFrame = false; // Consume jump action at end of frame
		SlidePressedThisFrame = false;
	}
	
	
	
	// ---- Callbacks from IPlayerActions ----

	
	
	public void OnMove(InputAction.CallbackContext context)
	{
		Move = context.ReadValue<Vector2>();
	}

	
	public void OnAttack(InputAction.CallbackContext context)
	{
		//
	}

	
	public void OnInteract(InputAction.CallbackContext context)
	{
		//
	}


	public void OnJump(InputAction.CallbackContext context)
	{
		if (context.started)
		{
			JumpPressedThisFrame = true;
			IsJumpHeld = true;
		}
		else if (context.canceled)
		{
			IsJumpHeld = false;
		}
	}
	
	public void OnSlide(InputAction.CallbackContext context)
	{
		if (context.started)
		{
			SlidePressedThisFrame = true;
		}
	}
}
