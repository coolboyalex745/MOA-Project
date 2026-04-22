using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Hatchet County - PlayerCombat
/// Parry only succeeds if the player PRESSED block during the active parry window.
/// Holding block before the window opens does not count as a parry.
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

    // IsBlocking   : true while the block button is held
    // parryPressed : true only if block was pressed DURING an active parry window
    public bool IsBlocking { get; private set; } = false;
    private bool parryPressed = false;

    // EnemyCombat calls this to open and close the parry window
    private bool parryWindowOpen = false;

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

    // Input callback -- called by PlayerInput (Send Messages)
    public void OnBlock(InputValue value)
    {
        IsBlocking = value.isPressed;
        animator.SetBool("isBlocking", IsBlocking);

        // Only register a parry press if the window is currently open
        // and the player is pressing (not releasing) the button
        if (IsBlocking && parryWindowOpen)
        {
            parryPressed = true;
            Debug.Log("[PlayerCombat] Parry input registered inside window!");
        }
    }

    // Called by EnemyCombat to tell the player when the parry window opens and closes

    public void SetParryWindow(bool open)
    {
        parryWindowOpen = open;

        if (open)
        {
            // Reset every time a new window opens
            parryPressed = false;
            Debug.Log("[PlayerCombat] Parry window opened.");
        }
        else
        {
            Debug.Log("[PlayerCombat] Parry window closed.");
        }
    }

    // Public API called by EnemyCombat when the hitbox connects

    public void ReceiveAttack(bool isParryWindow, int damage, Vector3 hitPoint)
    {
        // A parry requires BOTH: the window must be open AND block was pressed during it
        bool validParry = isParryWindow && parryPressed;

        if (validParry)
        {
            TriggerParrySuccess(hitPoint);
            parryPressed = false;
        }
        else if (!IsBlocking)
        {
            TakeDamage(damage, hitPoint);
        }
        // Blocking but no valid parry: the attack is blocked with no damage, no parry reward.
    }

    // Private helpers

    private void TriggerParrySuccess(Vector3 hitPoint)
    {
        Debug.Log("[PlayerCombat] PARRY!");

        if (audioSource != null && parrySFX != null)
            audioSource.PlayOneShot(parrySFX);

        if (parryVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(parryVFXPrefab, hitPoint, Quaternion.identity, transform);

            ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();

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
        GUILayout.Label("Parry window: " + parryWindowOpen);
        GUILayout.Label("Parry pressed: " + parryPressed);
    }
#endif
}