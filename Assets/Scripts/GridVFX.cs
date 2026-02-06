using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles visual effects for the grid: line highlight sweeps, screen shake,
/// tile land sparkles, and ambient background particles.
/// Attach to the same GameObject as GridManager or as a singleton.
/// </summary>
public class GridVFX : MonoBehaviour
{
    public static GridVFX Instance { get; private set; }

    [Header("Beam Flash Settings")]
    [SerializeField] private Sprite beamSprite;          // Assign particles/12.png or 13.png (vertical light streak)
    [SerializeField] private Sprite[] sparkleSprites;    // Assign particles/3, 5, 8, 9 (sparkle/star shapes)
    [SerializeField] private float beamFlashDuration = 0.45f;
    [SerializeField] private float beamOvershoot = 1.2f; // Beam extends slightly past grid edges
    [SerializeField] private Color beamColor = new Color(1f, 1f, 0.85f, 1f);
    [SerializeField] private int beamSparkleCount = 8;   // Sparkles scattered along the beam
    [SerializeField] private float beamSparkleSize = 28f;
    [SerializeField] private float beamThickness = 1.6f; // Multiplier on tile height for beam thickness

    [Header("Screen Shake Settings")]
    [SerializeField] private float baseShakeIntensity = 4f;
    [SerializeField] private float shakeIntensityPerChain = 2f;
    [SerializeField] private float maxShakeIntensity = 18f;
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeFrequency = 35f;

    [Header("Tile Sparkle Settings")]
    [SerializeField] private int sparklesPerTile = 5;
    [SerializeField] private float sparkleLifetime = 0.45f;
    [SerializeField] private float sparkleSpeed = 90f;
    [SerializeField] private float sparkleSize = 14f;
    [SerializeField] private Color sparkleColorA = new Color(1f, 0.95f, 0.6f, 0.9f);
    [SerializeField] private Color sparkleColorB = new Color(0.6f, 0.9f, 1f, 0.9f);

    [Header("Ambient Particles")]
    [SerializeField] private bool enableAmbientParticles = true;
    [SerializeField] private int ambientParticleCount = 18;
    [SerializeField] private float ambientSpeed = 18f;
    [SerializeField] private float ambientSizeMin = 8f;
    [SerializeField] private float ambientSizeMax = 16f;
    [SerializeField] private float ambientAlpha = 0.4f;
    [SerializeField] private Color ambientColorA = new Color(0.3f, 1f, 0.5f, 1f);  // Bright green
    [SerializeField] private Color ambientColorB = new Color(0.1f, 0.85f, 0.35f, 1f); // Deeper green

