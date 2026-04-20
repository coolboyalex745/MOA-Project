using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using System;

/// <summary>
/// Hatchet County - PlayerCombat
/// Handles blocking, parry detection, taking damage, and feedback.
///
/// Setup:
///   1. Add this component to your Player GameObject.
///   2. Assign a PlayerInput component (Send Messages mode).
///   3. In your InputActionAsset, create:
///        - Action "Block"  (Button, Hold)
///        - Action "Attack" (Button, Press) -- optional for this prototype
///   4. Hook up references in the Inspector:
///        - cameraShakeTarget : your Main Camera transform
///        - vignetteImage     : a fullscreen UI Image (Canvas > Screen Space Overlay, alpha = 0)
///        - parrySFX          : metallic clang AudioClip
///        - hitSFX            : thud / grunt AudioClip
///        - parryVFX          : spark ParticleSystem prefab (or scene instance)
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Camera Shake")]
    [SerializeField] private Transform cameraShakeTarget;
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeMagnitude = 0.08f;

    [Header("Vignette (negative feedback)")]
    [SerializeField] private Image vignetteImage;
    [SerializeField] private float vignetteFadeDuration = 0.4f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip parrySFX;
    [SerializeField] private AudioClip hitSFX;

    [Header("VFX")]
    [SerializeField] private ParticleSystem parryVFX;

    // State
    public bool IsBlocking { get; private set; } = false;

    // Unity lifecycle
    private void Awake()
    {
        currentHealth = maxHealth;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = 0f;
            vignetteImage.color = c;
        }
    }
    // Called automatically by PlayerInput when action names match.

    public void OnBlock(InputValue value)
    {
        IsBlocking = value.isPressed;
    }

    // Public API called by EnemyAttacker

    /// <summary>
    /// Called by the enemy when its attack lands.
    /// isParryWindow : true if the attack is still inside the parry-timing window.
    /// damage        : how much HP to subtract on a failed block.
    /// hitPoint      : world position for VFX spawn.
    /// </summary>
    public void ReceiveAttack(bool isParryWindow, int damage, Vector3 hitPoint)
    {
        if (IsBlocking && isParryWindow)
        {
            TriggerParrySuccess(hitPoint);
        }
        else if (!IsBlocking)
        {
            TakeDamage(damage, hitPoint);
        }
        // Blocking outside the parry window: attack is blocked, no damage and no parry reward.
    }

    // Private helpers

    private void TriggerParrySuccess(Vector3 hitPoint)
    {
        Debug.Log("[PlayerCombat] PARRY!");

        if (audioSource != null && parrySFX != null)
            audioSource.PlayOneShot(parrySFX);

        if (parryVFX != null)
        {
            parryVFX.transform.position = hitPoint;
            parryVFX.Play();
        }
    }

    private void TakeDamage(int damage, Vector3 hitPoint)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log("[PlayerCombat] Hit! HP: " + currentHealth + "/" + maxHealth);

        if (audioSource != null && hitSFX != null)
            audioSource.PlayOneShot(hitSFX);

        StartCoroutine(CameraShake());
        StartCoroutine(FlashVignette());

        if (currentHealth <= 0)
            OnDeath();
    }

    private void OnDeath()
    {
        Debug.Log("[PlayerCombat] Player died.");
        // Hook up your game-over logic here.
    }

    // Coroutines

    private IEnumerator CameraShake()
    {
        if (cameraShakeTarget == null) yield break;

        Vector3 origin = cameraShakeTarget.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float strength = Mathf.Lerp(shakeMagnitude, 0f, elapsed / shakeDuration);
            cameraShakeTarget.localPosition = origin + (Vector3)UnityEngine.Random.insideUnitCircle * strength;
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraShakeTarget.localPosition = origin;
    }

    private IEnumerator FlashVignette()
    {
        if (vignetteImage == null) yield break;

        Color c = vignetteImage.color;
        c.a = 0.55f;
        vignetteImage.color = c;

        float elapsed = 0f;
        while (elapsed < vignetteFadeDuration)
        {
            c.a = Mathf.Lerp(0.55f, 0f, elapsed / vignetteFadeDuration);
            vignetteImage.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        c.a = 0f;
        vignetteImage.color = c;
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUILayout.Label("HP: " + currentHealth + "/" + maxHealth);
        GUILayout.Label("Blocking: " + IsBlocking);
    }
#endif
}