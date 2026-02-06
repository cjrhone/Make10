using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles the particle explosion and collection VFX when a "10" match is made.
/// Particles explode outward, then collect into the score progress slider.
/// </summary>
public class TenExplosionVFX : MonoBehaviour
{
    public static TenExplosionVFX Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform particleContainer;

    [Header("Timing")]
    [SerializeField] private float explosionDuration = 0.35f;
    [SerializeField] private float pauseDuration = 0.1f;
    [SerializeField] private float collectionDuration = 0.5f;

    [Header("Explosion Settings")]
    [SerializeField] private float explosionRadius = 180f;
    [SerializeField] private float explosionDecay = 3f; // Exponential decay rate
    [SerializeField] private Vector2 forceMultiplierRange = new Vector2(0.7f, 1.3f); // Min/max force variation

    [Header("Small Particle Settings")]
    [SerializeField] private Vector2 smallSizeRange = new Vector2(24f, 34f);
    [SerializeField] private Color smallParticleColor = new Color(1f, 0.95f, 0.65f); // Bright Gold
    [SerializeField] private float smallRotationSpeed = 150f;

    [Header("Big Particle Settings")]
    [SerializeField] private Vector2 bigSizeRange = new Vector2(48f, 62f);
    [SerializeField] private Color bigParticleColor = new Color(0.85f, 0.55f, 1f); // Bright Purple
    [SerializeField] private float bigRotationSpeed = 75f;

    [Header("Collection Settings")]
    [SerializeField] private float collectionStaggerSmall = 0.03f;
    [SerializeField] private float collectionStaggerBig = 0.08f;
    [SerializeField] private float arrivalRandomness = 0.15f; // Random variation in arrival time (0-1)
    [SerializeField] private float shrinkOnApproach = 0.6f; // Final scale when hitting target

    [Header("Bounce Settings")]
    [SerializeField] private float smallBounceSubtle = 1.04f;
    [SerializeField] private float smallBounceMedium = 1.06f;
    [SerializeField] private float smallBounceStrong = 1.08f;
    [SerializeField] private float bigBounceScale = 1.15f;
    [SerializeField] private float bounceDuration = 0.08f;

    [Header("Glow Settings")]
    [SerializeField] private float glowSizeMultiplier = 2.2f;
    [SerializeField] private float glowAlpha = 0.55f;

    [Header("Impact Flash Settings")]
    [SerializeField] private float flashSize = 50f;
    [SerializeField] private float flashDuration = 0.18f;
    [SerializeField] private Color smallFlashColor = new Color(1f, 0.97f, 0.8f, 0.9f);
    [SerializeField] private Color bigFlashColor = new Color(0.9f, 0.7f, 1f, 0.95f);

    // Particle data
    private class ExplosionParticle
    {
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

    private List<ExplosionParticle> activeParticles = new List<ExplosionParticle>();
    private Coroutine currentVFXCoroutine;
    private int totalSmallArrived = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find particle container if not set
        if (particleContainer == null)
        {
            Transform containerTransform = transform.Find("ParticleContainer");
            if (containerTransform != null)
            {
                particleContainer = containerTransform.GetComponent<RectTransform>();
            }
            else
            {
                // Use self as container if no child found
                particleContainer = GetComponent<RectTransform>();
            }
        }
    }

    /// <summary>
    /// Trigger the explosion VFX at a world position with the current multiplier.
    /// </summary>
    /// <param name="worldPosition">Position in the grid container's local space</param>
    /// <param name="multiplier">Current score multiplier (affects particle count)</param>
    /// <param name="gridContainer">The container transform for proper positioning</param>
    public void TriggerExplosion(Vector2 worldPosition, float multiplier, RectTransform gridContainer)
    {
        if (currentVFXCoroutine != null)
        {
            StopCoroutine(currentVFXCoroutine);
            CleanupParticles();
        }

        currentVFXCoroutine = StartCoroutine(ExplosionSequence(worldPosition, multiplier, gridContainer));
    }

