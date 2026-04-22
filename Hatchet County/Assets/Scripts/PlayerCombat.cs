using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Hatchet County - PlayerCombat
/// Handles blocking, parry detection, taking damage, and feedback.
/// On a successful parry, a child GameObject is spawned at the contact point
/// and the clash VFX is played from it.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public Animator animator;

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

    [Header("Parry VFX")]
    [Tooltip("Prefab with a ParticleSystem on it. Spawned as a child at the contact point on parry.")]
    [SerializeField] private GameObject parryVFXPrefab;

    [Tooltip("How long before the spawned VFX child is destroyed. Match this to your particle duration.")]
    [SerializeField] private float parryVFXLifetime = 2f;

    // State
    public bool IsBlocking { get; private set; } = false;

    // Unity lifecycle
    private void Awake()
    {
        animator = GetComponent<Animator>();
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

    private void Update()
    {
        if (IsBlocking == false)
            animator.SetBool("isBlocking", false);
    }

    public void OnBlock(InputValue value)
    {
        IsBlocking = value.isPressed;
        animator.SetBool("isBlocking", true);
    }

    // Public API called by EnemyCombat

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
        // Blocking outside the parry window: blocked with no damage and no parry reward.
    }

    // Private helpers

    private void TriggerParrySuccess(Vector3 hitPoint)
    {
        Debug.Log("[PlayerCombat] PARRY!");

        // Sound
        if (audioSource != null && parrySFX != null)
            audioSource.PlayOneShot(parrySFX);

        // Spawn the clash VFX prefab as a child of the player at the contact point
        if (parryVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(parryVFXPrefab, hitPoint, Quaternion.identity, transform);

            ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();

            // Destroy the child after the effect finishes
            Destroy(vfxInstance, parryVFXLifetime);
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