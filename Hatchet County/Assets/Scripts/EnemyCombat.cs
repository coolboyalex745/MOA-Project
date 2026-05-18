using UnityEngine;
using System.Collections;

/// <summary>
/// Hatchet County - EnemyCombat
/// Damage only happens when the weapon collider physically touches the player.
/// Notifies PlayerCombat when the parry window opens and closes.
/// Requires a Boid component for movement and an EnemyFSM component for state.
/// EnemyFSM calls TryAttack() when it enters the Attacking state; this class
/// does not start attacks on its own.
///
/// Animation stages:
///   Idle        -- sword hidden, no bools set
///   isDrawing   -- sword appears
///   isCharging  -- enemy draws back, parry window opens (parryWindowDuration)
///   isAttacking -- swing fires, hitbox active
/// Each bool is set to true BEFORE the previous is set to false so the
/// animator always has a true condition and never falls back to idle.
/// </summary>
[RequireComponent(typeof(Boid))]
public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private WeaponHitbox weaponHitbox;
    [SerializeField] private Animator animator;

    [Header("Strike Range")]
    [Tooltip("Distance to the player that triggers the attack sequence.")]
    [SerializeField] private float strikeRange = 2f;
    [Tooltip("Minimum seconds between two attacks.")]
    [SerializeField] private float attackCooldown = 2.5f;

    [Header("Attack Timing")]
    [Tooltip("Wind-up time for the draw animation before the charge starts.")]
    [SerializeField] private float telegraphDelay = 0.6f;
    [Tooltip("How long the parry window stays open during the charge.")]
    [SerializeField] private float parryWindowDuration = 1f;
    [Tooltip("How long the hitbox stays active during the swing.")]
    [SerializeField] private float attackActiveDuration = 0.4f;
    [Tooltip("How long after the hitbox closes the parry window stays open.")]
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

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float StrikeRange => strikeRange;
    public float AttackCooldown => attackCooldown;
    public float LastAttackTime => lastAttackTime;
    public bool IsInParryWindow => isParryWindowOpen;
    public bool IsAttacking => isAttacking;

    private bool isParryWindowOpen = false;
    private bool isAttacking = false;
    private float lastAttackTime = float.NegativeInfinity;
    private Boid boid;

    private void Start()
    {
        boid = GetComponent<Boid>();
        weaponHitbox = GetComponentInChildren<WeaponHitbox>();
        playerCombat = FindAnyObjectByType<PlayerCombat>();
        animator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (playerCombat == null)
        {
            Debug.LogError("[EnemyCombat] No PlayerCombat found in scene.");
            return;
        }

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

        lastAttackTime = Time.time;
        StartCoroutine(PerformAttack());
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;

        if (attackTelegraphVFX != null)
            attackTelegraphVFX.Play();

        animator.SetBool("isDrawing", true);
        Debug.Log("[EnemyCombat] Drawing...");

        yield return new WaitForSeconds(telegraphDelay);

        isParryWindowOpen = true;
        playerCombat.SetParryWindow(true);

        animator.SetBool("isCharging", true);
        animator.SetBool("isDrawing", false);
        Debug.Log("[EnemyCombat] Charging -- parry window OPEN");

        yield return new WaitForSeconds(parryWindowDuration);

        animator.SetBool("isAttacking", true);
        animator.SetBool("isCharging", false);

        weaponHitbox.SetActive(true);
        Debug.Log("[EnemyCombat] Attacking -- hitbox ON");

        yield return new WaitForSeconds(attackActiveDuration);

        weaponHitbox.SetActive(false);
        Debug.Log("[EnemyCombat] Hitbox OFF");

        yield return new WaitForSeconds(parryGracePeriod);

        isParryWindowOpen = false;
        playerCombat.SetParryWindow(false);

        animator.SetBool("isAttacking", false);
        isAttacking = false;
        Debug.Log("[EnemyCombat] Parry window CLOSED -- returning to idle");
    }

    public void TakeDamage(int amount)
    {
        EnemyFSM fsm = GetComponent<EnemyFSM>();
        int finalAmount = fsm != null
            ? Mathf.CeilToInt(amount * fsm.BlockDamageMultiplier)
            : amount;

        currentHealth = Mathf.Max(0, currentHealth - finalAmount);
        Debug.Log($"[EnemyCombat] Took {finalAmount} damage -- {currentHealth}/{maxHealth} HP");

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, strikeRange);

        Gizmos.color = isParryWindowOpen ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
#endif
}