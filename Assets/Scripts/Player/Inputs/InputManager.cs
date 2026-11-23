using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-5)]
public class InputManager : MonoBehaviour, PlayerInputActions.IPlayerActions
{
	private PlayerInputActions inputActions;
	
    public Vector2 Move { get; private set; }
	public bool IsJumpHeld { get; private set; }
	public bool JumpPressedThisFrame { get; private set; }
	public bool SlidePressedThisFrame { get; private set; }
	
	public bool IsWhipHeld { get; private set; }
	public bool WhipPressedThisFrame { get; private set; }
	public bool AttackPressedThisFrame { get; private set; }

	public bool SlideJumpCancelAllowed => true;
	
	public bool IsLightHaloHeld { get; private set; }

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

	// Consume inputs
	private void LateUpdate()
	{
		JumpPressedThisFrame = false;
		SlidePressedThisFrame = false;
		WhipPressedThisFrame = false;
		AttackPressedThisFrame = false;
	}
	
	
	
	//* ---- Callbacks from IPlayerActions ----

	
	
	public void OnMove(InputAction.CallbackContext context)
	{
		Move = context.ReadValue<Vector2>();
	}

	
	public void OnAttack(InputAction.CallbackContext context)
	{
		if (context.started)
		{
			AttackPressedThisFrame = true;
		}
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

	public void OnWhip(InputAction.CallbackContext context)
	{
		if (context.started)
		{
			WhipPressedThisFrame = true;
			IsWhipHeld = true;
		}
		else if (context.canceled)
		{
			IsWhipHeld = false;
		}
	}

	public void OnLightHalo(InputAction.CallbackContext context)
	{
		if (context.started || context.performed)
		{
			IsLightHaloHeld = true;
		}
		else if (context.canceled)
		{
			IsLightHaloHeld = false;
		}
	}

	public void OnPauseMenu(InputAction.CallbackContext context)
	{
		return;
	}
}
