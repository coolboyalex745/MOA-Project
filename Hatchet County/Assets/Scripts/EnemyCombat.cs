using UnityEngine;
using System.Collections;

/// <summary>
/// Hatchet County - EnemyCombat
/// Damage only happens when the weapon collider physically touches the player.
///
/// Setup:
///   1. Add this script to your Enemy root GameObject.
///   2. Create a child GameObject on the weapon (e.g. "WeaponHitbox").
///      - Add a Collider to it (Box or Capsule), tick "Is Trigger".
///      - Add the WeaponHitbox component (second script below) to that child.
///      - Assign this EnemyCombat as the "owner" on WeaponHitbox in the Inspector.
///   3. Tag your Player GameObject as "Player".
///   4. The hitbox collider is disabled by default and only enabled during the attack window.
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

    [Tooltip("Wind-up time before the hitbox becomes active.")]
    [SerializeField] private float telegraphDelay = 0.6f;

    [Tooltip("How long the hitbox stays active (the swing window).")]
    [SerializeField] private float attackActiveDuration = 0.4f;

    [Tooltip("How long after the hitbox closes the parry window stays open (reward slightly late blocks).")]
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
            Debug.LogError("[EnemyCombat] No WeaponHitbox assigned. Create a child GO with a trigger collider and WeaponHitbox script.");
            return;
        }

        // Make sure hitbox starts disabled
        weaponHitbox.SetActive(false);
        weaponHitbox.OnPlayerHit += HandleWeaponHit;

        StartCoroutine(AttackLoop());
    }

    private void OnDestroy()
    {
        if (weaponHitbox != null)
            weaponHitbox.OnPlayerHit -= HandleWeaponHit;
    }

    // Attack loop

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

        // 1. Telegraph wind-up
        if (attackTelegraphVFX != null)
            attackTelegraphVFX.Play();

        animator.SetBool("isDrawing", true);
        Debug.Log("[EnemyCombat] Winding up...");

        yield return new WaitForSeconds(telegraphDelay);

        // 2. Open parry window and enable the hitbox
        isParryWindowOpen = true;
        animator.SetBool("isDrawing", false);
        animator.SetBool("isAttacking", true);

        if (swingSFX != null && audioSource != null)
            audioSource.PlayOneShot(swingSFX);

        weaponHitbox.SetActive(true);
        Debug.Log("[EnemyCombat] Hitbox ON -- parry window OPEN");

        // 3. Hitbox stays active for the swing duration
        yield return new WaitForSeconds(attackActiveDuration);

        weaponHitbox.SetActive(false);
        Debug.Log("[EnemyCombat] Hitbox OFF");

        // 4. Short grace period: parry window stays open a little after the swing
        //    so a slightly late block on the last frame still rewards a parry.
        yield return new WaitForSeconds(parryGracePeriod);

        isParryWindowOpen = false;
        isAttacking = false;
        animator.SetBool("isAttacking", false);
        Debug.Log("[EnemyCombat] Parry window CLOSED");
    }

    // Called by WeaponHitbox when the trigger collider touches the player

    private void HandleWeaponHit(Vector3 contactPoint)
    {
        playerCombat.ReceiveAttack(isParryWindowOpen, attackDamage, contactPoint);
    }

    // Public query
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