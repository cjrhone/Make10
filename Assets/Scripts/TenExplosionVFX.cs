using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles the particle explosion and collection VFX when a "10" match is made.
/// Particles explode outward, then collect into the score progress slider.
/// </summary>
public class TenExplosionVFX : MonoBehaviour {
  public static TenExplosionVFX Instance { get; private set; }

  [Header("References"), SerializeField] private RectTransform particleContainer;

  [Header("Timing"), SerializeField] private float explosionDuration = 0.35f;
  [SerializeField] private float pauseDuration = 0.1f;
  [SerializeField] private float collectionDuration = 0.5f;

  [Header("Explosion Settings"), SerializeField]
  private float explosionRadius = 180f;

  [SerializeField] private float explosionDecay = 3f; // Exponential decay rate
  [SerializeField] private Vector2 forceMultiplierRange = new(0.7f, 1.3f); // Min/max force variation

  [Header("Small Particle Settings"), SerializeField]
  private Vector2 smallSizeRange = new(24f, 34f);

  [SerializeField] private Color smallParticleColor = new(1f, 0.95f, 0.65f); // Bright Gold
  [SerializeField] private float smallRotationSpeed = 150f;

  [Header("Big Particle Settings"), SerializeField]
  private Vector2 bigSizeRange = new(48f, 62f);

  [SerializeField] private Color bigParticleColor = new(0.85f, 0.55f, 1f); // Bright Purple
  [SerializeField] private float bigRotationSpeed = 75f;

  [Header("Collection Settings"), SerializeField]
  private float collectionStaggerSmall = 0.03f;

  [SerializeField] private float collectionStaggerBig = 0.08f;
  [SerializeField] private float arrivalRandomness = 0.15f; // Random variation in arrival time (0-1)
  [SerializeField] private float shrinkOnApproach = 0.6f; // Final scale when hitting target

  [Header("Bounce Settings"), SerializeField]
  private float smallBounceSubtle = 1.04f;

  [SerializeField] private float smallBounceMedium = 1.06f;
  [SerializeField] private float smallBounceStrong = 1.08f;
  [SerializeField] private float bigBounceScale = 1.15f;
  [SerializeField] private float bounceDuration = 0.08f;

  [Header("Glow Settings"), SerializeField]
  private float glowSizeMultiplier = 2.2f;

  [SerializeField] private float glowAlpha = 0.55f;

  [Header("Impact Flash Settings"), SerializeField]
  private float flashSize = 50f;

  [SerializeField] private float flashDuration = 0.18f;
  [SerializeField] private Color smallFlashColor = new(1f, 0.97f, 0.8f, 0.9f);
  [SerializeField] private Color bigFlashColor = new(0.9f, 0.7f, 1f, 0.95f);

  // Particle data
  private class ExplosionParticle {
    public RectTransform transform;
    public Image image;
    public RectTransform glowTransform;
    public Image glowImage;
    public bool isBig;
    public Vector2 velocity;
    public Vector2 startPosition;
    public Vector2 peakPosition;
    public float rotationSpeed;
    public float arrivalTime; // When this particle should arrive at target
    public int arrivalOrder; // For bounce intensity calculation
  }

  /// <summary>
  /// Per-explosion context so multiple explosions can run concurrently.
  /// </summary>
  private class ExplosionContext {
    public List<ExplosionParticle> particles = new();
    public int totalSmallArrived = 0;
  }

  // Track all particle GameObjects globally for OnDestroy cleanup
  private List<GameObject> allParticleObjects = new();

  private void Awake() {
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }

    Instance = this;

