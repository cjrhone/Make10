using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Subtle "chalkboard" backdrop for the avatar panel.
///
/// Replaces the flat blue CharacterPanel Image with:
///   • a procedural soft radial vignette (parchment / warm cream),
///   • a few low-alpha floating math glyphs (10s, +, =, tile digits)
///     that drift very slowly, clipped to the panel via RectMask2D.
///
/// Designed to echo ParallaxBackground (grid backdrop) without competing
/// with it — fewer elements, lower alpha, slower drift.
///
/// SETUP:
///   1. Attach this component to the CharacterPanel GameObject
///      (the parent of AvatarImage).
///   2. Press Play. The panel restyles itself; no inspector wiring needed.
///   3. Tweak `panelTint`, `glyphCount`, `glyphAlpha` to taste.
/// </summary>
[RequireComponent(typeof(Image))]
public class AvatarBackground : MonoBehaviour
{
    [Header("Panel Backdrop")]
    [Tooltip("Base color for the panel. Alpha controls overall strength.")]
    [SerializeField] private Color panelTint = new Color(0.93f, 0.91f, 0.85f, 0.55f);

    [Tooltip("Softness of the radial vignette (higher = sharper edge).")]
    [SerializeField, Range(1f, 4f)] private float vignetteFalloff = 1.6f;

    [Tooltip("Resolution of the generated vignette texture.")]
    [SerializeField] private int vignetteResolution = 128;

    [Header("Floating Math Glyphs")]
    [SerializeField] private bool spawnGlyphs = true;
    [SerializeField, Range(0, 16)] private int glyphCount = 7;
    [SerializeField] private float glyphSizeMin = 28f;
    [SerializeField] private float glyphSizeMax = 64f;
    [SerializeField, Range(0f, 0.3f)] private float glyphAlpha = 0.10f;
    [SerializeField] private float driftSpeedMin = 4f;
    [SerializeField] private float driftSpeedMax = 9f;
    [SerializeField] private Vector2 driftDirection = new Vector2(-1f, -0.25f);
    [SerializeField] private float wobbleAmount = 6f;
    [SerializeField] private float wobbleSpeed = 0.4f;
    [SerializeField] private float rotationDrift = 3f;

    [Header("Glyph Set")]
    [SerializeField] private string[] glyphPool = { "10", "10", "+", "=", "·", "0", "1", "2", "3", "4", "5", "6", "7" };

    [Tooltip("Tints used for digit glyphs. Indices loosely align with tile colors.")]
    [SerializeField] private Color[] glyphTints = new Color[]
    {
        new Color(0.55f, 0.45f, 0.35f),  // warm grey
        new Color(0.70f, 0.55f, 0.25f),  // gold
        new Color(0.30f, 0.45f, 0.65f),  // blue
        new Color(0.35f, 0.55f, 0.40f),  // green
        new Color(0.70f, 0.40f, 0.40f),  // coral
        new Color(0.70f, 0.50f, 0.30f),  // orange
        new Color(0.50f, 0.35f, 0.60f),  // purple
    };

    [Header("Lifecycle")]
    [Tooltip("Regenerate glyphs each time this GameObject is enabled.")]
    [SerializeField] private bool regenerateOnEnable = false;

    private Image panelImage;
    private RectTransform panelRect;
    private readonly List<Glyph> glyphs = new List<Glyph>();
    private bool initialized;

    private class Glyph
    {
        public RectTransform rect;
        public float speed;
        public float wobbleOffset;
        public float rotationSpeed;
        public Vector2 basePosition;
    }

