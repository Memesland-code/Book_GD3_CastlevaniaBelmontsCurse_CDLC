using UnityEngine;

public class PlayerSlideState : PlayerState
{
    private float elapsedTime;
    private float slideDirection;
    
    public PlayerSlideState(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }


    
    public override void Enter()
    {
        elapsedTime = 0f;
        
        // Lock direction depending on start input
        float inputX = player.Input.Move.x;
        slideDirection = Mathf.Abs(inputX) > 0.01f
            ? Mathf.Sign(inputX)
            : Mathf.Sign(player.transform.right.x);
        
        player.SetSlideHitbox(isSliding: true);
    }


    
    public override void HandleInput()
    {
        // Jump cancel: allowed only if nothing above blocking player
        if (player.Input.SlideJumpCancelAllowed && player.Input.JumpPressedThisFrame && player.CanStandUp())
        {
            player.ExecuteJump();
            stateMachine.ChangeState(player.AirState);
        }
    }


    public override void LogicUpdate()
    {
        // If sliding towards void and not blocked by a ceiling -> fall
        if (!player.IsGrounded && player.CanStandUp())
        {
            stateMachine.ChangeState(player.AirState);
            return;
        }

        float decelStartTime = player.SlideDuration * player.SlideConstantRatio;
        
        // Tunnel security: if reaching slowing phase under a ceiling, stop the timer to keep full speed until end of tunnel
        if (elapsedTime >= decelStartTime && !player.CanStandUp())
        {
            elapsedTime = decelStartTime;
        }
        else
        {
            elapsedTime += Time.deltaTime;
        }

        if (elapsedTime >= player.SlideDuration)
        {
            stateMachine.ChangeState(player.GroundedState);
        }
    }



    public override void PhysicsUpdate()
    {
        float normalizedTime = Mathf.Clamp01(elapsedTime / player.SlideDuration);
        float currentSpeed;

        if (normalizedTime < player.SlideConstantRatio)
        {
            // Phase 1 (0% -> x%): full slide speed
            currentSpeed = player.SlideSpeed;
        }
        else
        {
            // Phase 2 (x% -> 100%): linear slowing down
            float decelProgress = Mathf.InverseLerp(player.SlideConstantRatio, 1f, normalizedTime);
            currentSpeed = Mathf.Lerp(player.SlideSpeed, player.SlideEndSpeed, decelProgress);
        }
        
        // Force horizontal speed while keeping constant gravity on linearVelocity.y
        player.Rigidbody.linearVelocity = new Vector3(slideDirection * currentSpeed, player.Rigidbody.linearVelocity.y, 0f);
    }


    public override void Exit()
    {
        player.SetSlideHitbox(isSliding: false);
    }
}
