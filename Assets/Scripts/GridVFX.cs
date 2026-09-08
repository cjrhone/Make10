using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles visual effects for the grid: line highlight sweeps, screen shake,
/// tile land sparkles, and ambient background particles.
/// Attach to the same GameObject as GridManager or as a singleton.
/// </summary>
public class GridVFX : MonoBehaviour {
  public static GridVFX Instance { get; private set; }

  [Header("Beam Flash Settings"), SerializeField] 
  private Sprite beamSprite; // Assign particles/12.png or 13.png (vertical light streak)

  [SerializeField] private Sprite[] sparkleSprites; // Assign particles/3, 5, 8, 9 (sparkle/star shapes)
  [SerializeField] private float beamFlashDuration = 0.30f;
  [SerializeField] private float beamOvershoot = 1.2f; // Beam extends slightly past grid edges
  [SerializeField] private int beamSparkleCount = 8;
  [SerializeField] private float beamSparkleSize = 28f;
  [SerializeField] private float beamThickness = 1.6f;

  [Header("Beam Gold Gradient"), SerializeField] 
  private Color goldEdgeColor = new(1f, 0.75f, 0.2f, 1f);

  [SerializeField] private float gradientPower = 1.5f;
  [SerializeField] private float uvScrollSpeed = 3.0f;
  [SerializeField] private float edgeFade = 0.12f;

  [Header("Screen Shake Settings"), SerializeField] 
  private float baseShakeIntensity = 8f;

  [SerializeField] private float shakeIntensityPerChain = 3f;
  [SerializeField] private float maxShakeIntensity = 24f;
  [SerializeField] private float shakeDuration = 0.3f;
  [SerializeField] private float shakeFrequency = 35f;

  [Header("Tile Sparkle Settings"), SerializeField] 
  private int sparklesPerTile = 5;

  [SerializeField] private float sparkleLifetime = 0.45f;
  [SerializeField] private float sparkleSpeed = 90f;
  [SerializeField] private float sparkleSize = 14f;
  [SerializeField] private Color sparkleColorA = new(1f, 0.95f, 0.6f, 0.9f);
  [SerializeField] private Color sparkleColorB = new(0.6f, 0.9f, 1f, 0.9f);

  [Header("Ambient Particles"), SerializeField] 
  private bool enableAmbientParticles = true;

  [SerializeField] private int ambientParticleCount = 8;
  [SerializeField] private float ambientSpeed = 18f;
  [SerializeField] private float ambientSizeMin = 4f;
  [SerializeField] private float ambientSizeMax = 10f;
  [SerializeField] private float ambientAlpha = 0.06f;
  [SerializeField] private Color ambientColorA = new(1f, 0.9f, 0.4f, 1f);
  [SerializeField] private Color ambientColorB = new(0.5f, 0.7f, 1f, 1f);

  // State
  private RectTransform gridContainer;
  private List<GameObject> ambientParticles = new();
  private Coroutine ambientCoroutine;
  private bool isShaking = false;
  private Vector2 originalGridPosition;
  private Material additiveMaterial; // Basic additive — for glow, sparkles
  private Material beamMaterial; // Enhanced additive — gold gradient, UV scroll, edge fade

  private void Awake() {
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }

    Instance = this;