    private void Awake()
    {
        panelImage = GetComponent<Image>();
        panelRect = (RectTransform)transform;
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    private void OnEnable()
    {
        if (regenerateOnEnable && initialized)
        {
            ClearGlyphs();
            SpawnGlyphs();
        }
    }

    private void OnDisable()
    {
        // Glyphs are children — they ride along with us.
    }

    private void Initialize()
    {
        ApplyBackdrop();
        EnsureMask();
        if (spawnGlyphs) SpawnGlyphs();
        initialized = true;
    }

    /// <summary>
    /// Replace the flat panel with a soft radial vignette in `panelTint`.
    /// Uses GlowTextureGenerator's cached circular falloff sprite.
    /// </summary>
    private void ApplyBackdrop()
    {
        if (panelImage == null) return;

        panelImage.sprite = GlowTextureGenerator.GetCircularGlowSprite(vignetteResolution, vignetteFalloff);
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = false; // stretch across the wide panel
        panelImage.color = panelTint;
        panelImage.raycastTarget = false;
    }

    /// <summary>
    /// Add a RectMask2D so floating glyphs clip cleanly at the panel edge.
    /// Idempotent — won't add a second mask if one already exists.
    /// </summary>
    private void EnsureMask()
    {
        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    private void SpawnGlyphs()
    {
        if (glyphCount <= 0 || glyphPool == null || glyphPool.Length == 0) return;

        Rect r = panelRect.rect;

        for (int i = 0; i < glyphCount; i++)
        {
            string content = glyphPool[Random.Range(0, glyphPool.Length)];
            float size = Random.Range(glyphSizeMin, glyphSizeMax);
            float speed = Random.Range(driftSpeedMin, driftSpeedMax);

            GameObject obj = new GameObject($"AvatarBg_{content}_{i}");
            obj.transform.SetParent(transform, false);
            // Render behind any sibling avatar image. CharacterPanel's children
            // are drawn in hierarchy order; we want glyphs *behind* AvatarImage,
            // so place them at the top of the sibling list.
            obj.transform.SetAsFirstSibling();

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size * 2f, size * 1.4f);

            Vector2 start = new Vector2(
                Random.Range(-r.width * 0.5f, r.width * 0.5f),
                Random.Range(-r.height * 0.5f, r.height * 0.5f)
            );
            rt.anchoredPosition = start;
            rt.localEulerAngles = new Vector3(0, 0, Random.Range(-12f, 12f));

            TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            Color tint = PickTintFor(content);
            text.color = new Color(tint.r, tint.g, tint.b, glyphAlpha);

            glyphs.Add(new Glyph
            {
                rect = rt,
                speed = speed,
                wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
                rotationSpeed = Random.Range(-rotationDrift, rotationDrift),
                basePosition = start,
            });
        }
    }

    /// <summary>
    /// Map a glyph string to a tint. Digits use their tile-color slot;
    /// symbols get a neutral warm grey.
    /// </summary>
    private Color PickTintFor(string content)
    {
        if (content.Length == 1 && int.TryParse(content, out int digit))
        {
            if (glyphTints != null && glyphTints.Length > 0)
            {
                return glyphTints[digit % glyphTints.Length];
            }
        }
        // "10" gets a slightly emphasized warm gold
        if (content == "10")
        {
            return new Color(0.65f, 0.50f, 0.25f);
        }
        // Math symbols: warm muted grey
        return new Color(0.50f, 0.45f, 0.40f);
    }

    private void Update()
    {
        if (glyphs.Count == 0) return;

        float t = Time.time;
        Vector2 drift = driftDirection.sqrMagnitude > 0.0001f
            ? driftDirection.normalized
            : Vector2.left;
        Vector2 perpendicular = new Vector2(-drift.y, drift.x);

        Rect r = panelRect.rect;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;
        const float buffer = 80f;

        for (int i = 0; i < glyphs.Count; i++)
        {
            var g = glyphs[i];
            if (g.rect == null) continue;

            Vector2 step = drift * g.speed * Time.deltaTime;
            float wobble = Mathf.Sin(t * wobbleSpeed + g.wobbleOffset) * wobbleAmount * Time.deltaTime;
            step += perpendicular * wobble;

            g.basePosition += step;

            // Wrap around so the field is infinite within the panel bounds.
            if (g.basePosition.x < -halfW - buffer) g.basePosition.x = halfW + buffer;
            else if (g.basePosition.x > halfW + buffer) g.basePosition.x = -halfW - buffer;
            if (g.basePosition.y < -halfH - buffer) g.basePosition.y = halfH + buffer;
            else if (g.basePosition.y > halfH + buffer) g.basePosition.y = -halfH - buffer;

            g.rect.anchoredPosition = g.basePosition;

            if (g.rotationSpeed != 0f)
            {
                float rot = g.rect.localEulerAngles.z;
                g.rect.localEulerAngles = new Vector3(0, 0, rot + g.rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void ClearGlyphs()
    {
        for (int i = 0; i < glyphs.Count; i++)
        {
            if (glyphs[i].rect != null)
            {
                Destroy(glyphs[i].rect.gameObject);
            }
        }
        glyphs.Clear();
    }

    private void OnDestroy()
    {
        ClearGlyphs();
    }

    [ContextMenu("Regenerate Glyphs")]
    public void Regenerate()
    {
        ClearGlyphs();
        ApplyBackdrop();
        EnsureMask();
        if (spawnGlyphs) SpawnGlyphs();
    }
}
