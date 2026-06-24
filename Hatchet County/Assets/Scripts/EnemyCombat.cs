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

        playerCombat.ReceiveAttack(isParryWindowOpen, attackDamage, contactPoint);
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