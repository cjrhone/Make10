using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages avatar image states based on game events.
/// Attach to a GameObject with a UI Image component.
/// </summary>
public class AvatarManager : MonoBehaviour {
  public static AvatarManager Instance { get; private set; }

  [Header("Avatar Images"), SerializeField] 
  private Sprite strugglingSprite; // Default - trying to solve

  [SerializeField] private Sprite solveSprite; // Ah-hah moment with lightbulb
  [SerializeField] private Sprite scribblingSprite; // Writing after solving
  [SerializeField] private Sprite hotStreakSprite; // On fire during hot streak

  [Header("Timing"), SerializeField]  private float solveDuration = 1f; // How long to show solve sprite
  [SerializeField] private float scribblingDuration = 10f; // How long before returning to struggling

  [Header("References"), SerializeField] 
  private Image avatarImage;

  [Header("Optional Effects"), SerializeField] 
  private GameObject lightbulbEffect; // Optional lightbulb popup

  [SerializeField] private GameObject fireEffect; // Optional fire particles

  [Header("Solve Animation Settings"), SerializeField] 
  private float solveBounceHeight = 20f;

  [SerializeField] private float solveBounceDuration = 0.3f;
  [SerializeField] private int solveBounceCount = 2;

  [Header("Hot Streak Shake Settings"), SerializeField] 
  private float shakeIntensity = 8f;

  [SerializeField] private float shakeSpeed = 50f;
  [SerializeField] private float shakeRotationAmount = 3f;

  [Header("Solve Particle Settings"), SerializeField] 
  private bool enableSolveParticles = true;

  [SerializeField] private float particleSpawnRate = 0.15f;
  [SerializeField] private int maxParticles = 8;
  [SerializeField] private float particleLifetime = 1.2f;
  [SerializeField] private float particleRiseSpeed = 40f;
  [SerializeField] private float particleDriftAmount = 20f;
  [SerializeField] private Vector2 particleSizeRange = new(6f, 12f);
  [SerializeField] private Color particleColorStart = new(1f, 0.95f, 0.5f, 0.8f); // Golden/yellow
  [SerializeField] private Color particleColorEnd = new(1f, 0.8f, 0.3f, 0f); // Fade to transparent

  // Current state tracking
  public enum AvatarState {
    Struggling,
    Solve,
    Scribbling,
    HotStreak
  }

  private AvatarState currentState = AvatarState.Struggling;
  private Coroutine stateTimerCoroutine;
  private bool isInHotStreak = false;

  // Particle system
  private List<SolveParticle> activeParticles = new();
  private Coroutine particleSpawnerCoroutine;
  private bool particlesActive = false;
  private RectTransform avatarRect;

  // Animation state
  private Vector2 originalPosition;
  private Coroutine bounceCoroutine;
  private Coroutine shakeCoroutine;
  private bool isShaking = false;

  private class SolveParticle {
    public RectTransform transform;
    public Image image;
    public float lifetime;
    public float maxLifetime;
    public float driftDirection;
    public float wobbleOffset;
    public float startX;
  }

  private void Awake() {
    // Singleton pattern
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }

    Instance = this;

    // Auto-find Image component if not assigned
    if (avatarImage == null) {
      avatarImage = GetComponent<Image>();
    }