    private IEnumerator ExplosionSequence(Vector2 originPosition, float multiplier, RectTransform gridContainer)
    {
        // Calculate particle counts
        int totalPoints = CalculateTotalPoints(multiplier);
        int smallCount, bigCount;
        CalculateParticleCounts(totalPoints, out smallCount, out bigCount);

        Debug.Log($"[TenExplosionVFX] Multiplier: {multiplier:F2}, Points: {totalPoints}, Small: {smallCount}, Big: {bigCount}");

        // Get target position (score progress slider)
        RectTransform targetSlider = UIManager.Instance?.GetScoreProgressSlider();
        if (targetSlider == null)
        {
            Debug.LogWarning("[TenExplosionVFX] No score progress slider found!");
            yield break;
        }

        // Convert origin position to our container's space
        Vector2 localOrigin = ConvertPosition(originPosition, gridContainer, particleContainer);

        // Convert target position to our container's space
        Vector2 targetPosition = ConvertPosition(Vector2.zero, targetSlider, particleContainer);

        // Spawn all particles
        SpawnParticles(localOrigin, smallCount, bigCount);

        totalSmallArrived = 0;

        // Phase 1: Explosion
        yield return StartCoroutine(ExplosionPhase());

        // Store peak positions (glow follows main particle, so just store once)
        foreach (var particle in activeParticles)
        {
            if (particle.transform != null)
            {
                particle.peakPosition = particle.transform.anchoredPosition;
            }
        }

        // Phase 2: Brief pause
        yield return new WaitForSeconds(pauseDuration);

        // Phase 3: Collection
        yield return StartCoroutine(CollectionPhase(targetPosition, targetSlider));

        // Flush any remaining pending score (safety net)
        UIManager.Instance?.FlushPendingScore();

        // Cleanup
        CleanupParticles();
        currentVFXCoroutine = null;
    }

    private int CalculateTotalPoints(float multiplier)
    {
        int basePoints = 10;
        int multiplierBonus = Mathf.FloorToInt((multiplier - 1f) * 8f);
        return Mathf.Clamp(basePoints + multiplierBonus, 10, 50);
    }

    private void CalculateParticleCounts(int totalPoints, out int smallCount, out int bigCount)
    {
        if (totalPoints <= 14)
        {
            smallCount = totalPoints;
            bigCount = 0;
        }
        else
        {
            bigCount = Mathf.Min(Mathf.FloorToInt((totalPoints - 10) / 5f), 5);
            smallCount = totalPoints - (bigCount * 5);
        }
    }

    private void SpawnParticles(Vector2 origin, int smallCount, int bigCount)
    {
        activeParticles.Clear();

        // Spawn small particles
        for (int i = 0; i < smallCount; i++)
        {
            SpawnParticle(origin, false, i);
        }

        // Spawn big particles
        for (int i = 0; i < bigCount; i++)
        {
            SpawnParticle(origin, true, smallCount + i);
        }

        // Assign arrival times with randomness
        float baseArrivalTime = 0f;
        int arrivalOrder = 0;

        // Small particles - staggered with random variation
        foreach (var particle in activeParticles)
        {
            if (!particle.isBig)
            {
                // Add random offset to arrival time
                float randomOffset = Random.Range(-arrivalRandomness, arrivalRandomness) * collectionDuration;
                particle.arrivalTime = Mathf.Max(0f, baseArrivalTime + randomOffset);
                particle.arrivalOrder = arrivalOrder++;
                baseArrivalTime += collectionStaggerSmall;
            }
        }

        // Big particles arrive after small ones, also with randomness
        baseArrivalTime += 0.1f; // Small gap before big particles start
        foreach (var particle in activeParticles)
        {
            if (particle.isBig)
            {
                float randomOffset = Random.Range(-arrivalRandomness * 0.5f, arrivalRandomness) * collectionDuration;
                particle.arrivalTime = Mathf.Max(0f, baseArrivalTime + randomOffset);
                particle.arrivalOrder = arrivalOrder++;
                baseArrivalTime += collectionStaggerBig;
            }
        }
    }

    private void SpawnParticle(Vector2 origin, bool isBig, int index)
    {
        Color particleColor = isBig ? bigParticleColor : smallParticleColor;

        // Create glow first (behind particle)
        GameObject glowObj = new GameObject(isBig ? "BigGlow" : "SmallGlow");
        glowObj.transform.SetParent(particleContainer, false);

        RectTransform glowRT = glowObj.AddComponent<RectTransform>();
        glowRT.anchoredPosition = origin;

        Vector2 sizeRange = isBig ? bigSizeRange : smallSizeRange;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        float glowSize = size * glowSizeMultiplier;
        glowRT.sizeDelta = new Vector2(glowSize, glowSize);
        glowRT.localEulerAngles = new Vector3(0, 0, 45f);

        Image glowImg = glowObj.AddComponent<Image>();
        glowImg.color = new Color(particleColor.r, particleColor.g, particleColor.b, glowAlpha);
        glowImg.raycastTarget = false;

        // Apply soft diamond glow texture (since particles are rotated 45 degrees)
        GlowTextureGenerator.ApplyDiamondGlow(glowImg, 64, 1.8f);

        // Create main particle (on top of glow)
        GameObject particleObj = new GameObject(isBig ? "BigParticle" : "SmallParticle");
        particleObj.transform.SetParent(particleContainer, false);

        RectTransform rt = particleObj.AddComponent<RectTransform>();
        rt.anchoredPosition = origin;
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0, 0, 45f);