    var additiveShader = Shader.Find("UI/Additive");
    if (additiveShader != null) {
      // Basic additive: no gradient, no scroll, no edge fade
      additiveMaterial = new Material(additiveShader);
      additiveMaterial.SetFloat("_GradientStrength", 0f);
      additiveMaterial.SetFloat("_ScrollSpeed", 0f);
      additiveMaterial.SetFloat("_EdgeFade", 0f);

      // Enhanced beam: gold gradient + UV scroll + soft edges
      beamMaterial = new Material(additiveShader);
      beamMaterial.SetColor("_GradientColor", goldEdgeColor);
      beamMaterial.SetFloat("_GradientStrength", 1f);
      beamMaterial.SetFloat("_GradientPower", gradientPower);
      beamMaterial.SetFloat("_ScrollSpeed", uvScrollSpeed);
      beamMaterial.SetFloat("_EdgeFade", edgeFade);

      Debug.Log("[GridVFX] Additive materials created (basic + beam).");
    }
    else {
      Debug.LogWarning("[GridVFX] Could not find 'UI/Additive' shader.");
    }
  }

  /// <summary>
  /// Initialize with the grid container reference.
  /// </summary>
  public void Initialize (RectTransform container) {
    gridContainer = container;
    originalGridPosition = container.anchoredPosition;

    Debug.Log(
      $"[GridVFX] Initialized with container: {container.name}, rect: {container.rect.size}, sizeDelta: {container.sizeDelta}");

    if (enableAmbientParticles) {
      StartAmbientParticles();
    }
  }

  private void OnDestroy() {
    CleanupAmbient();
    if (additiveMaterial != null) {
      Destroy(additiveMaterial);
    }

    if (beamMaterial != null) {
      Destroy(beamMaterial);
    }
  }

  #region Beam Flash

  /// <summary>
  /// Flash a beam of light across each matched row/column simultaneously.
  /// The entire line lights up at once — no scrubbing.
  /// </summary>
  public IEnumerator PlayLineSweeps (MatchResult result, float tileSize, float tileSpacing) {
    if (gridContainer == null || result == null) {
      yield break;
    }

    var gridWidth = 5;
    var gridHeight = 5;
    var gm = FindAnyObjectByType<GridManager>();
    if (gm != null) {
      var size = gm.GetGridSize();
      gridWidth = size.x;
      gridHeight = size.y;
    }

    var totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
    var totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;

    var flashes = new List<Coroutine>();

    // Flash beam across each matched row (horizontal)
    foreach (var row in result.matchedRows) {
      var rowY = totalHeight / 2f - tileSize / 2f - row * (tileSize + tileSpacing);
      var beamWidth = totalWidth * beamOvershoot;
      var beamHeight = tileSize * beamThickness;
      flashes.Add(StartCoroutine(FlashBeam(new Vector2(0f, rowY), new Vector2(beamWidth, beamHeight), true, totalWidth,
        rowY)));
    }

    // Flash beam across each matched column (vertical)
    foreach (var col in result.matchedColumns) {
      var colX = -totalWidth / 2f + tileSize / 2f + col * (tileSize + tileSpacing);
      var beamWidth = tileSize * beamThickness;
      var beamHeight = totalHeight * beamOvershoot;
      flashes.Add(StartCoroutine(FlashBeam(new Vector2(colX, 0f), new Vector2(beamWidth, beamHeight), false,
        totalHeight, colX)));
    }

    foreach (var c in flashes) {
      yield return c;
    }
  }

  /// <summary>
  /// Helper: create an additive Image (basic material — glow/sparkles).
  /// </summary>
  private Image CreateAdditiveImage (string name, Vector2 position, Vector2 sizeDelta, Sprite sprite = null,
    float rotation = 0f) {
    var obj = new GameObject(name);
    obj.transform.SetParent(gridContainer, false);

    var rt = obj.AddComponent<RectTransform>();
    rt.anchoredPosition = position;
    rt.sizeDelta = sizeDelta;
    if (rotation != 0f) {
      rt.localEulerAngles = new Vector3(0, 0, rotation);
    }

    var img = obj.AddComponent<Image>();
    img.raycastTarget = false;
    if (sprite != null) {
      img.sprite = sprite;
    }

    img.color = new Color(1f, 1f, 1f, 0f);

    if (additiveMaterial != null) {
      img.material = additiveMaterial;
    }

    return img;
  }

  /// <summary>
  /// Helper: create a beam Image (enhanced material — gold gradient + scroll + edge fade).
  /// </summary>
  private Image CreateBeamImage (string name, Vector2 position, Vector2 sizeDelta, Sprite sprite = null,
    float rotation = 0f) {
    var img = CreateAdditiveImage(name, position, sizeDelta, sprite, rotation);
    if (beamMaterial != null) {
      img.material = beamMaterial;
    }

    return img;
  }

  private IEnumerator FlashBeam (Vector2 center, Vector2 size, bool isHorizontal, float lineLength,
    float fixedAxisPos) {
    var rot = isHorizontal ? 90f : 0f;

    // Swap dimensions for 90° rotation so size.x = thickness, size.y = length — always.
    if (isHorizontal) {
      size = new Vector2(size.y, size.x);
    }

    var allObjects = new List<GameObject>();

    // === LAYER 1: Wide soft bloom (basic additive, no gradient) ===
    var glowImg = CreateAdditiveImage("BeamGlow", center, size * 2.5f, null, rot);
    GlowTextureGenerator.ApplyCircularGlow(glowImg, 64, 1.5f);
    allObjects.Add(glowImg.gameObject);

    // === LAYER 2: Core beam sprite (enhanced material — gold gradient + scroll + edge fade) ===
    var beamImg = CreateBeamImage("BeamCore", center, size, beamSprite, rot);
    allObjects.Add(beamImg.gameObject);

    // === LAYER 3: Hot center (enhanced material — narrower, extra bright) ===
    var coreSize = new Vector2(size.x * 0.35f, size.y);
    var coreImg = CreateBeamImage("BeamHotCore", center, coreSize, beamSprite, rot);
    allObjects.Add(coreImg.gameObject);

    // === LAYER 4: Sparkle sprites scattered along the beam (basic additive) ===
    var sparkleImages = new List<Image>();
    for (var i = 0; i < beamSparkleCount; i++) {
      var posAlongLine = Random.Range(-lineLength * 0.5f, lineLength * 0.5f);
      var posOffAxis = Random.Range(-size.x * 0.4f, size.x * 0.4f);
      Vector2 sparkPos;
      if (isHorizontal) {
        sparkPos = new Vector2(posAlongLine, fixedAxisPos + posOffAxis);
      }
      else {
        sparkPos = new Vector2(fixedAxisPos + posOffAxis, posAlongLine);
      }

      var sparkSize = beamSparkleSize * Random.Range(0.5f, 1.4f);
      var spr = sparkleSprites != null && sparkleSprites.Length > 0 ?
        sparkleSprites[Random.Range(0, sparkleSprites.Length)] :
        null;

      var sImg = CreateAdditiveImage($"BeamSparkle_{i}", sparkPos, new Vector2(sparkSize, sparkSize), spr,
        Random.Range(0f, 360f));
      sImg.gameObject.SetActive(false);
      sparkleImages.Add(sImg);
      allObjects.Add(sImg.gameObject);
    }

    // === Animate: BURST in → brief hold → fade out + afterglow ===
    var elapsed = 0f;
    var burstTime = 0.04f; // Explosive expand (faster = snappier)
    var holdTime = 0.06f; // Brief peak brightness
    var fadeTime = beamFlashDuration - burstTime - holdTime;
    var sparklesActivated = false;

    // Gold tint for the glow layer
    var glowGold = new Color(goldEdgeColor.r, goldEdgeColor.g, goldEdgeColor.b, 1f);

    while (elapsed < beamFlashDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / beamFlashDuration;

      float alpha;
      float thicknessScale;

      if (elapsed < burstTime) {
        // BURST — explosive expand from thin slit to 1.4x overshoot
        var bt = elapsed / burstTime;
        alpha = Mathf.Pow(bt, 0.3f); // Very fast rise
        thicknessScale = Mathf.Lerp(0.05f, 1.4f, bt * bt); // Accelerating expand with big overshoot
      }
      else if (elapsed < burstTime + holdTime) {
        // HOLD — snap back from overshoot to 1.0, full brightness
        var ht = (elapsed - burstTime) / holdTime;
        alpha = 1f;
        thicknessScale = Mathf.Lerp(1.4f, 1.0f, ht); // Snap from overshoot to normal

        if (!sparklesActivated) {
          sparklesActivated = true;
          foreach (var sImg in sparkleImages) {
            if (sImg != null) {
              sImg.gameObject.SetActive(true);
            }
          }
        }
      }
      else {
        // FADE OUT — beam thins and fades, glow lingers (afterglow)
        var ft = (elapsed - burstTime - holdTime) / fadeTime;
        alpha = 1f - ft * ft; // Quadratic ease out
        thicknessScale = Mathf.Lerp(1.0f, 0.15f, ft * ft); // Beam collapses back to thin line
      }

      // --- Glow (layer 1) — trails behind the beam, fades slower (afterglow) ---
      var glowFade =
        elapsed < burstTime + holdTime ? alpha * 0.6f : Mathf.Max(alpha * 0.6f, (1f - t) * 0.35f); // Afterglow lingers
      glowImg.color = new Color(glowGold.r, glowGold.g, glowGold.b, glowFade);
      var glowRT = glowImg.GetComponent<RectTransform>();
      var glowThick = Mathf.Max(thicknessScale, 0.5f); // Glow never fully collapses
      glowRT.sizeDelta = new Vector2(size.x * glowThick * 2.5f, size.y * 2.5f);

      // --- Core beam (layer 2) — gold-tinted, shader adds gradient + scroll ---
      beamImg.color = new Color(goldEdgeColor.r, goldEdgeColor.g, goldEdgeColor.b, alpha);
      var beamRT = beamImg.GetComponent<RectTransform>();
      beamRT.sizeDelta = new Vector2(size.x * thicknessScale, size.y);

      // --- Hot core (layer 3) — bright white-gold, stays bright slightly longer ---
      var coreAlpha = Mathf.Min(alpha * 1.4f, 1f);
      coreImg.color = new Color(1f, 0.95f, 0.7f, coreAlpha);
      var coreRT = coreImg.GetComponent<RectTransform>();
      coreRT.sizeDelta = new Vector2(size.x * thicknessScale * 0.35f, size.y);

      // --- Sparkles (layer 4) ---
      foreach (var sImg in sparkleImages) {
        if (sImg == null) {
          continue;
        }

        var srt = sImg.GetComponent<RectTransform>();
        if (srt == null) {
          continue;
        }

        sImg.color = new Color(1f, 0.95f, 0.7f, alpha * 0.9f); // Warm gold sparkles

        float sparkScale;
        if (t < 0.2f) {
          sparkScale = Mathf.Pow(t / 0.2f, 0.4f); // Explosive pop
        }
        else {
          sparkScale = Mathf.Max(0f, 1f - (t - 0.2f) / 0.8f);
        }

        srt.localScale = Vector3.one * sparkScale;

        srt.localEulerAngles += new Vector3(0, 0, 150f * Time.deltaTime);
      }

      yield return null;
    }

    // Cleanup
    foreach (var obj in allObjects) {
      if (obj != null) {
        Destroy(obj);
      }
    }
  }

  #endregion

  #region Screen Shake

  /// <summary>
  /// Shake the grid container. Intensity scales with consecutive chain count.
  /// </summary>
  public void TriggerShake (int chainCount = 1) {
    if (gridContainer == null || isShaking) {
      return;
    }

    var intensity = Mathf.Min(baseShakeIntensity + (chainCount - 1) * shakeIntensityPerChain, maxShakeIntensity);
    StartCoroutine(ShakeCoroutine(intensity));
  }

  private IEnumerator ShakeCoroutine (float intensity) {
    isShaking = true;
    var elapsed = 0f;
    var origin = originalGridPosition;

    while (elapsed < shakeDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / shakeDuration;
      var decay = 1f - t; // Linear decay

      var offsetX = Mathf.Sin(elapsed * shakeFrequency) * intensity * decay;
      var offsetY = Mathf.Cos(elapsed * shakeFrequency * 1.3f) * intensity * decay * 0.7f;

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
  public void SpawnLandSparkle (Vector2 position, float tileWidth = 60f) {
    if (gridContainer == null) {
      return;
    }

    StartCoroutine(LandSparkleCoroutine(position, tileWidth));
  }

  private IEnumerator LandSparkleCoroutine (Vector2 position, float tileWidth) {
    var halfWidth = tileWidth * 0.5f;
    for (var i = 0; i < sparklesPerTile; i++) {
      // Spread sparkles across the full tile width
      var offset = new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-5f, 12f));
      StartCoroutine(AnimateSingleSparkle(position + offset));
      yield return new WaitForSeconds(0.025f);
    }
  }

  private IEnumerator AnimateSingleSparkle (Vector2 startPos) {
    var sparkle = new GameObject("LandSparkle");
    sparkle.transform.SetParent(gridContainer, false);

    var rt = sparkle.AddComponent<RectTransform>();
    rt.anchoredPosition = startPos;
    var size = sparkleSize * Random.Range(0.6f, 1.2f);
    rt.sizeDelta = new Vector2(size, size);
    rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

    var img = sparkle.AddComponent<Image>();
    var baseColor = Color.Lerp(sparkleColorA, sparkleColorB, Random.value);
    img.color = baseColor;
    img.raycastTarget = false;

    // Upward drift with slight randomness
    var angle = Random.Range(50f, 130f) * Mathf.Deg2Rad;
    var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * sparkleSpeed * Random.Range(0.7f, 1.3f);

    var elapsed = 0f;
    while (elapsed < sparkleLifetime) {
      if (sparkle == null) {
        yield break;
      }

      elapsed += Time.deltaTime;
      var t = elapsed / sparkleLifetime;

      // Move upward
      rt.anchoredPosition += velocity * Time.deltaTime;
      velocity.y -= 80f * Time.deltaTime; // Slight gravity

      // Scale: pop in quickly, shrink out
      float scale;
      if (t < 0.15f) {
        scale = t / 0.15f; // Quick pop in
      }
      else {
        scale = 1f - (t - 0.15f) / 0.85f; // Slow shrink
      }

      rt.localScale = Vector3.one * scale;

      // Fade out
      var alpha = 1f - t * t; // Quadratic fade
      img.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);

      // Spin
      var rot = rt.localEulerAngles.z;
      rt.localEulerAngles = new Vector3(0, 0, rot + 180f * Time.deltaTime);

      yield return null;
    }

    if (sparkle != null) {
      Destroy(sparkle);
    }
  }

  #endregion

  #region Ambient Particles

  /// <summary>
  /// Start the ambient background particles.
  /// </summary>
  public void StartAmbientParticles() {
    if (gridContainer == null) {
      return;
    }

    CleanupAmbient();
    ambientCoroutine = StartCoroutine(AmbientParticleLoop());
  }

  public void StopAmbientParticles() {
    if (ambientCoroutine != null) {
      StopCoroutine(ambientCoroutine);
      ambientCoroutine = null;
    }

    CleanupAmbient();
  }

  private IEnumerator AmbientParticleLoop() {
    // Spawn initial batch
    for (var i = 0; i < ambientParticleCount; i++) {
      SpawnAmbientParticle(true);
      yield return new WaitForSeconds(0.1f);
    }

    // Continuously respawn particles as they expire
    while (true) {
      // Clean up destroyed particles
      ambientParticles.RemoveAll(p => p == null);

      // Maintain target count
      while (ambientParticles.Count < ambientParticleCount) {
        SpawnAmbientParticle(false);
      }

      yield return new WaitForSeconds(1f);
    }
  }

  private void SpawnAmbientParticle (bool randomizeStartPosition) {
    if (gridContainer == null) {
      return;
    }

    var particle = new GameObject("AmbientParticle");
    particle.transform.SetParent(gridContainer, false);
    // Place behind tiles (index 0 = back)
    particle.transform.SetSiblingIndex(0);

    var rt = particle.AddComponent<RectTransform>();
    var size = Random.Range(ambientSizeMin, ambientSizeMax);
    rt.sizeDelta = new Vector2(size, size);
    rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

    var img = particle.AddComponent<Image>();
    var color = Color.Lerp(ambientColorA, ambientColorB, Random.value);
    img.color = new Color(color.r, color.g, color.b, 0f); // Start invisible
    img.raycastTarget = false;

    // Random position within grid area (or below if not randomized = rising from bottom)
    // Use sizeDelta as fallback if rect returns zero (layout not yet calculated)
    var containerWidth = gridContainer.rect.width > 1f ? gridContainer.rect.width : gridContainer.sizeDelta.x;
    var containerHeight = gridContainer.rect.height > 1f ? gridContainer.rect.height : gridContainer.sizeDelta.y;

    // Ensure minimum spread area
    if (containerWidth < 50f) {
      containerWidth = 300f;
    }

    if (containerHeight < 50f) {
      containerHeight = 300f;
    }

    var xPos = Random.Range(-containerWidth * 0.6f, containerWidth * 0.6f);
    float yPos;

    if (randomizeStartPosition) {
      yPos = Random.Range(-containerHeight * 0.6f, containerHeight * 0.6f);
    }
    else {
      yPos = -containerHeight * 0.6f; // Start below grid
    }

    rt.anchoredPosition = new Vector2(xPos, yPos);

    ambientParticles.Add(particle);

    var lifetime = Random.Range(8f, 15f);
    var driftX = Random.Range(-10f, 10f);
    var speed = ambientSpeed * Random.Range(0.5f, 1.5f);

    StartCoroutine(AnimateAmbientParticle(particle, rt, img, color, lifetime, driftX, speed));
  }

  private IEnumerator AnimateAmbientParticle (GameObject obj, RectTransform rt, Image img, Color color, float lifetime,
    float driftX, float speed) {
    var elapsed = 0f;
    var wobblePhase = Random.Range(0f, Mathf.PI * 2f);

    while (elapsed < lifetime) {
      if (obj == null) {
        yield break;
      }

      elapsed += Time.deltaTime;
      var t = elapsed / lifetime;

      // Slow upward drift with horizontal wobble
      var wobble = Mathf.Sin((elapsed + wobblePhase) * 0.8f) * driftX;
      rt.anchoredPosition += new Vector2(wobble * Time.deltaTime, speed * Time.deltaTime);

      // Fade: in over first 15%, out over last 25%
      float alpha;
      if (t < 0.15f) {
        alpha = t / 0.15f;
      }
      else if (t > 0.75f) {
        alpha = 1f - (t - 0.75f) / 0.25f;
      }
      else {
        alpha = 1f;
      }

      img.color = new Color(color.r, color.g, color.b, ambientAlpha * alpha);

      // Gentle pulse
      var pulse = 1f + Mathf.Sin(elapsed * 2f) * 0.15f;
      rt.localScale = Vector3.one * pulse;

      yield return null;
    }

    if (obj != null) {
      ambientParticles.Remove(obj);
      Destroy(obj);
    }
  }

  /// <summary>
  /// React ambient particles to a big solve — brief scatter outward.
  /// </summary>
  public void PulseAmbientParticles() {
    foreach (var p in ambientParticles) {
      if (p == null) {
        continue;
      }

      var rt = p.GetComponent<RectTransform>();
      if (rt == null) {
        continue;
      }

      // Push particles outward from center
      var fromCenter = rt.anchoredPosition.normalized;
      if (fromCenter.sqrMagnitude < 0.01f) {
        fromCenter = Random.insideUnitCircle.normalized;
      }

      StartCoroutine(BurstPush(rt, fromCenter * 30f));
    }
  }

  private IEnumerator BurstPush (RectTransform rt, Vector2 push) {
    if (rt == null) {
      yield break;
    }

    var elapsed = 0f;
    var duration = 0.4f;
    var startOffset = Vector2.zero;

    while (elapsed < duration) {
      if (rt == null) {
        yield break;
      }

      elapsed += Time.deltaTime;
      var t = elapsed / duration;

      // Quick push, slow return
      var strength = Mathf.Sin(t * Mathf.PI) * (1f - t);
      rt.anchoredPosition += push * strength * Time.deltaTime;

      yield return null;
    }
  }

  private void CleanupAmbient() {
    foreach (var p in ambientParticles) {
      if (p != null) {
        Destroy(p);
      }
    }

    ambientParticles.Clear();
  }

  #endregion
}