    // State
    private RectTransform gridContainer;
    private List<GameObject> ambientParticles = new List<GameObject>();
    private Coroutine ambientCoroutine;
    private bool isShaking = false;
    private Vector2 originalGridPosition;
    private Material additiveMaterial; // Cached additive UI material

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Create additive UI material at runtime
        Shader additiveShader = Shader.Find("UI/Additive");
        if (additiveShader != null)
        {
            additiveMaterial = new Material(additiveShader);
            Debug.Log("[GridVFX] Additive UI material created successfully.");
        }
        else
        {
            Debug.LogWarning("[GridVFX] Could not find 'UI/Additive' shader. Beam particles will use default UI shader (black backgrounds will show).");
        }
    }

    /// <summary>
    /// Initialize with the grid container reference.
    /// </summary>
    public void Initialize(RectTransform container)
    {
        gridContainer = container;
        originalGridPosition = container.anchoredPosition;

        Debug.Log($"[GridVFX] Initialized with container: {container.name}, rect: {container.rect.size}, sizeDelta: {container.sizeDelta}");

        if (enableAmbientParticles)
        {
            StartAmbientParticles();
        }
    }

    private void OnDestroy()
    {
        CleanupAmbient();
        if (additiveMaterial != null)
            Destroy(additiveMaterial);
    }

    #region Beam Flash

    /// <summary>
    /// Flash a beam of light across each matched row/column simultaneously.
    /// The entire line lights up at once — no scrubbing.
    /// </summary>
    public IEnumerator PlayLineSweeps(MatchResult result, float tileSize, float tileSpacing)
    {
        if (gridContainer == null || result == null) yield break;

        int gridWidth = 5;
        int gridHeight = 5;
        GridManager gm = GridManager.FindFirstObjectByType<GridManager>();
        if (gm != null)
        {
            Vector2Int size = gm.GetGridSize();
            gridWidth = size.x;
            gridHeight = size.y;
        }

        float totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
        float totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;

        List<Coroutine> flashes = new List<Coroutine>();

        // Flash beam across each matched row (horizontal)
        foreach (int row in result.matchedRows)
        {
            float rowY = totalHeight / 2f - tileSize / 2f - row * (tileSize + tileSpacing);
            float beamWidth = totalWidth * beamOvershoot;
            float beamHeight = tileSize * beamThickness;
            flashes.Add(StartCoroutine(FlashBeam(new Vector2(0f, rowY), new Vector2(beamWidth, beamHeight), true, totalWidth, rowY)));
        }

        // Flash beam across each matched column (vertical)
        foreach (int col in result.matchedColumns)
        {
            float colX = -totalWidth / 2f + tileSize / 2f + col * (tileSize + tileSpacing);
            float beamWidth = tileSize * beamThickness;
            float beamHeight = totalHeight * beamOvershoot;
            flashes.Add(StartCoroutine(FlashBeam(new Vector2(colX, 0f), new Vector2(beamWidth, beamHeight), false, totalHeight, colX)));
        }

        foreach (Coroutine c in flashes)
            yield return c;
    }

    /// <summary>
    /// Helper: create an additive Image on a new GameObject parented to gridContainer.
    /// </summary>
    private Image CreateAdditiveImage(string name, Vector2 position, Vector2 sizeDelta, Sprite sprite = null, float rotation = 0f)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(gridContainer, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;
        if (rotation != 0f)
            rt.localEulerAngles = new Vector3(0, 0, rotation);

        Image img = obj.AddComponent<Image>();
        img.raycastTarget = false;
        if (sprite != null)
            img.sprite = sprite;
        img.color = new Color(1f, 1f, 1f, 0f); // Start invisible

        // Apply additive material so black = transparent, white = glow
        if (additiveMaterial != null)
            img.material = additiveMaterial;

        return img;
    }

    private IEnumerator FlashBeam(Vector2 center, Vector2 size, bool isHorizontal, float lineLength, float fixedAxisPos)
    {
        float rot = isHorizontal ? 90f : 0f;

        // When rotating 90°, RectTransform sizeDelta X becomes visual Y and vice versa.
        // Swap dimensions so the beam visually stretches the correct way.
        // After this swap: size.x = thickness (short), size.y = length (long) — always.
        if (isHorizontal)
            size = new Vector2(size.y, size.x);

        List<GameObject> allObjects = new List<GameObject>();

        // === LAYER 1: Wide soft glow (biggest, softest — the "bloom") ===
        Image glowImg = CreateAdditiveImage("BeamGlow", center, size * 2.2f, null, rot);
        GlowTextureGenerator.ApplyCircularGlow(glowImg, 64, 1.5f);
        if (additiveMaterial != null) glowImg.material = additiveMaterial;
        allObjects.Add(glowImg.gameObject);

        // === LAYER 2: Core beam sprite (the main light streak) ===
        Image beamImg = CreateAdditiveImage("BeamCore", center, size, beamSprite, rot);
        allObjects.Add(beamImg.gameObject);

        // === LAYER 3: Hot center (narrow, extra bright — the "core" of the light) ===
        Vector2 coreSize = new Vector2(size.x * 0.4f, size.y);
        Image coreImg = CreateAdditiveImage("BeamHotCore", center, coreSize, beamSprite, rot);
        allObjects.Add(coreImg.gameObject);

        // === LAYER 4: Sparkle sprites scattered along the beam ===
        List<Image> sparkleImages = new List<Image>();
        for (int i = 0; i < beamSparkleCount; i++)
        {
            // Spread sparkles along the line length, with slight off-axis jitter based on thickness
            float posAlongLine = Random.Range(-lineLength * 0.5f, lineLength * 0.5f);
            float posOffAxis = Random.Range(-size.x * 0.4f, size.x * 0.4f);
            Vector2 sparkPos;
            if (isHorizontal)
                sparkPos = new Vector2(posAlongLine, fixedAxisPos + posOffAxis);
            else
                sparkPos = new Vector2(fixedAxisPos + posOffAxis, posAlongLine);

            float sparkSize = beamSparkleSize * Random.Range(0.5f, 1.4f);
            Sprite spr = (sparkleSprites != null && sparkleSprites.Length > 0)
                ? sparkleSprites[Random.Range(0, sparkleSprites.Length)]
                : null;

            Image sImg = CreateAdditiveImage($"BeamSparkle_{i}", sparkPos, new Vector2(sparkSize, sparkSize), spr, Random.Range(0f, 360f));
            sImg.gameObject.SetActive(false);
            sparkleImages.Add(sImg);
            allObjects.Add(sImg.gameObject);
        }

        // === Animate: flash in → hold → fade out ===
        float elapsed = 0f;
        float flashInTime = 0.07f;
        float holdTime = 0.13f;
        float fadeOutTime = beamFlashDuration - flashInTime - holdTime;
        bool sparklesActivated = false;

        while (elapsed < beamFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / beamFlashDuration;

            float alpha;
            float thicknessScale;

            if (elapsed < flashInTime)
            {
                // FLASH IN — rapid expand from thin line to full beam
                float ft = elapsed / flashInTime;
                alpha = Mathf.Pow(ft, 0.5f); // Fast rise (sqrt curve)
                thicknessScale = Mathf.Lerp(0.15f, 1.1f, ft); // Overshoot slightly
            }
            else if (elapsed < flashInTime + holdTime)
            {
                // HOLD — full brightness with gentle pulse
                float ht = (elapsed - flashInTime) / holdTime;
                alpha = 1f;
                thicknessScale = 1.1f - Mathf.Sin(ht * Mathf.PI) * 0.1f; // Settle from overshoot

                if (!sparklesActivated)
                {
                    sparklesActivated = true;
                    foreach (var sImg in sparkleImages)
                        if (sImg != null) sImg.gameObject.SetActive(true);
                }
            }
            else
            {
                // FADE OUT — beam thins and fades
                float ft = (elapsed - flashInTime - holdTime) / fadeOutTime;
                alpha = 1f - (ft * ft); // Quadratic ease out
                thicknessScale = Mathf.Lerp(1f, 0.3f, ft); // Beam gets thinner as it fades
            }

            // Unified sizing: size.x = thickness, size.y = length (rotation handles orientation)

            // --- Apply to glow (layer 1) ---
            float glowAlpha = alpha * 0.5f;
            glowImg.color = new Color(beamColor.r, beamColor.g, beamColor.b, glowAlpha);
            RectTransform glowRT = glowImg.GetComponent<RectTransform>();
            glowRT.sizeDelta = new Vector2(size.x * thicknessScale * 2.2f, size.y * 2.2f);

            // --- Apply to core beam (layer 2) ---
            beamImg.color = new Color(beamColor.r, beamColor.g, beamColor.b, alpha);
            RectTransform beamRT = beamImg.GetComponent<RectTransform>();
            beamRT.sizeDelta = new Vector2(size.x * thicknessScale, size.y);

            // --- Apply to hot core (layer 3) — stays bright longer ---
            float coreAlpha = Mathf.Min(alpha * 1.3f, 1f);
            coreImg.color = new Color(1f, 1f, 1f, coreAlpha); // Pure white for maximum brightness
            RectTransform coreRT = coreImg.GetComponent<RectTransform>();
            coreRT.sizeDelta = new Vector2(size.x * thicknessScale * 0.4f, size.y);

            // --- Animate sparkles (layer 4) ---
            foreach (var sImg in sparkleImages)
            {
                if (sImg == null) continue;
                RectTransform srt = sImg.GetComponent<RectTransform>();
                if (srt == null) continue;

                sImg.color = new Color(1f, 1f, 0.9f, alpha * 0.85f);

                // Pop scale: grow fast then shrink
                float sparkScale;
                if (t < 0.25f)
                    sparkScale = Mathf.Pow(t / 0.25f, 0.5f); // Quick pop
                else
                    sparkScale = Mathf.Max(0f, 1f - (t - 0.25f) / 0.75f);
                srt.localScale = Vector3.one * sparkScale;

                // Slow spin
                srt.localEulerAngles += new Vector3(0, 0, 120f * Time.deltaTime);
            }

            yield return null;
        }

        // Cleanup all objects
        foreach (var obj in allObjects)
            if (obj != null) Destroy(obj);
    }

    #endregion

    #region Screen Shake

    /// <summary>
    /// Shake the grid container. Intensity scales with consecutive chain count.
    /// </summary>
    public void TriggerShake(int chainCount = 1)
    {
        if (gridContainer == null || isShaking) return;

        float intensity = Mathf.Min(baseShakeIntensity + (chainCount - 1) * shakeIntensityPerChain, maxShakeIntensity);
        StartCoroutine(ShakeCoroutine(intensity));
    }

    private IEnumerator ShakeCoroutine(float intensity)
    {
        isShaking = true;
        float elapsed = 0f;
        Vector2 origin = originalGridPosition;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shakeDuration;
            float decay = 1f - t; // Linear decay

            float offsetX = Mathf.Sin(elapsed * shakeFrequency) * intensity * decay;
            float offsetY = Mathf.Cos(elapsed * shakeFrequency * 1.3f) * intensity * decay * 0.7f;

            gridContainer.anchoredPosition = origin + new Vector2(offsetX, offsetY);
            yield return null;
        }

        gridContainer.anchoredPosition = origin;
        isShaking = false;
    }

    #endregion

    #region Tile Land Sparkle

    /// <summary>
    /// Spawn sparkle particles at a tile's position when it lands.
    /// tileWidth controls how wide the sparkles spread across the tile.
    /// </summary>
    public void SpawnLandSparkle(Vector2 position, float tileWidth = 60f)
    {
        if (gridContainer == null) return;
        StartCoroutine(LandSparkleCoroutine(position, tileWidth));
    }

    private IEnumerator LandSparkleCoroutine(Vector2 position, float tileWidth)
    {
        float halfWidth = tileWidth * 0.5f;
        for (int i = 0; i < sparklesPerTile; i++)
        {
            // Spread sparkles across the full tile width
            Vector2 offset = new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-5f, 12f));
            StartCoroutine(AnimateSingleSparkle(position + offset));
            yield return new WaitForSeconds(0.025f);
        }
    }

    private IEnumerator AnimateSingleSparkle(Vector2 startPos)
    {
        GameObject sparkle = new GameObject("LandSparkle");
        sparkle.transform.SetParent(gridContainer, false);

        RectTransform rt = sparkle.AddComponent<RectTransform>();
        rt.anchoredPosition = startPos;
        float size = sparkleSize * Random.Range(0.6f, 1.2f);
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

        Image img = sparkle.AddComponent<Image>();
        Color baseColor = Color.Lerp(sparkleColorA, sparkleColorB, Random.value);
        img.color = baseColor;
        img.raycastTarget = false;

        // Upward drift with slight randomness
        float angle = Random.Range(50f, 130f) * Mathf.Deg2Rad;
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * sparkleSpeed * Random.Range(0.7f, 1.3f);

        float elapsed = 0f;
        while (elapsed < sparkleLifetime)
        {
            if (sparkle == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / sparkleLifetime;

            // Move upward
            rt.anchoredPosition += velocity * Time.deltaTime;
            velocity.y -= 80f * Time.deltaTime; // Slight gravity

            // Scale: pop in quickly, shrink out
            float scale;
            if (t < 0.15f)
                scale = t / 0.15f; // Quick pop in
            else
                scale = 1f - ((t - 0.15f) / 0.85f); // Slow shrink

            rt.localScale = Vector3.one * scale;

            // Fade out
            float alpha = 1f - (t * t); // Quadratic fade
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);

            // Spin
            float rot = rt.localEulerAngles.z;
            rt.localEulerAngles = new Vector3(0, 0, rot + 180f * Time.deltaTime);

            yield return null;
        }

        if (sparkle != null)
            Destroy(sparkle);
    }

    #endregion

    #region Ambient Particles

    /// <summary>
    /// Start the ambient background particles.
    /// </summary>
    public void StartAmbientParticles()
    {
        if (gridContainer == null) return;

        CleanupAmbient();
        ambientCoroutine = StartCoroutine(AmbientParticleLoop());
    }

    public void StopAmbientParticles()
    {
        if (ambientCoroutine != null)
        {
            StopCoroutine(ambientCoroutine);
            ambientCoroutine = null;
        }
        CleanupAmbient();
    }

    private IEnumerator AmbientParticleLoop()
    {
        // Spawn initial batch
        for (int i = 0; i < ambientParticleCount; i++)
        {
            SpawnAmbientParticle(randomizeStartPosition: true);
            yield return new WaitForSeconds(0.1f);
        }

        // Continuously respawn particles as they expire
        while (true)
        {
            // Clean up destroyed particles
            ambientParticles.RemoveAll(p => p == null);

            // Maintain target count
            while (ambientParticles.Count < ambientParticleCount)
            {
                SpawnAmbientParticle(randomizeStartPosition: false);
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void SpawnAmbientParticle(bool randomizeStartPosition)
    {
        if (gridContainer == null) return;

        GameObject particle = new GameObject("AmbientParticle");
        particle.transform.SetParent(gridContainer, false);
        // Place behind tiles (index 0 = back)
        particle.transform.SetSiblingIndex(0);

        RectTransform rt = particle.AddComponent<RectTransform>();
        float size = Random.Range(ambientSizeMin, ambientSizeMax);
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

        Image img = particle.AddComponent<Image>();
        Color color = Color.Lerp(ambientColorA, ambientColorB, Random.value);
        img.color = new Color(color.r, color.g, color.b, 0f); // Start invisible
        img.raycastTarget = false;

        // Random position within grid area (or below if not randomized = rising from bottom)
        // Use sizeDelta as fallback if rect returns zero (layout not yet calculated)
        float containerWidth = gridContainer.rect.width > 1f ? gridContainer.rect.width : gridContainer.sizeDelta.x;
        float containerHeight = gridContainer.rect.height > 1f ? gridContainer.rect.height : gridContainer.sizeDelta.y;

        // Ensure minimum spread area
        if (containerWidth < 50f) containerWidth = 300f;
        if (containerHeight < 50f) containerHeight = 300f;

        float xPos = Random.Range(-containerWidth * 0.6f, containerWidth * 0.6f);
        float yPos;

        if (randomizeStartPosition)
            yPos = Random.Range(-containerHeight * 0.6f, containerHeight * 0.6f);
        else
            yPos = -containerHeight * 0.6f; // Start below grid

        rt.anchoredPosition = new Vector2(xPos, yPos);

        ambientParticles.Add(particle);

        float lifetime = Random.Range(8f, 15f);
        float driftX = Random.Range(-10f, 10f);
        float speed = ambientSpeed * Random.Range(0.5f, 1.5f);

        StartCoroutine(AnimateAmbientParticle(particle, rt, img, color, lifetime, driftX, speed));
    }

    private IEnumerator AnimateAmbientParticle(GameObject obj, RectTransform rt, Image img, Color color, float lifetime, float driftX, float speed)
    {
        float elapsed = 0f;
        float wobblePhase = Random.Range(0f, Mathf.PI * 2f);

        while (elapsed < lifetime)
        {
            if (obj == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            // Slow upward drift with horizontal wobble
            float wobble = Mathf.Sin((elapsed + wobblePhase) * 0.8f) * driftX;
            rt.anchoredPosition += new Vector2(wobble * Time.deltaTime, speed * Time.deltaTime);

            // Fade: in over first 15%, out over last 25%
            float alpha;
            if (t < 0.15f)
                alpha = t / 0.15f;
            else if (t > 0.75f)
                alpha = 1f - ((t - 0.75f) / 0.25f);
            else
                alpha = 1f;

            img.color = new Color(color.r, color.g, color.b, ambientAlpha * alpha);

            // Gentle pulse
            float pulse = 1f + Mathf.Sin(elapsed * 2f) * 0.15f;
            rt.localScale = Vector3.one * pulse;

            yield return null;
        }

        if (obj != null)
        {
            ambientParticles.Remove(obj);
            Destroy(obj);
        }
    }

    /// <summary>
    /// React ambient particles to a big solve — brief scatter outward.
    /// </summary>
    public void PulseAmbientParticles()
    {
        foreach (GameObject p in ambientParticles)
        {
            if (p == null) continue;
            RectTransform rt = p.GetComponent<RectTransform>();
            if (rt == null) continue;

            // Push particles outward from center
            Vector2 fromCenter = rt.anchoredPosition.normalized;
            if (fromCenter.sqrMagnitude < 0.01f)
                fromCenter = Random.insideUnitCircle.normalized;

            StartCoroutine(BurstPush(rt, fromCenter * 30f));
        }
    }

    private IEnumerator BurstPush(RectTransform rt, Vector2 push)
    {
        if (rt == null) yield break;

        float elapsed = 0f;
        float duration = 0.4f;
        Vector2 startOffset = Vector2.zero;

        while (elapsed < duration)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Quick push, slow return
            float strength = Mathf.Sin(t * Mathf.PI) * (1f - t);
            rt.anchoredPosition += push * strength * Time.deltaTime;

            yield return null;
        }
    }

    private void CleanupAmbient()
    {
        foreach (GameObject p in ambientParticles)
        {
            if (p != null) Destroy(p);
        }
        ambientParticles.Clear();
    }

    #endregion
}
