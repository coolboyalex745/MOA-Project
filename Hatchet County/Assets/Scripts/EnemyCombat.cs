using UnityEngine;
using System.Collections;

/// <summary>
/// Hatchet County - EnemyAttacker
/// A stationary enemy that repeatedly attacks the player on a timer.
/// Exposes a parry window -- if the player blocks inside it, a parry is triggered.
///
/// Setup:
///   1. Add this component to your Enemy GameObject.
///   2. Assign playerCombat in the Inspector (drag your Player GO).
///   3. Optionally assign attackTelegraphVFX -- a simple particle / animation
///      that plays BEFORE the hit so the player gets a visual cue to block.
///   4. Assign hitPoint: a child Transform placed at the weapon tip / contact
///      position, used for VFX spawn.
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private Transform hitPoint;
    [SerializeField] private Animator animator;

    [Header("Attack Timing")]
    [Tooltip("Seconds between each attack attempt.")]
    [SerializeField] private float attackInterval = 2.5f;

    [Tooltip("How long before the hit lands the telegraph VFX plays (gives player time to react).")]
    [SerializeField] private float telegraphDelay = 0.6f;

    [Tooltip("How long after the hit starts the player can still parry.")]
    [SerializeField] private float parryWindow = 1f;

    [Header("Damage")]
    [SerializeField] private int attackDamage = 20;

    [Header("VFX")]
    [Tooltip("Optional: particle / animation that plays as a telegraph before the hit.")]
    [SerializeField] private ParticleSystem attackTelegraphVFX;

    // State
    private bool isParryWindowOpen = false;
    private bool isAttacking = false;

    // Unity lifecycle
    private void Start()
    {
        playerCombat = FindAnyObjectByType<PlayerCombat>();
        animator = GetComponent<Animator>();
        if (playerCombat == null)
        {
            Debug.LogError("[EnemyAttacker] No PlayerCombat assigned! Drag the Player GO into the Inspector.");
            return;
        }

        StartCoroutine(AttackLoop());
    }

    // Attack loop

    private IEnumerator AttackLoop()
    {
        // Small initial delay so the game does not open with an instant hit.
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            yield return StartCoroutine(PerformAttack());
            yield return new WaitForSeconds(attackInterval);
        }
    }

    private IEnumerator PerformAttack()
    {
        animator.SetBool("isDrawing", true);
        isAttacking = true;

        // 1. Telegraph -- show a visual cue so the player knows the attack is coming.
        if (attackTelegraphVFX != null)
            attackTelegraphVFX.Play();

        Debug.Log("[EnemyAttacker] Winding up...");
        yield return new WaitForSeconds(telegraphDelay);

 

        // 2. Open parry window and land hit.
      
        isParryWindowOpen = true;
        Debug.Log("[EnemyAttacker] ATTACK -- parry window OPEN");

        animator.SetBool("isDrawing", false);
        animator.SetBool("isAttacking", true);
        Vector3 contactPoint = hitPoint != null ? hitPoint.position : transform.position;
        playerCombat.ReceiveAttack(isParryWindow: true, attackDamage, contactPoint);

        // 3. Keep window open briefly, then close it.
        // The window stays open so a slightly late block still counts as a parry.
        yield return new WaitForSeconds(parryWindow);

        animator.SetBool("isAttacking", false);
        isParryWindowOpen = false;
        isAttacking = false;
        Debug.Log("[EnemyAttacker] Parry window CLOSED");
    }

    // Public query

    /// <summary>Can be read by other systems (e.g. UI indicator) to show attack warning.</summary>
    public bool IsInParryWindow => isParryWindowOpen;
    public bool IsAttacking => isAttacking;

    // Gizmos
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hitPoint == null) return;
        Gizmos.color = isParryWindowOpen ? Color.green : Color.red;
        Gizmos.DrawWireSphere(hitPoint.position, 0.15f);
    }
#endif
}