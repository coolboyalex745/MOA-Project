using UnityEngine;
using System;

/// <summary>
/// Hatchet County - PlayerWeaponHitbox
/// Player-side counterpart to EnemyCombat's WeaponHitbox. Attach this to the
/// player's weapon child GameObject that has a Trigger Collider.
/// It fires an event when it touches an Enemy, then disables itself so it
/// can only register one hit per swing.
///
/// Setup:
///   1. Add a Collider to this GameObject -- tick "Is Trigger".
///   2. Assign this component as the "weaponHitbox" reference on PlayerCombat.
///   3. Tag your enemies as "Enemy".
/// </summary>
public class PlayerWeaponHitbox : MonoBehaviour
{
    // PlayerCombat subscribes to this to receive hit events
    public event Action<Vector3, EnemyCombat> OnEnemyHit;
    private bool hitRegistered = false;

    public void SetActive(bool active)
    {
        // Reset hit flag each time the hitbox is turned on so it can land
        // exactly one hit per swing
        if (active)
            hitRegistered = false;

        // Enable/disable the collider rather than the whole GameObject
        // so this script keeps running
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitRegistered) return;
        if (!other.CompareTag("Enemy")) return;

        EnemyCombat enemyCombat = other.GetComponentInParent<EnemyCombat>();
        if (enemyCombat == null)
        {
            Debug.LogWarning("[PlayerWeaponHitbox] Hit something tagged 'Enemy' with no EnemyCombat component.");
            return;
        }

        hitRegistered = true;

        // Use the closest point on the enemy collider as the contact point for VFX
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        Debug.Log("[PlayerWeaponHitbox] Hit enemy at: " + contactPoint);

        OnEnemyHit?.Invoke(contactPoint, enemyCombat);

        // Disable the collider immediately so the same swing cannot hit twice
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }
}