    // Auto-find particle container if not set
    if (particleContainer == null) {
      var containerTransform = transform.Find("ParticleContainer");
      if (containerTransform != null) {
        particleContainer = containerTransform.GetComponent<RectTransform>();
      }
      else
        // Use self as container if no child found
      {
        particleContainer = GetComponent<RectTransform>();
      }
    }
  }

  /// <summary>
  /// Trigger the explosion VFX at a world position with the current multiplier.
  /// Supports concurrent explosions — multiple can run simultaneously.
  /// </summary>
  /// <param name="worldPosition">Position in the grid container's local space</param>
  /// <param name="multiplier">Current score multiplier (affects particle count)</param>
  /// <param name="gridContainer">The container transform for proper positioning</param>
  public void TriggerExplosion (Vector2 worldPosition, float multiplier, RectTransform gridContainer) {
    // Allow concurrent explosions — don't cancel previous ones
    StartCoroutine(ExplosionSequence(worldPosition, multiplier, gridContainer));
  }

  private IEnumerator ExplosionSequence (Vector2 originPosition, float multiplier, RectTransform gridContainer) {
    // Each explosion gets its own context for concurrent support
    var ctx = new ExplosionContext();

    // Calculate particle counts
    var totalPoints = CalculateTotalPoints(multiplier);
    int smallCount, bigCount;
    CalculateParticleCounts(totalPoints, out smallCount, out bigCount);

    Debug.Log(
      $"[TenExplosionVFX] Multiplier: {multiplier:F2}, Points: {totalPoints}, Small: {smallCount}, Big: {bigCount}");

    // Get target position (score progress slider)
    var targetSlider = UIManager.Instance?.GetScoreProgressSlider();
    if (targetSlider == null) {
      Debug.LogWarning("[TenExplosionVFX] No score progress slider found!");
      yield break;
    }

    // Convert origin position to our container's space
    var localOrigin = ConvertPosition(originPosition, gridContainer, particleContainer);

    // Convert target position to our container's space
    var targetPosition = ConvertPosition(Vector2.zero, targetSlider, particleContainer);

    // Spawn all particles into this explosion's context
    SpawnParticles(ctx, localOrigin, smallCount, bigCount);

    // Phase 1: Explosion
    yield return StartCoroutine(ExplosionPhase(ctx));

    // Store peak positions (glow follows main particle, so just store once)
    foreach (var particle in ctx.particles) {
      if (particle.transform != null) {
        particle.peakPosition = particle.transform.anchoredPosition;
      }
    }

    // Phase 2: Brief pause
    yield return new WaitForSeconds(pauseDuration);

    // Phase 3: Collection
    yield return StartCoroutine(CollectionPhase(ctx, targetPosition, targetSlider));

    // Flush any remaining pending score (safety net)
    UIManager.Instance?.FlushPendingScore();

    // Cleanup this explosion's particles
    CleanupParticles(ctx);
  }

  private int CalculateTotalPoints (float multiplier) {
    var basePoints = 10;
    var multiplierBonus = Mathf.FloorToInt((multiplier - 1f) * 8f);
    return Mathf.Clamp(basePoints + multiplierBonus, 10, 50);
  }

  private void CalculateParticleCounts (int totalPoints, out int smallCount, out int bigCount) {
    if (totalPoints <= 14) {
      smallCount = totalPoints;
      bigCount = 0;
    }
    else {
      bigCount = Mathf.Min(Mathf.FloorToInt((totalPoints - 10) / 5f), 5);
      smallCount = totalPoints - bigCount * 5;
    }
  }

  private void SpawnParticles (ExplosionContext ctx, Vector2 origin, int smallCount, int bigCount) {
    // Spawn small particles
    for (var i = 0; i < smallCount; i++) {
      SpawnParticle(ctx, origin, false, i);
    }

    // Spawn big particles
    for (var i = 0; i < bigCount; i++) {
      SpawnParticle(ctx, origin, true, smallCount + i);
    }

    // Assign arrival times with randomness
    var baseArrivalTime = 0f;
    var arrivalOrder = 0;

    // Small particles - staggered with random variation
    foreach (var particle in ctx.particles) {
      if (!particle.isBig) {
        // Add random offset to arrival time
        var randomOffset = Random.Range(-arrivalRandomness, arrivalRandomness) * collectionDuration;
        particle.arrivalTime = Mathf.Max(0f, baseArrivalTime + randomOffset);
        particle.arrivalOrder = arrivalOrder++;
        baseArrivalTime += collectionStaggerSmall;
      }
    }

    // Big particles arrive after small ones, also with randomness
    baseArrivalTime += 0.1f; // Small gap before big particles start
    foreach (var particle in ctx.particles) {
      if (particle.isBig) {
        var randomOffset = Random.Range(-arrivalRandomness * 0.5f, arrivalRandomness) * collectionDuration;
        particle.arrivalTime = Mathf.Max(0f, baseArrivalTime + randomOffset);
        particle.arrivalOrder = arrivalOrder++;
        baseArrivalTime += collectionStaggerBig;
      }
    }
  }

  private void SpawnParticle (ExplosionContext ctx, Vector2 origin, bool isBig, int index) {
    var particleColor = isBig ? bigParticleColor : smallParticleColor;

    // Create glow first (behind particle)
    var glowObj = new GameObject(isBig ? "BigGlow" : "SmallGlow");
    glowObj.transform.SetParent(particleContainer, false);

    var glowRT = glowObj.AddComponent<RectTransform>();
    glowRT.anchoredPosition = origin;

    var sizeRange = isBig ? bigSizeRange : smallSizeRange;
    var size = Random.Range(sizeRange.x, sizeRange.y);
    var glowSize = size * glowSizeMultiplier;
    glowRT.sizeDelta = new Vector2(glowSize, glowSize);
    glowRT.localEulerAngles = new Vector3(0, 0, 45f);

    var glowImg = glowObj.AddComponent<Image>();
    glowImg.color = new Color(particleColor.r, particleColor.g, particleColor.b, glowAlpha);
    glowImg.raycastTarget = false;

    // Apply soft diamond glow texture (since particles are rotated 45 degrees)
    GlowTextureGenerator.ApplyDiamondGlow(glowImg, 64, 1.8f);

    // Track globally for OnDestroy cleanup
    allParticleObjects.Add(glowObj);

    // Create main particle (on top of glow)
    var particleObj = new GameObject(isBig ? "BigParticle" : "SmallParticle");
    particleObj.transform.SetParent(particleContainer, false);

    var rt = particleObj.AddComponent<RectTransform>();
    rt.anchoredPosition = origin;
    rt.sizeDelta = new Vector2(size, size);
    rt.localEulerAngles = new Vector3(0, 0, 45f);

    var img = particleObj.AddComponent<Image>();
    img.color = particleColor;
    img.raycastTarget = false;

    // Apply soft diamond glow texture for the main particle too (softer edges)
    GlowTextureGenerator.ApplyDiamondGlow(img, 64, 3f);

    // Track globally for OnDestroy cleanup
    allParticleObjects.Add(particleObj);

    // Random explosion direction using golden angle for even distribution
    var goldenAngle = 137.5f * Mathf.Deg2Rad;
    var angle = index * goldenAngle + Random.Range(-0.2f, 0.2f);

    // Vary the explosion force
    var force = explosionRadius * Random.Range(forceMultiplierRange.x, forceMultiplierRange.y);
    if (isBig) {
      force *= 0.8f; // Big particles don't go as far
    }

    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

    var particle = new ExplosionParticle {
      transform = rt,
      image = img,
      glowTransform = glowRT,
      glowImage = glowImg,
      isBig = isBig,
      velocity = direction * force / explosionDuration,
      startPosition = origin,
      rotationSpeed = (isBig ? bigRotationSpeed : smallRotationSpeed) * (Random.value > 0.5f ? 1f : -1f)
    };

    ctx.particles.Add(particle);
  }

  private IEnumerator ExplosionPhase (ExplosionContext ctx) {
    var elapsed = 0f;

    while (elapsed < explosionDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / explosionDuration;

      // Exponential decay for fast-to-slow motion
      var decayFactor = Mathf.Exp(-explosionDecay * t);

      foreach (var particle in ctx.particles) {
        if (particle.transform == null) {
          continue;
        }

        // Move outward with decay
        var movement = particle.velocity * decayFactor * Time.deltaTime;
        particle.transform.anchoredPosition += movement;

        // Move glow with particle
        if (particle.glowTransform != null) {
          particle.glowTransform.anchoredPosition = particle.transform.anchoredPosition;
        }

        // Rotate
        var rot = particle.transform.localEulerAngles.z;
        particle.transform.localEulerAngles = new Vector3(0, 0, rot + particle.rotationSpeed * Time.deltaTime);

        // Rotate glow slightly slower for a "trailing" effect
        if (particle.glowTransform != null) {
          var glowRot = particle.glowTransform.localEulerAngles.z;
          particle.glowTransform.localEulerAngles =
            new Vector3(0, 0, glowRot + particle.rotationSpeed * 0.7f * Time.deltaTime);
        }
      }

      yield return null;
    }
  }

  private IEnumerator CollectionPhase (ExplosionContext ctx, Vector2 targetPosition, RectTransform targetSlider) {
    // Calculate total collection time based on staggered arrivals
    var maxArrivalTime = 0f;
    foreach (var particle in ctx.particles) {
      if (particle.arrivalTime > maxArrivalTime) {
        maxArrivalTime = particle.arrivalTime;
      }
    }

    var totalTime = maxArrivalTime + collectionDuration;

    var elapsed = 0f;
    var arrivedParticles = new HashSet<ExplosionParticle>();

    while (elapsed < totalTime && ctx.particles.Count > 0) {
      elapsed += Time.deltaTime;

      foreach (var particle in ctx.particles) {
        if (particle.transform == null || arrivedParticles.Contains(particle)) {
          continue;
        }

        // Calculate this particle's progress (accounting for stagger)
        var particleElapsed = elapsed - particle.arrivalTime;
        if (particleElapsed < 0) {
          continue; // Not started yet
        }

        var t = Mathf.Clamp01(particleElapsed / collectionDuration);

        // Ease-in (slow to fast) using quadratic
        var easedT = t * t;

        // Move toward target with curved path
        var currentPos = Vector2.Lerp(particle.peakPosition, targetPosition, easedT);

        // Add a slight curve (perpendicular offset that peaks in middle)
        var curveAmount = Mathf.Sin(t * Mathf.PI) * 30f;
        var toTarget = (targetPosition - particle.peakPosition).normalized;
        var perpendicular = new Vector2(-toTarget.y, toTarget.x);
        currentPos += perpendicular * curveAmount * (particle.isBig ? 1.5f : 1f);

        particle.transform.anchoredPosition = currentPos;

        // Move glow with particle
        if (particle.glowTransform != null) {
          particle.glowTransform.anchoredPosition = currentPos;
        }

        // Shrink as approaching
        var scale = Mathf.Lerp(1f, shrinkOnApproach, easedT);
        particle.transform.localScale = Vector3.one * scale;

        // Shrink glow as well
        if (particle.glowTransform != null) {
          particle.glowTransform.localScale = Vector3.one * scale;
        }

        // Fade slightly
        var alpha = Mathf.Lerp(1f, 0.8f, easedT);
        particle.image.color = new Color(
          particle.image.color.r,
          particle.image.color.g,
          particle.image.color.b,
          alpha
        );

        // Fade glow as well
        if (particle.glowImage != null) {
          particle.glowImage.color = new Color(
            particle.glowImage.color.r,
            particle.glowImage.color.g,
            particle.glowImage.color.b,
            glowAlpha * alpha
          );
        }

        // Rotate (slower as approaching)
        var rotSpeed = particle.rotationSpeed * (1f - easedT * 0.5f);
        var rot = particle.transform.localEulerAngles.z;
        particle.transform.localEulerAngles = new Vector3(0, 0, rot + rotSpeed * Time.deltaTime);

        // Rotate glow
        if (particle.glowTransform != null) {
          var glowRot = particle.glowTransform.localEulerAngles.z;
          particle.glowTransform.localEulerAngles = new Vector3(0, 0, glowRot + rotSpeed * 0.7f * Time.deltaTime);
        }

        // Check if arrived
        if (t >= 1f) {
          arrivedParticles.Add(particle);
          OnParticleArrived(ctx, particle, targetSlider);
        }
      }

      yield return null;
    }
  }

  private void OnParticleArrived (ExplosionContext ctx, ExplosionParticle particle, RectTransform targetSlider) {
    // Hide particle and glow
    if (particle.transform != null) {
      particle.transform.gameObject.SetActive(false);
    }

    if (particle.glowTransform != null) {
      particle.glowTransform.gameObject.SetActive(false);
    }

    // Spawn impact flash at the target position
    var flashPos = ConvertPosition(Vector2.zero, targetSlider, particleContainer);
    StartCoroutine(SpawnImpactFlash(flashPos, particle.isBig));

    // Calculate bounce intensity and points
    float bounceScale;
    int pointsValue;

    if (particle.isBig) {
      bounceScale = bigBounceScale;
      pointsValue = 5; // Big particle = 5 points
      AudioManager.Instance?.PlayScoreTickBig();
    }
    else {
      ctx.totalSmallArrived++;
      pointsValue = 1; // Small particle = 1 point

      if (ctx.totalSmallArrived <= 5) {
        bounceScale = smallBounceSubtle;
      }
      else if (ctx.totalSmallArrived <= 15) {
        bounceScale = smallBounceMedium;
      }
      else {
        bounceScale = smallBounceStrong;
      }

      AudioManager.Instance?.PlayScoreTickSmall();
    }

    // Trigger bounce on progress bar
    UIManager.Instance?.BounceProgressBar(bounceScale, bounceDuration);

    // Update score display incrementally
    UIManager.Instance?.OnParticleScoreArrived(pointsValue);
  }

  private IEnumerator SpawnImpactFlash (Vector2 position, bool isBig) {
    var flashObj = new GameObject("ImpactFlash");
    flashObj.transform.SetParent(particleContainer, false);

    var rt = flashObj.AddComponent<RectTransform>();
    rt.anchoredPosition = position;

    var size = isBig ? flashSize * 1.5f : flashSize;
    rt.sizeDelta = new Vector2(size, size);
    // No rotation needed - using circular glow for flash
    rt.localEulerAngles = Vector3.zero;

    var img = flashObj.AddComponent<Image>();
    var flashColor = isBig ? bigFlashColor : smallFlashColor;
    img.color = flashColor;
    img.raycastTarget = false;

    // Apply soft circular glow for impact flash
    GlowTextureGenerator.ApplyCircularGlow(img, 64, 1.2f);

    // Animate flash: quick scale up, then fade out
    var elapsed = 0f;
    while (elapsed < flashDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / flashDuration;

      // Scale: pop up quickly, then hold
      var scale = t < 0.3f ? Mathf.Lerp(0.5f, 1.2f, t / 0.3f) : Mathf.Lerp(1.2f, 0.8f, (t - 0.3f) / 0.7f);
      rt.localScale = Vector3.one * scale;

      // Fade out
      var alpha = Mathf.Lerp(flashColor.a, 0f, t);
      img.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

      yield return null;
    }

    Destroy(flashObj);
  }

  private Vector2 ConvertPosition (Vector2 localPosition, RectTransform sourceRect, RectTransform targetRect) {
    // Convert from source's local space to world space, then to target's local space
    var worldPos = sourceRect.TransformPoint(localPosition);
    var localPos = targetRect.InverseTransformPoint(worldPos);
    return new Vector2(localPos.x, localPos.y);
  }

  /// <summary>
  /// Cleanup particles for a specific explosion context.
  /// </summary>
  private void CleanupParticles (ExplosionContext ctx) {
    foreach (var particle in ctx.particles) {
      if (particle.transform != null) {
        allParticleObjects.Remove(particle.transform.gameObject);
        Destroy(particle.transform.gameObject);
      }

      if (particle.glowTransform != null) {
        allParticleObjects.Remove(particle.glowTransform.gameObject);
        Destroy(particle.glowTransform.gameObject);
      }
    }

    ctx.particles.Clear();
  }

  /// <summary>
  /// Cleanup ALL remaining particles (safety net for scene teardown).
  /// </summary>
  private void CleanupAllParticles() {
    foreach (var obj in allParticleObjects) {
      if (obj != null) {
        Destroy(obj);
      }
    }

    allParticleObjects.Clear();
  }

  private void OnDestroy() {
    CleanupAllParticles();
  }
}