        Image img = particleObj.AddComponent<Image>();
        img.color = particleColor;
        img.raycastTarget = false;

        // Apply soft diamond glow texture for the main particle too (softer edges)
        GlowTextureGenerator.ApplyDiamondGlow(img, 64, 3f);

        // Random explosion direction using golden angle for even distribution
        float goldenAngle = 137.5f * Mathf.Deg2Rad;
        float angle = index * goldenAngle + Random.Range(-0.2f, 0.2f);

        // Vary the explosion force
        float force = explosionRadius * Random.Range(forceMultiplierRange.x, forceMultiplierRange.y);
        if (isBig) force *= 0.8f; // Big particles don't go as far

        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        ExplosionParticle particle = new ExplosionParticle
        {
            transform = rt,
            image = img,
            glowTransform = glowRT,
            glowImage = glowImg,
            isBig = isBig,
            velocity = direction * force / explosionDuration,
            startPosition = origin,
            rotationSpeed = (isBig ? bigRotationSpeed : smallRotationSpeed) * (Random.value > 0.5f ? 1f : -1f)
        };

        activeParticles.Add(particle);
    }

    private IEnumerator ExplosionPhase()
    {
        float elapsed = 0f;

        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / explosionDuration;

            // Exponential decay for fast-to-slow motion
            float decayFactor = Mathf.Exp(-explosionDecay * t);

            foreach (var particle in activeParticles)
            {
                if (particle.transform == null) continue;

                // Move outward with decay
                Vector2 movement = particle.velocity * decayFactor * Time.deltaTime;
                particle.transform.anchoredPosition += movement;

                // Move glow with particle
                if (particle.glowTransform != null)
                {
                    particle.glowTransform.anchoredPosition = particle.transform.anchoredPosition;
                }

                // Rotate
                float rot = particle.transform.localEulerAngles.z;
                particle.transform.localEulerAngles = new Vector3(0, 0, rot + particle.rotationSpeed * Time.deltaTime);

                // Rotate glow slightly slower for a "trailing" effect
                if (particle.glowTransform != null)
                {
                    float glowRot = particle.glowTransform.localEulerAngles.z;
                    particle.glowTransform.localEulerAngles = new Vector3(0, 0, glowRot + particle.rotationSpeed * 0.7f * Time.deltaTime);
                }
            }

            yield return null;
        }
    }

    private IEnumerator CollectionPhase(Vector2 targetPosition, RectTransform targetSlider)
    {
        // Calculate total collection time based on staggered arrivals
        float maxArrivalTime = 0f;
        foreach (var particle in activeParticles)
        {
            if (particle.arrivalTime > maxArrivalTime)
                maxArrivalTime = particle.arrivalTime;
        }
        float totalTime = maxArrivalTime + collectionDuration;

        float elapsed = 0f;
        HashSet<ExplosionParticle> arrivedParticles = new HashSet<ExplosionParticle>();

        while (elapsed < totalTime && activeParticles.Count > 0)
        {
            elapsed += Time.deltaTime;

            foreach (var particle in activeParticles)
            {
                if (particle.transform == null || arrivedParticles.Contains(particle)) continue;

                // Calculate this particle's progress (accounting for stagger)
                float particleElapsed = elapsed - particle.arrivalTime;
                if (particleElapsed < 0) continue; // Not started yet

                float t = Mathf.Clamp01(particleElapsed / collectionDuration);

                // Ease-in (slow to fast) using quadratic
                float easedT = t * t;

                // Move toward target with curved path
                Vector2 currentPos = Vector2.Lerp(particle.peakPosition, targetPosition, easedT);

                // Add a slight curve (perpendicular offset that peaks in middle)
                float curveAmount = Mathf.Sin(t * Mathf.PI) * 30f;
                Vector2 toTarget = (targetPosition - particle.peakPosition).normalized;
                Vector2 perpendicular = new Vector2(-toTarget.y, toTarget.x);
                currentPos += perpendicular * curveAmount * (particle.isBig ? 1.5f : 1f);

                particle.transform.anchoredPosition = currentPos;

                // Move glow with particle
                if (particle.glowTransform != null)
                {
                    particle.glowTransform.anchoredPosition = currentPos;
                }

                // Shrink as approaching
                float scale = Mathf.Lerp(1f, shrinkOnApproach, easedT);
                particle.transform.localScale = Vector3.one * scale;

                // Shrink glow as well
                if (particle.glowTransform != null)
                {
                    particle.glowTransform.localScale = Vector3.one * scale;
                }

                // Fade slightly
                float alpha = Mathf.Lerp(1f, 0.8f, easedT);
                particle.image.color = new Color(
                    particle.image.color.r,
                    particle.image.color.g,
                    particle.image.color.b,
                    alpha
                );

                // Fade glow as well
                if (particle.glowImage != null)
                {
                    particle.glowImage.color = new Color(
                        particle.glowImage.color.r,
                        particle.glowImage.color.g,
                        particle.glowImage.color.b,
                        glowAlpha * alpha
                    );
                }

                // Rotate (slower as approaching)
                float rotSpeed = particle.rotationSpeed * (1f - easedT * 0.5f);
                float rot = particle.transform.localEulerAngles.z;
                particle.transform.localEulerAngles = new Vector3(0, 0, rot + rotSpeed * Time.deltaTime);

                // Rotate glow
                if (particle.glowTransform != null)
                {
                    float glowRot = particle.glowTransform.localEulerAngles.z;
                    particle.glowTransform.localEulerAngles = new Vector3(0, 0, glowRot + rotSpeed * 0.7f * Time.deltaTime);
                }

                // Check if arrived
                if (t >= 1f)
                {
                    arrivedParticles.Add(particle);
                    OnParticleArrived(particle, targetSlider);
                }
            }

            yield return null;
        }
    }

    private void OnParticleArrived(ExplosionParticle particle, RectTransform targetSlider)
    {
        // Hide particle and glow
        if (particle.transform != null)
        {
            particle.transform.gameObject.SetActive(false);
        }
        if (particle.glowTransform != null)
        {
            particle.glowTransform.gameObject.SetActive(false);
        }

        // Spawn impact flash at the target position
        Vector2 flashPos = ConvertPosition(Vector2.zero, targetSlider, particleContainer);
        StartCoroutine(SpawnImpactFlash(flashPos, particle.isBig));

        // Calculate bounce intensity and points
        float bounceScale;
        int pointsValue;

        if (particle.isBig)
        {
            bounceScale = bigBounceScale;
            pointsValue = 5; // Big particle = 5 points
            AudioManager.Instance?.PlayScoreTickBig();
        }
        else
        {
            totalSmallArrived++;
            pointsValue = 1; // Small particle = 1 point

            if (totalSmallArrived <= 5)
                bounceScale = smallBounceSubtle;
            else if (totalSmallArrived <= 15)
                bounceScale = smallBounceMedium;
            else
                bounceScale = smallBounceStrong;

            AudioManager.Instance?.PlayScoreTickSmall();
        }

        // Trigger bounce on progress bar
        UIManager.Instance?.BounceProgressBar(bounceScale, bounceDuration);

        // Update score display incrementally
        UIManager.Instance?.OnParticleScoreArrived(pointsValue);
    }

    private IEnumerator SpawnImpactFlash(Vector2 position, bool isBig)
    {
        GameObject flashObj = new GameObject("ImpactFlash");
        flashObj.transform.SetParent(particleContainer, false);

        RectTransform rt = flashObj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;

        float size = isBig ? flashSize * 1.5f : flashSize;
        rt.sizeDelta = new Vector2(size, size);
        // No rotation needed - using circular glow for flash
        rt.localEulerAngles = Vector3.zero;

        Image img = flashObj.AddComponent<Image>();
        Color flashColor = isBig ? bigFlashColor : smallFlashColor;
        img.color = flashColor;
        img.raycastTarget = false;

        // Apply soft circular glow for impact flash
        GlowTextureGenerator.ApplyCircularGlow(img, 64, 1.2f);

        // Animate flash: quick scale up, then fade out
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;

            // Scale: pop up quickly, then hold
            float scale = t < 0.3f ? Mathf.Lerp(0.5f, 1.2f, t / 0.3f) : Mathf.Lerp(1.2f, 0.8f, (t - 0.3f) / 0.7f);
            rt.localScale = Vector3.one * scale;

            // Fade out
            float alpha = Mathf.Lerp(flashColor.a, 0f, t);
            img.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

            yield return null;
        }

        Destroy(flashObj);
    }

    private Vector2 ConvertPosition(Vector2 localPosition, RectTransform sourceRect, RectTransform targetRect)
    {
        // Convert from source's local space to world space, then to target's local space
        Vector3 worldPos = sourceRect.TransformPoint(localPosition);
        Vector3 localPos = targetRect.InverseTransformPoint(worldPos);
        return new Vector2(localPos.x, localPos.y);
    }

    private void CleanupParticles()
    {
        foreach (var particle in activeParticles)
        {
            if (particle.transform != null)
            {
                Destroy(particle.transform.gameObject);
            }
            if (particle.glowTransform != null)
            {
                Destroy(particle.glowTransform.gameObject);
            }
        }
        activeParticles.Clear();
    }

    private void OnDestroy()
    {
        CleanupParticles();
    }
}
