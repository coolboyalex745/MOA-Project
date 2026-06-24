using UnityEngine;

/// <summary>
/// Hatchet County - EnemyFSM
/// Finite state machine that drives the enemy's high-level behaviour.
/// EnemyCombat and Boid handle the specifics of attacking and movement;
/// FSM owns the state, decides transitions each frame, and drives the animator.
///
/// Animator parameters used:
///   isAttacking (bool) -- controlled by EnemyCombat via animation events
///   isBlocking  (bool) -- set by EnemyFSM when entering/leaving Blocking state
///
/// States:
///   Idle      -- outside detection range; Boid roams passively.
///   Chasing   -- player inside detection range but outside strike range.
///   Attacking  -- attack animation is playing (driven by EnemyCombat)
///   Blocking  -- reactive guard state based on player attack event
///
/// Blocking trigger:
///   PlayerCombat.OnAttackStarted is event-driven.
///   If enemy is not committed to attack, it may enter Blocking.
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(Boid))]
public class EnemyFSM : MonoBehaviour
{
    public enum EnemyState { Idle, Chasing, Attacking, Blocking }

    [Header("Thresholds")]
    [SerializeField] private float detectionRange = 12f;

    [Header("Blocking")]
    [SerializeField][Range(0f, 1f)] private float blockChance = 0.6f;
    [SerializeField] private float blockDuration = 1.2f;
    [SerializeField] private float blockDamageMultiplier = 0.25f;

    [Header("Animator")]
    [SerializeField] private string isBlockingParam = "isBlocking";

    public EnemyState State { get; private set; } = EnemyState.Idle;
    public float BlockDamageMultiplier => State == EnemyState.Blocking ? blockDamageMultiplier : 1f;

    private EnemyCombat enemyCombat;
    private Animator animator;
    private PlayerCombat playerCombat;

    private float blockTimer;

    private void Start()
    {
        enemyCombat = GetComponent<EnemyCombat>();
        animator = GetComponent<Animator>();
        playerCombat = FindAnyObjectByType<PlayerCombat>();

        if (playerCombat != null)
            playerCombat.OnAttackStarted += HandlePlayerAttackStarted;
    }

    private void OnDestroy()
    {
        if (playerCombat != null)
            playerCombat.OnAttackStarted -= HandlePlayerAttackStarted;
    }

    private void Update()
    {
        if (playerCombat == null) return;

        UpdateState();
        ApplyState();
        SyncAnimator();
    }
    private void FacePlayer()
    {
        if (playerCombat == null) return;

        Vector3 direction = playerCombat.transform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = targetRotation;
    }

    private void HandlePlayerAttackStarted()
    {
        if (State == EnemyState.Attacking) return;
        if (State == EnemyState.Blocking) return;

        float dist = Vector3.Distance(transform.position, playerCombat.transform.position);
        if (dist > detectionRange) return;

        if (Random.value <= blockChance)
            EnterBlocking();
    }

    private void UpdateState()
    {
        // Only force-face the player while stationary (mid-attack or blocking).
        // During Chasing/Idle, Boid.VisualRotation already smoothly rotates the
        // model to match movement velocity -- calling FacePlayer() here too
        // caused both scripts to fight over transform.rotation every frame,
        // which is what produced the jumpy/flickery rotation.
        if (State == EnemyState.Attacking || State == EnemyState.Blocking)
            FacePlayer();

        if (State == EnemyState.Blocking)
        {
            blockTimer -= Time.deltaTime;
            if (blockTimer <= 0f)
                SetState(EnemyState.Chasing);
            return;
        }

        if (State == EnemyState.Attacking)
        {
            if (!enemyCombat.IsAttacking)
                SetState(EnemyState.Chasing);

            return;
        }

        float dist = Vector3.Distance(transform.position, playerCombat.transform.position);

        if (dist > detectionRange)
        {
            SetState(EnemyState.Idle);
            return;
        }

        if (dist <= enemyCombat.StrikeRange &&
            Time.time >= enemyCombat.LastAttackTime + enemyCombat.AttackCooldown)
        {
            SetState(EnemyState.Attacking);
            return;
        }

        SetState(EnemyState.Chasing);
    }

    private void ApplyState()
    {
        switch (State)
        {
            case EnemyState.Idle:
                break;

            case EnemyState.Chasing:
                break;

            case EnemyState.Attacking:
                enemyCombat.TryAttack();
                break;

            case EnemyState.Blocking:
                break;
        }
    }

    private void EnterBlocking()
    {
        SetState(EnemyState.Blocking);
        blockTimer = blockDuration;
    }

    private void SetState(EnemyState next)
    {
        if (State == next) return;
        State = next;
    }

    private void SyncAnimator()
    {
        if (animator == null) return;
        animator.SetBool(isBlockingParam, State == EnemyState.Blocking);
    }
}