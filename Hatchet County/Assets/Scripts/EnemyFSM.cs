using UnityEngine;

/// <summary>
/// Hatchet County - EnemyFSM
/// Finite state machine that drives the enemy's high-level behaviour.
/// EnemyCombat and Boid handle the specifics of attacking and movement;
/// EnemyFSM owns the state, decides transitions each frame, and sets the
/// animator parameter "fsmState" (int) so the animator can drive blends.
///
/// States:
///   Idle      -- outside detection range; Boid roams passively.
///   Chasing   -- player inside detection range but outside strike range;
///                Boid hunts, no combat action taken.
///   Attacking -- inside strike range and cooldown elapsed; EnemyCombat runs
///                the draw/charge/swing sequence. FSM stays here until the
///                sequence finishes before re-evaluating.
///   Blocking  -- player pressed attack AND the enemy is not already mid-swing;
///                enemy raises its guard for blockReactionTime seconds, reducing
///                incoming damage. Transitions back to Chasing when the window
///                closes or the block duration expires.
///
/// Blocking trigger:
///   PlayerCombat.AttackTriggered is read each frame. When it fires and the
///   FSM is in Idle or Chasing (not already committed to an attack), the enemy
///   has a blockChance probability of entering Blocking immediately.
/// </summary>
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(Boid))]
public class EnemyFSM : MonoBehaviour
{
    public enum EnemyState { Idle, Chasing, Attacking, Blocking }

    [Header("Thresholds")]
    [Tooltip("Must match Boid.detectionRange so the FSM and movement agree.")]
    [SerializeField] private float detectionRange = 12f;

    [Header("Blocking")]
    [Tooltip("0 = never blocks, 1 = always blocks when the player attacks.")]
    [SerializeField][Range(0f, 1f)] private float blockChance = 0.6f;
    [Tooltip("How long the enemy holds its block before returning to Chasing.")]
    [SerializeField] private float blockDuration = 1.2f;
    [Tooltip("Damage multiplier applied while blocking (0 = immune, 0.5 = half damage).")]
    [SerializeField] private float blockDamageMultiplier = 0.25f;

    [Header("Animator")]
    [Tooltip("Animator int parameter driven by the FSM. Map: 0=Idle 1=Chasing 2=Attacking 3=Blocking.")]
    [SerializeField] private string fsmStateParam = "fsmState";

    public EnemyState State { get; private set; } = EnemyState.Idle;
    public float BlockDamageMultiplier => State == EnemyState.Blocking ? blockDamageMultiplier : 1f;

    private EnemyCombat enemyCombat;
    private Boid boid;
    private Animator animator;
    private PlayerCombat playerCombat;

    private float blockTimer = 0f;
    private bool wasPlayerAttacking = false;

    private void Start()
    {
        enemyCombat = GetComponent<EnemyCombat>();
        boid = GetComponent<Boid>();
        animator = GetComponent<Animator>();
        playerCombat = FindAnyObjectByType<PlayerCombat>();

        if (playerCombat == null)
            Debug.LogError("[EnemyFSM] No PlayerCombat found in scene.");
    }

    private void Update()
    {
        if (playerCombat == null) return;

        CheckBlockTrigger();
        UpdateState();
        ApplyState();
        SyncAnimator();
    }

    private void CheckBlockTrigger()
    {
        bool playerAttackingNow = PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.AttackTriggered;
        bool attackStartedThisFrame = playerAttackingNow && !wasPlayerAttacking;
        wasPlayerAttacking = playerAttackingNow;

        if (!attackStartedThisFrame) return;
        if (State == EnemyState.Attacking) return;
        if (State == EnemyState.Blocking) return;

        float dist = Vector3.Distance(transform.position, playerCombat.transform.position);
        if (dist > detectionRange) return;

        if (Random.value <= blockChance)
            EnterBlocking();
    }

    private void UpdateState()
    {
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

        float distToPlayer = Vector3.Distance(transform.position, playerCombat.transform.position);

        if (distToPlayer > detectionRange)
        {
            SetState(EnemyState.Idle);
            return;
        }

        if (distToPlayer <= enemyCombat.StrikeRange &&
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
        Debug.Log("[EnemyFSM] Blocking!");
    }

    private void SetState(EnemyState next)
    {
        if (State == next) return;
        State = next;
        Debug.Log($"[EnemyFSM] --> {next}");
    }

    private void SyncAnimator()
    {
        if (animator == null) return;
        animator.SetInteger(fsmStateParam, (int)State);
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        if (screenPos.z > 0)
            GUI.Label(new Rect(screenPos.x - 40, Screen.height - screenPos.y, 120, 20),
                      $"FSM: {State}");
    }
#endif
}