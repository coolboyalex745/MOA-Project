using UnityEngine;
using System.Collections;

/// <summary>
/// Hatchet County - EnemyCombat
/// Damage only happens when the weapon collider physically touches the player.
/// Notifies PlayerCombat when the parry window opens and closes.
///
/// Animation stages:
///   Idle      -- sword hidden, no bools set
///   isDrawing -- sword appears
///   isCharging -- enemy draws back, parry window opens (1 second)
///   isAttacking -- swing fires, hitbox active
/// Each bool is set to true BEFORE the previous is set to false so the
/// animator always has a true condition and never falls back to idle/hidden.
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private WeaponHitbox weaponHitbox;
    [SerializeField] private Animator animator;

    [Header("Attack Timing")]
    [Tooltip("Seconds between each attack attempt.")]
    [SerializeField] private float attackInterval = 2.5f;

    [Tooltip("Wind-up time for the draw animation before the charge starts.")]
    [SerializeField] private float telegraphDelay = 0.6f;

    [Tooltip("How long the parry window stays open during the charge (seconds before the hit).")]
    [SerializeField] private float parryWindowDuration = 1f;

    [Tooltip("How long the hitbox stays active during the swing.")]
    [SerializeField] private float attackActiveDuration = 0.4f;

    [Tooltip("How long after the hitbox closes the parry window stays open.")]
    [SerializeField] private float parryGracePeriod = 0.15f;

    [Header("Damage")]
    [SerializeField] private int attackDamage = 20;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swingSFX;

    [Header("VFX")]
    [SerializeField] private ParticleSystem attackTelegraphVFX;

    // State
    private bool isParryWindowOpen = false;
    private bool isAttacking = false;

    private void Start()
    {
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
            Debug.LogError("[EnemyCombat] No WeaponHitbox assigned.");
            return;
        }

        weaponHitbox.SetActive(false);
        weaponHitbox.OnPlayerHit += HandleWeaponHit;

        StartCoroutine(AttackLoop());
    }

    private void OnDestroy()
    {
        if (weaponHitbox != null)
            weaponHitbox.OnPlayerHit -= HandleWeaponHit;
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            yield return StartCoroutine(PerformAttack());
            yield return new WaitForSeconds(attackInterval);
        }
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;

        // Stage 1: Draw -- sword appears
        if (attackTelegraphVFX != null)
            attackTelegraphVFX.Play();

        animator.SetBool("isDrawing", true);
        Debug.Log("[EnemyCombat] Drawing...");

        yield return new WaitForSeconds(telegraphDelay);

        // Stage 2: Charge -- set isCharging TRUE before isDrawing FALSE
        // so the animator transitions draw -> charge without touching idle
        isParryWindowOpen = true;
        playerCombat.SetParryWindow(true);

        animator.SetBool("isCharging", true);
        animator.SetBool("isDrawing", false);
        Debug.Log("[EnemyCombat] Charging -- parry window OPEN");

        yield return new WaitForSeconds(parryWindowDuration);

        // Stage 3: Attack -- set isAttacking TRUE before isCharging FALSE
        // so the animator transitions charge -> attack without touching idle
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

        // Return to idle -- set isAttacking FALSE last
        animator.SetBool("isAttacking", false);
        isAttacking = false;
        Debug.Log("[EnemyCombat] Parry window CLOSED -- returning to idle");
    }

    private void HandleWeaponHit(Vector3 contactPoint)
    {
        if (swingSFX != null && audioSource != null)
            audioSource.PlayOneShot(swingSFX);

        playerCombat.ReceiveAttack(isParryWindowOpen, attackDamage, contactPoint);
    }

    public bool IsInParryWindow => isParryWindowOpen;
    public bool IsAttacking => isAttacking;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isParryWindowOpen ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
#endif
}