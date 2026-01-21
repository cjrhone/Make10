using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Adds dynamic VFX to a loading bar using procedural math-based effects.
/// Features: particle trail, shimmer waves, glow pulse, color shifting, completion burst.
/// </summary>
public class LoadingBarVFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform fillArea;

    [Header("Particle Trail Settings")]
    [SerializeField] private int particleCount = 8;
    [SerializeField] private float particleSize = 12f;
    [SerializeField] private float particleTrailLength = 30f;
    [SerializeField] private Color particleColor = new Color(1f, 1f, 0.8f, 0.9f);

    [Header("Shimmer Wave Settings")]
    [SerializeField] private float shimmerSpeed = 3f;
    [SerializeField] private float shimmerIntensity = 0.3f;
    [SerializeField] private int shimmerWaveCount = 2;

    [Header("Glow Pulse Settings")]
    [SerializeField] private float glowPulseSpeed = 2f;
    [SerializeField] private float glowMinIntensity = 0.5f;
    [SerializeField] private float glowMaxIntensity = 1.2f;

    [Header("Color Gradient")]
    [SerializeField] private Color startColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color midColor = new Color(0.4f, 0.9f, 0.5f);
    [SerializeField] private Color endColor = new Color(1f, 0.85f, 0.2f);

    [Header("Completion Burst")]
    [SerializeField] private int burstParticleCount = 20;
    [SerializeField] private float burstSpeed = 300f;
    [SerializeField] private float burstDuration = 0.6f;

    // Internal state
    private List<RectTransform> trailParticles = new List<RectTransform>();
    private List<Image> trailImages = new List<Image>();
    private List<RectTransform> shimmerBars = new List<RectTransform>();
    private Image glowOverlay;
    private float lastProgress = 0f;
    private bool isComplete = false;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Auto-find references if not set
        if (progressSlider == null)
            progressSlider = GetComponent<Slider>();
        if (fillImage == null && progressSlider != null)
            fillImage = progressSlider.fillRect?.GetComponent<Image>();
        if (fillArea == null && progressSlider != null)
            fillArea = progressSlider.fillRect;
    }

    private void Start()
    {
        CreateTrailParticles();
        CreateShimmerBars();
        CreateGlowOverlay();
    }

    private void Update()
    {
        if (progressSlider == null || isComplete) return;

        float progress = progressSlider.value;

        UpdateTrailParticles(progress);
        UpdateShimmerEffect(progress);
        UpdateGlowPulse(progress);
        UpdateFillColor(progress);

        // Check for completion
        if (progress >= 0.99f && !isComplete)
        {
            isComplete = true;
            StartCoroutine(CompletionBurst());
        }

        lastProgress = progress;
    }

    #region Particle Trail

    private void CreateTrailParticles()
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject($"TrailParticle_{i}");
            particle.transform.SetParent(transform, false);

            RectTransform rt = particle.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(particleSize, particleSize);
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = particle.AddComponent<Image>();
            img.color = particleColor;
            img.raycastTarget = false;

            trailParticles.Add(rt);
            trailImages.Add(img);
        }
    }

    private void UpdateTrailParticles(float progress)
    {
        if (fillArea == null) return;

        float barWidth = rectTransform.rect.width;
        float leadingEdge = progress * barWidth;

        for (int i = 0; i < trailParticles.Count; i++)
        {
            RectTransform rt = trailParticles[i];
            Image img = trailImages[i];

            // Calculate particle position with trail effect
            float trailOffset = (i / (float)particleCount) * particleTrailLength;
            float particleX = leadingEdge - trailOffset;

            // Sinusoidal vertical movement - each particle has different phase
            float phase = i * 0.7f + Time.time * 4f;
            float waveAmplitude = 8f * (1f - (i / (float)particleCount)); // Decreasing amplitude
            float particleY = Mathf.Sin(phase) * waveAmplitude;

            // Spiral/helix pattern using Lissajous curves
            float lissajousX = Mathf.Sin(Time.time * 3f + i * 0.5f) * 3f;
            float lissajousY = Mathf.Cos(Time.time * 2f + i * 0.8f) * 5f;

            rt.anchoredPosition = new Vector2(
                Mathf.Max(0, particleX) + lissajousX,
                particleY + lissajousY
            );

            // Fade based on position in trail
            float fadeT = i / (float)particleCount;
            float alpha = Mathf.Lerp(0.9f, 0.1f, fadeT);

            // Pulse alpha
            alpha *= 0.7f + 0.3f * Mathf.Sin(Time.time * 8f + i);

            // Hide if behind the bar start
            if (particleX < 0)
                alpha = 0;

            img.color = new Color(particleColor.r, particleColor.g, particleColor.b, alpha);

            // Scale based on position - leading particles bigger
            float scale = Mathf.Lerp(1.2f, 0.4f, fadeT);
            scale *= 0.8f + 0.2f * Mathf.Sin(Time.time * 6f + i * 0.5f);
            rt.localScale = Vector3.one * scale;

            // Rotation for sparkle effect
            rt.localEulerAngles = new Vector3(0, 0, Time.time * 180f + i * 45f);
        }
    }

    #endregion

    #region Shimmer Effect

    private void CreateShimmerBars()
    {
        for (int i = 0; i < shimmerWaveCount; i++)
        {
            GameObject shimmer = new GameObject($"ShimmerWave_{i}");
            shimmer.transform.SetParent(transform, false);
            shimmer.transform.SetAsFirstSibling(); // Behind particles

            RectTransform rt = shimmer.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = shimmer.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;

            shimmerBars.Add(rt);
        }
    }

    private void UpdateShimmerEffect(float progress)
    {
        float barWidth = rectTransform.rect.width;

        for (int i = 0; i < shimmerBars.Count; i++)
        {
            RectTransform rt = shimmerBars[i];
            Image img = rt.GetComponent<Image>();

            // Wave position using sine - creates traveling wave effect
            float wavePhase = Time.time * shimmerSpeed + i * Mathf.PI;
            float wavePosition = (Mathf.Sin(wavePhase) + 1f) / 2f; // 0 to 1

            // Only show shimmer in the filled portion
            float shimmerX = wavePosition * progress * barWidth;

            // Gaussian-like intensity falloff
            float shimmerWidth = 40f;
            float normalizedX = shimmerX / barWidth;

            // Create the shimmer highlight using a gradient mask approach
            // Alpha based on distance from shimmer center
            float distFromShimmer = Mathf.Abs(normalizedX - wavePosition * progress);
            float alpha = Mathf.Exp(-distFromShimmer * distFromShimmer * 50f) * shimmerIntensity;

            // Boost shimmer near leading edge
            float edgeProximity = 1f - Mathf.Abs(progress - normalizedX) * 5f;
            edgeProximity = Mathf.Clamp01(edgeProximity);
            alpha += edgeProximity * 0.2f * Mathf.Sin(Time.time * 10f);

            // Only show in filled area
            if (normalizedX > progress)
                alpha = 0;

            img.color = new Color(1f, 1f, 1f, alpha * progress);

            // Subtle scale pulse
            float scaleX = 1f + 0.02f * Mathf.Sin(Time.time * 4f + i);
            rt.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    #endregion

    #region Glow Pulse

    private void CreateGlowOverlay()
    {
        GameObject glow = new GameObject("GlowOverlay");
        glow.transform.SetParent(transform, false);
        glow.transform.SetAsFirstSibling();

        RectTransform rt = glow.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-10f, -10f);
        rt.offsetMax = new Vector2(10f, 10f);

        glowOverlay = glow.AddComponent<Image>();
        glowOverlay.color = new Color(1f, 1f, 1f, 0f);
        glowOverlay.raycastTarget = false;
    }

    private void UpdateGlowPulse(float progress)
    {
        if (glowOverlay == null) return;

        // Multi-frequency pulse for organic feel
        float pulse1 = Mathf.Sin(Time.time * glowPulseSpeed) * 0.5f + 0.5f;
        float pulse2 = Mathf.Sin(Time.time * glowPulseSpeed * 1.7f) * 0.3f + 0.5f;
        float pulse3 = Mathf.Sin(Time.time * glowPulseSpeed * 0.5f) * 0.2f + 0.5f;

        float combinedPulse = (pulse1 + pulse2 + pulse3) / 3f;

        // Intensity increases with progress
        float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, progress);
        intensity *= combinedPulse;

        // Glow color shifts with progress
        Color glowColor = GetProgressColor(progress);
        glowOverlay.color = new Color(glowColor.r, glowColor.g, glowColor.b, intensity * 0.15f);
    }

    #endregion

    #region Color Gradient

    private void UpdateFillColor(float progress)
    {
        if (fillImage == null) return;

        Color targetColor = GetProgressColor(progress);

        // Add subtle hue shift based on time
        float hueShift = Mathf.Sin(Time.time * 2f) * 0.05f;
        Color.RGBToHSV(targetColor, out float h, out float s, out float v);
        h = Mathf.Repeat(h + hueShift, 1f);
        targetColor = Color.HSVToRGB(h, s, v);

        fillImage.color = targetColor;
    }

    private Color GetProgressColor(float progress)
    {
        // Three-point gradient with smooth interpolation
        if (progress < 0.5f)
        {
            float t = progress * 2f;
            // Smooth step for nicer transition
            t = t * t * (3f - 2f * t);
            return Color.Lerp(startColor, midColor, t);
        }
        else
        {
            float t = (progress - 0.5f) * 2f;
            t = t * t * (3f - 2f * t);
            return Color.Lerp(midColor, endColor, t);
        }
    }

    #endregion

    #region Completion Burst

    private IEnumerator CompletionBurst()
    {
        List<GameObject> burstParticles = new List<GameObject>();
        float barWidth = rectTransform.rect.width;
        Vector2 burstOrigin = new Vector2(barWidth, 0);

        // Create burst particles
        for (int i = 0; i < burstParticleCount; i++)
        {
            GameObject particle = new GameObject($"BurstParticle_{i}");
            particle.transform.SetParent(transform, false);

            RectTransform rt = particle.AddComponent<RectTransform>();
            rt.anchoredPosition = burstOrigin;
            float size = Random.Range(8f, 20f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localEulerAngles = new Vector3(0, 0, 45f);

            Image img = particle.AddComponent<Image>();
            img.color = endColor;
            img.raycastTarget = false;

            burstParticles.Add(particle);
        }

        // Animate burst
        float elapsed = 0f;
        List<Vector2> velocities = new List<Vector2>();
        List<float> rotSpeeds = new List<float>();

        // Initialize velocities with fibonacci spiral pattern
        for (int i = 0; i < burstParticleCount; i++)
        {
            // Golden angle for even distribution
            float goldenAngle = 137.5f * Mathf.Deg2Rad;
            float angle = i * goldenAngle;

            // Vary speed based on index
            float speed = burstSpeed * (0.5f + Random.Range(0f, 1f));
            velocities.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed);
            rotSpeeds.Add(Random.Range(-720f, 720f));
        }

        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;

            for (int i = 0; i < burstParticles.Count; i++)
            {
                GameObject p = burstParticles[i];
                if (p == null) continue;

                RectTransform rt = p.GetComponent<RectTransform>();
                Image img = p.GetComponent<Image>();

                // Move with velocity and gravity
                Vector2 pos = rt.anchoredPosition;
                Vector2 gravity = new Vector2(0, -400f) * Time.deltaTime;
                pos += velocities[i] * Time.deltaTime + gravity;
                rt.anchoredPosition = pos;

                // Apply drag to velocity
                velocities[i] *= 0.98f;

                // Rotate
                float rot = rt.localEulerAngles.z + rotSpeeds[i] * Time.deltaTime;
                rt.localEulerAngles = new Vector3(0, 0, rot);

                // Fade and shrink
                float alpha = 1f - t;
                float scale = Mathf.Lerp(1f, 0.2f, t);

                // Color shift during burst
                Color burstColor = Color.Lerp(endColor, Color.white, t * 0.5f);
                img.color = new Color(burstColor.r, burstColor.g, burstColor.b, alpha);
                rt.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // Cleanup
        foreach (var p in burstParticles)
        {
            if (p != null) Destroy(p);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Reset the VFX state for a new loading sequence.
    /// </summary>
    public void ResetVFX()
    {
        isComplete = false;
        lastProgress = 0f;

        if (fillImage != null)
            fillImage.color = startColor;
    }

    /// <summary>
    /// Manually trigger completion burst (for testing).
    /// </summary>
    [ContextMenu("Test Completion Burst")]
    public void TestCompletionBurst()
    {
        StartCoroutine(CompletionBurst());
    }

    #endregion
}