    if (avatarImage != null) {
      avatarRect = avatarImage.GetComponent<RectTransform>();
      if (avatarRect != null) {
        originalPosition = avatarRect.anchoredPosition;
      }

      // Don't block raycasts — allows pause button behind avatar to receive taps
      avatarImage.raycastTarget = false;
    }
  }

  private void Start() {
    // Start in struggling state
    SetState(AvatarState.Struggling);

    // Hide optional effects
    if (lightbulbEffect != null) {
      lightbulbEffect.SetActive(false);
    }

    if (fireEffect != null) {
      fireEffect.SetActive(false);
    }
  }

  private void Update() {
    // Update active particles
    UpdateParticles();
  }

  /// <summary>
  /// Call this when the player successfully makes 10.
  /// Triggers: Solve (1s) → Scribbling (10s) → Struggling
  /// </summary>
  public void OnSolve() {
    // Don't interrupt hot streak
    if (isInHotStreak) {
      return;
    }

    SetState(AvatarState.Solve);
  }

  /// <summary>
  /// Call this when hot streak mode activates.
  /// </summary>
  public void OnHotStreakStart() {
    isInHotStreak = true;
    SetState(AvatarState.HotStreak);
  }

  /// <summary>
  /// Call this when hot streak mode ends.
  /// </summary>
  public void OnHotStreakEnd() {
    isInHotStreak = false;
    SetState(AvatarState.Struggling);
  }

  /// <summary>
  /// Force return to struggling state (e.g., on game reset).
  /// </summary>
  public void ResetToDefault() {
    isInHotStreak = false;
    StopAllTimers();
    StopShake();
    StopSolveParticles();

    // Stop bounce if running
    if (bounceCoroutine != null) {
      StopCoroutine(bounceCoroutine);
      bounceCoroutine = null;
    }

    SetState(AvatarState.Struggling);
  }

  private void SetState (AvatarState newState) {
    // Stop any existing timer
    StopAllTimers();

    currentState = newState;

    // Update sprite and effects based on state
    switch (newState) {
      case AvatarState.Struggling:
        SetSprite(strugglingSprite);
        SetLightbulb(false);
        SetFire(false);
        StopSolveParticles();
        StopShake();
        break;

      case AvatarState.Solve:
        SetSprite(solveSprite);
        SetLightbulb(true);
        SetFire(false);
        StartSolveParticles();
        // Auto-transition to Scribbling after delay
        stateTimerCoroutine = StartCoroutine(TransitionAfterDelay(AvatarState.Scribbling, solveDuration));
        // Play punch animation
        AnimateSolve();
        break;

      case AvatarState.Scribbling:
        SetSprite(scribblingSprite);
        SetLightbulb(false);
        SetFire(false);
        // Particles continue from Solve state (already active)
        // Auto-transition to Struggling after delay (unless hot streak starts)
        stateTimerCoroutine = StartCoroutine(TransitionAfterDelay(AvatarState.Struggling, scribblingDuration));
        break;

      case AvatarState.HotStreak:
        SetSprite(hotStreakSprite);
        SetLightbulb(false);
        SetFire(true);
        StopSolveParticles(); // Hot streak has its own fire effect
        StartShake(); // Violent energy shaking!
        // No auto-transition - stays until OnHotStreakEnd() is called
        break;
    }

    Debug.Log($"[AvatarManager] State changed to: {newState}");
  }

  private void SetSprite (Sprite sprite) {
    if (avatarImage != null && sprite != null) {
      avatarImage.sprite = sprite;
    }
  }

  private void SetLightbulb (bool active) {
    if (lightbulbEffect != null) {
      lightbulbEffect.SetActive(active);

      // Optional: animate lightbulb popup
      if (active) {
        AnimationUtilities.PopIn(lightbulbEffect.transform, 0.3f);
      }
    }
  }

  private void SetFire (bool active) {
    if (fireEffect != null) {
      fireEffect.SetActive(active);
    }
  }

  private void AnimateSolve() {
    if (avatarRect == null) {
      return;
    }

    // Stop any existing bounce
    if (bounceCoroutine != null) {
      StopCoroutine(bounceCoroutine);
    }

    bounceCoroutine = StartCoroutine(BounceAnimation());
  }

  private IEnumerator BounceAnimation() {
    // Quick bouncy "ah-hah!" effect
    var totalDuration = solveBounceDuration;
    var singleBounceDuration = totalDuration / solveBounceCount;

    for (var i = 0; i < solveBounceCount; i++) {
      var bounceHeight = solveBounceHeight * (1f - (float)i / solveBounceCount); // Decreasing height
      var elapsed = 0f;

      // Bounce up and down
      while (elapsed < singleBounceDuration) {
        elapsed += Time.deltaTime;
        var t = elapsed / singleBounceDuration;

        // Parabolic arc: up then down
        var arc = Mathf.Sin(t * Mathf.PI);
        var yOffset = arc * bounceHeight;

        // Squash and stretch
        var squash = 1f + arc * 0.15f; // Stretch when high
        var stretch = 1f - arc * 0.1f; // Squash when high (inverse for width)

        avatarRect.anchoredPosition = originalPosition + new Vector2(0, yOffset);
        avatarRect.localScale = new Vector3(stretch, squash, 1f);

        yield return null;
      }
    }

    // Reset to original
    avatarRect.anchoredPosition = originalPosition;
    avatarRect.localScale = Vector3.one;
    bounceCoroutine = null;
  }

  #region Hot Streak Shake

  private void StartShake() {
    if (avatarRect == null) {
      return;
    }

    isShaking = true;

    if (shakeCoroutine != null) {
      StopCoroutine(shakeCoroutine);
    }

    shakeCoroutine = StartCoroutine(ShakeLoop());
  }

  private void StopShake() {
    isShaking = false;

    if (shakeCoroutine != null) {
      StopCoroutine(shakeCoroutine);
      shakeCoroutine = null;
    }

    // Reset position and rotation
    if (avatarRect != null) {
      avatarRect.anchoredPosition = originalPosition;
      avatarRect.localEulerAngles = Vector3.zero;
      avatarRect.localScale = Vector3.one;
    }
  }

  private IEnumerator ShakeLoop() {
    var time = 0f;

    while (isShaking) {
      time += Time.deltaTime;

      // Rapid, chaotic shaking using multiple sine waves at different frequencies
      var shakeX = Mathf.Sin(time * shakeSpeed) * shakeIntensity;
      shakeX += Mathf.Sin(time * shakeSpeed * 1.7f) * shakeIntensity * 0.5f; // Add chaos

      var shakeY = Mathf.Cos(time * shakeSpeed * 1.3f) * shakeIntensity * 0.7f;
      shakeY += Mathf.Sin(time * shakeSpeed * 2.1f) * shakeIntensity * 0.3f; // More chaos

      // Rotation shake
      var rotZ = Mathf.Sin(time * shakeSpeed * 0.9f) * shakeRotationAmount;
      rotZ += Mathf.Cos(time * shakeSpeed * 1.4f) * shakeRotationAmount * 0.5f;

      // Subtle scale pulse for extra energy
      var scalePulse = 1f + Mathf.Sin(time * shakeSpeed * 0.5f) * 0.03f;

      avatarRect.anchoredPosition = originalPosition + new Vector2(shakeX, shakeY);
      avatarRect.localEulerAngles = new Vector3(0, 0, rotZ);
      avatarRect.localScale = Vector3.one * scalePulse;

      yield return null;
    }

    // Reset when done
    avatarRect.anchoredPosition = originalPosition;
    avatarRect.localEulerAngles = Vector3.zero;
    avatarRect.localScale = Vector3.one;
  }

  #endregion

  private IEnumerator TransitionAfterDelay (AvatarState nextState, float delay) {
    yield return new WaitForSeconds(delay);

    // Only transition if not in hot streak
    if (!isInHotStreak) {
      SetState(nextState);
    }
  }

  private void StopAllTimers() {
    if (stateTimerCoroutine != null) {
      StopCoroutine(stateTimerCoroutine);
      stateTimerCoroutine = null;
    }
  }

  #region Solve Particles

  private void StartSolveParticles() {
    if (!enableSolveParticles || avatarRect == null) {
      return;
    }

    particlesActive = true;

    if (particleSpawnerCoroutine != null) {
      StopCoroutine(particleSpawnerCoroutine);
    }

    particleSpawnerCoroutine = StartCoroutine(ParticleSpawnerLoop());
  }

  private void StopSolveParticles() {
    particlesActive = false;

    if (particleSpawnerCoroutine != null) {
      StopCoroutine(particleSpawnerCoroutine);
      particleSpawnerCoroutine = null;
    }

    // Let existing particles fade out naturally (don't destroy immediately)
  }

  private IEnumerator ParticleSpawnerLoop() {
    while (particlesActive) {
      if (activeParticles.Count < maxParticles) {
        SpawnParticle();
      }

      yield return new WaitForSeconds(particleSpawnRate);
    }
  }

  private void SpawnParticle() {
    if (avatarRect == null) {
      return;
    }

    var particleObj = new GameObject("SolveParticle");
    particleObj.transform.SetParent(avatarRect.parent, false);

    // Position behind avatar
    particleObj.transform.SetSiblingIndex(avatarRect.GetSiblingIndex());

    var rt = particleObj.AddComponent<RectTransform>();

    // Spawn around the avatar
    var avatarBounds = avatarRect.rect;
    var avatarPos = avatarRect.anchoredPosition;

    // Spawn from bottom half of avatar, spread horizontally
    var spawnX = avatarPos.x + Random.Range(-avatarBounds.width * 0.4f, avatarBounds.width * 0.4f);
    var spawnY = avatarPos.y + Random.Range(-avatarBounds.height * 0.3f, avatarBounds.height * 0.1f);

    rt.anchoredPosition = new Vector2(spawnX, spawnY);

    var size = Random.Range(particleSizeRange.x, particleSizeRange.y);
    rt.sizeDelta = new Vector2(size, size);

    // Diamond shape
    rt.localEulerAngles = new Vector3(0, 0, 45f);

    var img = particleObj.AddComponent<Image>();
    img.color = particleColorStart;
    img.raycastTarget = false;

    var particle = new SolveParticle {
      transform = rt,
      image = img,
      lifetime = particleLifetime * Random.Range(0.8f, 1.2f),
      maxLifetime = particleLifetime,
      driftDirection = Random.Range(-1f, 1f),
      wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
      startX = spawnX
    };

    activeParticles.Add(particle);
  }

  private void UpdateParticles() {
    var time = Time.time;

    for (var i = activeParticles.Count - 1; i >= 0; i--) {
      var particle = activeParticles[i];

      if (particle.transform == null) {
        activeParticles.RemoveAt(i);
        continue;
      }

      particle.lifetime -= Time.deltaTime;

      if (particle.lifetime <= 0) {
        Destroy(particle.transform.gameObject);
        activeParticles.RemoveAt(i);
        continue;
      }

      var lifePercent = particle.lifetime / particle.maxLifetime;

      // Rise upward
      var pos = particle.transform.anchoredPosition;
      pos.y += particleRiseSpeed * Time.deltaTime;

      // Gentle side-to-side wobble
      var wobble = Mathf.Sin(time * 3f + particle.wobbleOffset) * particleDriftAmount * 0.3f;
      pos.x = particle.startX + wobble + particle.driftDirection * particleDriftAmount * (1f - lifePercent);

      particle.transform.anchoredPosition = pos;

      // Scale: start small, grow slightly, then shrink
      var scaleT = 1f - lifePercent;
      float scale;
      if (scaleT < 0.2f) {
        scale = Mathf.Lerp(0.3f, 1f, scaleT / 0.2f); // Grow in
      }
      else {
        scale = Mathf.Lerp(1f, 0.5f, (scaleT - 0.2f) / 0.8f); // Shrink out
      }

      particle.transform.localScale = Vector3.one * scale;

      // Color fade
      particle.image.color = Color.Lerp(particleColorEnd, particleColorStart, lifePercent);

      // Gentle rotation
      var rot = particle.transform.localEulerAngles.z;
      particle.transform.localEulerAngles = new Vector3(0, 0, rot + 30f * Time.deltaTime);
    }
  }

  private void ClearAllParticles() {
    foreach (var particle in activeParticles) {
      if (particle.transform != null) {
        Destroy(particle.transform.gameObject);
      }
    }

    activeParticles.Clear();
  }

  #endregion

  private void OnDisable() {
    StopSolveParticles();
    StopShake();
    ClearAllParticles();
  }

  private void OnDestroy() {
    ClearAllParticles();
  }

  // Public getter for current state
  public AvatarState CurrentState => currentState;
}