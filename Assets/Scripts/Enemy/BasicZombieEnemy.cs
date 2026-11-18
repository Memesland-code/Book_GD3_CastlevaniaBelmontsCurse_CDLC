using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int amount);
}

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class BasicZombieEnemy : MonoBehaviour, IDamageable
{
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int DieHash = Animator.StringToHash("Die");
    
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float destroyDelayAfterDeath = 4f;

    [Header("Detection & Aggro")]
    [SerializeField] private float detectionRange = 9f;
    [SerializeField] private float loseAggroRange = 12f;
    [SerializeField] private float eyeHeightOffset = 1.2f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Movement & Melee")]
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private float meleeRange = 1.15f;
    [SerializeField] private float wallCheckDistance = 0.55f;
    
    private Rigidbody rb;
    private CapsuleCollider bodyCollider;
    private Transform targetPlayer;

    private int currentHealth;
    private int facingDirection = 1;
    private bool isAggro;
    private bool isDead;

    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        currentHealth = maxHealth;

        // Lock Z position and all rotations for clean 2.5D physics
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    
    
    private void Update()
    {
        if (isDead) return;

        UpdateTargetDetection();
    }

    
    
    private void FixedUpdate()
    {
        if (isDead) return;

        UpdateMovementAndCombat();
    }

    
    
    private void UpdateTargetDetection()
    {
        if (!isAggro)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, playerLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform candidate = hits[i].transform.root;
                if (HasLineOfSight(candidate))
                {
                    targetPlayer = candidate;
                    isAggro = true;
                    break;
                }
            }
        }
        else
        {
            if (targetPlayer == null)
            {
                LoseAggro();
                return;
            }

            float distance = Vector3.Distance(transform.position, targetPlayer.position);
            if (distance > loseAggroRange || !HasLineOfSight(targetPlayer))
            {
                LoseAggro();
            }
        }
    }

    
    
    private bool HasLineOfSight(Transform target)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeightOffset;
        Vector3 destination = target.position + Vector3.up * eyeHeightOffset;

        return !Physics.Linecast(origin, destination, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    
    
    private void LoseAggro()
    {
        isAggro = false;
        targetPlayer = null;
        SetAnimationStates(walking: false, attacking: false);
    }

    
    
    private void UpdateMovementAndCombat()
    {
        if (!isAggro || targetPlayer == null)
        {
            StopHorizontalMovement();
            SetAnimationStates(walking: false, attacking: false);
            return;
        }

        float deltaX = targetPlayer.position.x - transform.position.x;
        float absDeltaX = Mathf.Abs(deltaX);
        float verticalDiff = Mathf.Abs(targetPlayer.position.y - transform.position.y);

        // Turn towards player
        if (absDeltaX > 0.05f)
        {
            SetFacingDirection(deltaX > 0f ? 1 : -1);
        }

        // Melee range reached (same horizontal & vertical level)
        if (absDeltaX <= meleeRange && verticalDiff <= 1.5f)
        {
            StopHorizontalMovement();
            SetAnimationStates(walking: false, attacking: true);
            return;
        }

        // Stop pushing against walls while keeping gravity active so it can fall off ledges
        if (IsBlockedByWall())
        {
            StopHorizontalMovement();
            SetAnimationStates(walking: false, attacking: false);
            return;
        }

        rb.linearVelocity = new Vector3(facingDirection * moveSpeed, rb.linearVelocity.y, 0f);
        SetAnimationStates(walking: true, attacking: false);
    }

    
    
    private bool IsBlockedByWall()
    {
        Vector3 origin = transform.position + Vector3.up * (bodyCollider.height * 0.5f);
        return Physics.Raycast(origin, Vector3.right * facingDirection, wallCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    
    
    private void StopHorizontalMovement()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    
    
    private void SetFacingDirection(int dir)
    {
        if (facingDirection == dir) return;
        facingDirection = dir;

        if (visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.Euler(0f, facingDirection > 0 ? 90f : -90f, 0f);
        }
    }

    
    
    private void SetAnimationStates(bool walking, bool attacking)
    {
        if (animator == null) return;
        animator.SetBool(IsWalkingHash, walking);
        animator.SetBool(IsAttackingHash, attacking);
    }
    
    

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= Mathf.Max(1, amount);
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    
    
    private void Die()
    {
	    isDead = true;
	    isAggro = false;

	    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

	    if (targetPlayer != null && targetPlayer.TryGetComponent(out Collider playerCol))
	    {
		    Physics.IgnoreCollision(bodyCollider, playerCol, true);
	    }

	    if (animator != null)
	    {
		    animator.SetBool(IsWalkingHash, false);
		    animator.SetBool(IsAttackingHash, false);
		    animator.SetTrigger(DieHash);
	    }

	    Destroy(gameObject, destroyDelayAfterDeath);
    }
    
    

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, loseAggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
#endif
}