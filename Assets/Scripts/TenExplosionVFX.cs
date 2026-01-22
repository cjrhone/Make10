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
    [SerializeField] private float explosionRadius = 150f;
    [SerializeField] private float explosionDecay = 3f; // Exponential decay rate
    [SerializeField] private Vector2 forceMultiplierRange = new Vector2(0.7f, 1.3f); // Min/max force variation

    [Header("Small Particle Settings")]
    [SerializeField] private Vector2 smallSizeRange = new Vector2(20f, 28f);
    [SerializeField] private Color smallParticleColor = new Color(1f, 0.9f, 0.5f); // Gold
    [SerializeField] private float smallRotationSpeed = 150f;

    [Header("Big Particle Settings")]
    [SerializeField] private Vector2 bigSizeRange = new Vector2(40f, 52f);
    [SerializeField] private Color bigParticleColor = new Color(0.7f, 0.4f, 1f); // Purple
    [SerializeField] private float bigRotationSpeed = 75f;

    [Header("Collection Settings")]
    [SerializeField] private float collectionStaggerSmall = 0.03f;
    [SerializeField] private float collectionStaggerBig = 0.08f;
    [SerializeField] private float shrinkOnApproach = 0.6f; // Final scale when hitting target

    [Header("Bounce Settings")]
    [SerializeField] private float smallBounceSubtle = 1.04f;
    [SerializeField] private float smallBounceMedium = 1.06f;
    [SerializeField] private float smallBounceStrong = 1.08f;
    [SerializeField] private float bigBounceScale = 1.15f;
    [SerializeField] private float bounceDuration = 0.08f;

    // Particle data
    private class ExplosionParticle
    {
        public RectTransform transform;
        public Image image;
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

        // Store peak positions
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

        // Assign arrival times (staggered)
        float arrivalTime = 0f;
        int arrivalOrder = 0;

        // Small particles arrive first
        foreach (var particle in activeParticles)
        {
            if (!particle.isBig)
            {
                particle.arrivalTime = arrivalTime;
                particle.arrivalOrder = arrivalOrder++;
                arrivalTime += collectionStaggerSmall;
            }
        }

        // Big particles arrive after
        foreach (var particle in activeParticles)
        {
            if (particle.isBig)
            {
                particle.arrivalTime = arrivalTime;
                particle.arrivalOrder = arrivalOrder++;
                arrivalTime += collectionStaggerBig;
            }
        }
    }

    private void SpawnParticle(Vector2 origin, bool isBig, int index)
    {
        GameObject particleObj = new GameObject(isBig ? "BigParticle" : "SmallParticle");
        particleObj.transform.SetParent(particleContainer, false);

        RectTransform rt = particleObj.AddComponent<RectTransform>();
        rt.anchoredPosition = origin;

        // Random size within range
        Vector2 sizeRange = isBig ? bigSizeRange : smallSizeRange;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        rt.sizeDelta = new Vector2(size, size);

        // Diamond rotation (45 degrees)
        rt.localEulerAngles = new Vector3(0, 0, 45f);

        Image img = particleObj.AddComponent<Image>();
        img.color = isBig ? bigParticleColor : smallParticleColor;
        img.raycastTarget = false;

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

                // Rotate
                float rot = particle.transform.localEulerAngles.z;
                particle.transform.localEulerAngles = new Vector3(0, 0, rot + particle.rotationSpeed * Time.deltaTime);
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

                // Shrink as approaching
                float scale = Mathf.Lerp(1f, shrinkOnApproach, easedT);
                particle.transform.localScale = Vector3.one * scale;

                // Fade slightly
                float alpha = Mathf.Lerp(1f, 0.8f, easedT);
                particle.image.color = new Color(
                    particle.image.color.r,
                    particle.image.color.g,
                    particle.image.color.b,
                    alpha
                );

                // Rotate (slower as approaching)
                float rotSpeed = particle.rotationSpeed * (1f - easedT * 0.5f);
                float rot = particle.transform.localEulerAngles.z;
                particle.transform.localEulerAngles = new Vector3(0, 0, rot + rotSpeed * Time.deltaTime);

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
        // Hide particle
        if (particle.transform != null)
        {
            particle.transform.gameObject.SetActive(false);
        }

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
        }
        activeParticles.Clear();
    }

    private void OnDestroy()
    {
        CleanupParticles();
    }
}
