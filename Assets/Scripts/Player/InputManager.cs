using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
	private PlayerController playerController;
    public Vector2 move;
	public bool jump;

	private void Awake()
	{
		playerController = GetComponent<PlayerController>();
	}

	public void OnMove(InputValue value)
	{
		move = value.Get<Vector2>();
	}

	public void OnJump(InputValue value)
	{
		playerController.Jump();
	}
}
