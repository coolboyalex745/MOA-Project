using UnityEngine;

/// <summary>
/// Hatchet County - EnemyCombat
/// Damage only happens when weapon collider physically touches the player.
/// Attack flow is now fully animation-driven (no timing-based cutoffs).
///
/// Animation stages:
///   Idle        -- no attack active
///   isAttacking -- controlled by FSM trigger + animation events
///
/// Animation events must call:
///   EnableHitbox  -> activates damage window
///   DisableHitbox -> ends damage window
///   EndAttack     -> resets attack state
/// </summary>
[RequireComponent(typeof(Boid))]
public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private WeaponHitbox weaponHitbox;
    [SerializeField] private Animator animator;

    [Header("Strike Range")]
    [SerializeField] private float strikeRange = 2f;
    [SerializeField] private float attackCooldown = 2.5f;

    [Header("Attack Timing")]
    [Tooltip("Legacy timing removed from control flow, kept for tuning if needed.")]
    [SerializeField] private float telegraphDelay = 0.6f;
    [SerializeField] private float parryWindowDuration = 1f;
    [SerializeField] private float attackActiveDuration = 0.4f;
    [SerializeField] private float parryGracePeriod = 0.15f;

    [Header("Attack Safety Net")]
    [Tooltip("If EndAttack() (an Animation Event) hasn't fired within this many seconds of TryAttack(), force-end the attack so the enemy doesn't get stuck forever. This is a fallback for a missing/broken Animation Event on the attack clip -- it is NOT a substitute for fixing the actual event. Set to 0 to disable.")]
    [SerializeField] private float maxAttackDuration = 3f;

    [Header("Stats")]
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swingSFX;

    [Header("VFX")]
    [SerializeField] private ParticleSystem attackTelegraphVFX;

    [Header("Gizmos")]
    [Tooltip("Vertical offset for editor gizmos. With a capsule collider the pivot is usually centered, but most humanoid model rigs have their root pivot at the feet -- this raises the gizmo spheres back up to roughly chest height.")]
    [SerializeField] private float gizmoHeightOffset = 1f;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float StrikeRange => strikeRange;
    public float AttackCooldown => attackCooldown;
    public float LastAttackTime => lastAttackTime;
    public bool IsAttacking => isAttacking;

    private bool isAttacking;
    private bool isParryWindowOpen;
    private float lastAttackTime;

    private void Start()
    {
        weaponHitbox = GetComponentInChildren<WeaponHitbox>();
        playerCombat = FindAnyObjectByType<PlayerCombat>();
        animator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (weaponHitbox == null)
        {
            Debug.LogError("[EnemyCombat] No WeaponHitbox found in children.");
            return;
        }

        weaponHitbox.SetActive(false);
        weaponHitbox.OnPlayerHit += HandleWeaponHit;
    }

    private void Update()
    {
        // SAFETY NET: if EndAttack() (an Animation Event) hasn't fired within
        // maxAttackDuration seconds of TryAttack(), something is wrong with the
        // attack clip's events -- force-close the attack so the enemy doesn't
        // get stuck in isAttacking=true forever. This should never trigger once
        // the clip's EndAttack event is actually wired up correctly.
        if (isAttacking && maxAttackDuration > 0f && Time.time >= lastAttackTime + maxAttackDuration)
        {
            Debug.LogWarning("[EnemyCombat] Attack safety-net triggered -- EndAttack() never fired " +
                              $"within {maxAttackDuration}s of TryAttack(). Check that the attack " +
                              "Animation Clip has an Animation Event calling EndAttack().");
            EndAttack();
        }
    }

    private void OnDestroy()
    {
        if (weaponHitbox != null)
            weaponHitbox.OnPlayerHit -= HandleWeaponHit;
    }

    public void TryAttack()
    {
        if (isAttacking) return;

        isAttacking = true;
        lastAttackTime = Time.time;

        animator.SetBool("isAttacking", true);
    }

    /// <summary>
    /// Animation Event: enables hit detection
    /// </summary>
    public void EnableHitbox()
    {
        weaponHitbox.SetActive(true);
    }

    /// <summary>
    /// Animation Event: disables hit detection
    /// </summary>
    public void DisableHitbox()
    {
        weaponHitbox.SetActive(false);
    }

    /// <summary>
    /// Animation Event: fully ends attack cycle
    /// </summary>
    public void EndAttack()
    {
        if (!isAttacking) return;

        weaponHitbox.SetActive(false);

        if (playerCombat != null)
            playerCombat.SetParryWindow(false);

        animator.SetBool("isAttacking", false);
        isAttacking = false;
    }

    /// <summary>
    /// Called by PlayerCombat the instant a parry against THIS enemy succeeds.
    /// Immediately interrupts the swing instead of waiting for the clip's
    /// EndAttack event (or the safety-net timeout) to close it out naturally --
    /// a successful parry should stagger the enemy right away, not eventually.
    /// </summary>
    public void InterruptAttack()
    {
        if (!isAttacking) return;

        Debug.Log("[EnemyCombat] Attack interrupted by successful parry.");

        // TODO: trigger a stagger/hitstun animation state here once you have one
        // (e.g. animator.SetTrigger("staggered")) instead of just snapping back
        // to Idle/Chasing.
        EndAttack();
    }

    public void TakeDamage(int amount)
    {
        EnemyFSM fsm = GetComponent<EnemyFSM>();

        int finalAmount = fsm != null
            ? Mathf.CeilToInt(amount * fsm.BlockDamageMultiplier)
            : amount;

        currentHealth = Mathf.Max(0, currentHealth - finalAmount);

        Debug.Log($"[EnemyCombat] Took {finalAmount} damage -- {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        Debug.Log("[EnemyCombat] Enemy died.");
        Destroy(gameObject);
    }

    private void HandleWeaponHit(Vector3 contactPoint)
    {
        if (swingSFX != null && audioSource != null)
            audioSource.PlayOneShot(swingSFX);

        // Pass 'this' through so PlayerCombat can call back into InterruptAttack()
        // on THIS specific enemy if the hit resolves as a successful parry.
        playerCombat.ReceiveAttack(this, isParryWindowOpen, attackDamage, contactPoint);
    }

    public void CloseParryWindow()
    {
        isParryWindowOpen = false;
        if (playerCombat != null)
            playerCombat.SetParryWindow(false);
    }
    public void OpenParryWindow()
    {
        isParryWindowOpen = true;
        if (playerCombat != null)
            playerCombat.SetParryWindow(true);
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * gizmoHeightOffset;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin, strikeRange);

        Gizmos.color = isParryWindowOpen ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, 0.2f);
    }
#endif
}