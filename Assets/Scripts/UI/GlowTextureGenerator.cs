using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Generates procedural soft glow textures for UI effects.
/// Caches textures by size to avoid regenerating.
/// </summary>
public static class GlowTextureGenerator
{
    private static Dictionary<int, Sprite> cachedGlowSprites = new Dictionary<int, Sprite>();
    private static Dictionary<int, Sprite> cachedDiamondGlowSprites = new Dictionary<int, Sprite>();

    /// <summary>
    /// Get or create a circular soft glow sprite.
    /// </summary>
    /// <param name="size">Texture size in pixels (will be cached)</param>
    /// <param name="falloffPower">How quickly the glow fades (higher = sharper edge)</param>
    public static Sprite GetCircularGlowSprite(int size = 64, float falloffPower = 2f)
    {
        int cacheKey = size * 100 + (int)(falloffPower * 10);

        if (cachedGlowSprites.TryGetValue(cacheKey, out Sprite cached))
        {
            return cached;
        }

        Texture2D texture = CreateCircularGlowTexture(size, falloffPower);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = $"CircularGlow_{size}_{falloffPower:F1}";

        cachedGlowSprites[cacheKey] = sprite;
        return sprite;
    }

    /// <summary>
    /// Get or create a diamond-shaped soft glow sprite (rotated square look).
    /// </summary>
    public static Sprite GetDiamondGlowSprite(int size = 64, float falloffPower = 2f)
    {
        int cacheKey = size * 100 + (int)(falloffPower * 10);

        if (cachedDiamondGlowSprites.TryGetValue(cacheKey, out Sprite cached))
        {
            return cached;
        }

        Texture2D texture = CreateDiamondGlowTexture(size, falloffPower);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = $"DiamondGlow_{size}_{falloffPower:F1}";

        cachedDiamondGlowSprites[cacheKey] = sprite;
        return sprite;
    }

    /// <summary>
    /// Creates a circular soft glow texture with radial falloff.
    /// </summary>
    private static Texture2D CreateCircularGlowTexture(int size, float falloffPower)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float center = size / 2f;
        float maxDist = center;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Normalized distance from center (0 at center, 1 at edge)
                float normalizedDist = dist / maxDist;

                // Soft falloff using power function
                // At center: alpha = 1, at edge: alpha approaches 0
                float alpha = Mathf.Pow(Mathf.Max(0f, 1f - normalizedDist), falloffPower);

                // Smooth the very center to avoid harsh bright spot
                if (normalizedDist < 0.1f)
                {
                    alpha = Mathf.Lerp(0.85f, alpha, normalizedDist / 0.1f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    /// <summary>
    /// Creates a diamond-shaped soft glow (for particles that rotate 45 degrees).
    /// Uses Manhattan distance for diamond shape.
    /// </summary>
    private static Texture2D CreateDiamondGlowTexture(int size, float falloffPower)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float center = size / 2f;
        float maxDist = center;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center + 0.5f);
                float dy = Mathf.Abs(y - center + 0.5f);

                // Manhattan distance creates diamond shape
                float dist = dx + dy;

                // Normalized distance
                float normalizedDist = dist / maxDist;

                // Soft falloff
                float alpha = Mathf.Pow(Mathf.Max(0f, 1f - normalizedDist), falloffPower);

                // Smooth center
                if (normalizedDist < 0.15f)
                {
                    alpha = Mathf.Lerp(0.9f, alpha, normalizedDist / 0.15f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    /// <summary>
    /// Apply a circular glow sprite to an Image component.
    /// </summary>
    public static void ApplyCircularGlow(Image image, int textureSize = 64, float falloffPower = 2f)
    {
        if (image == null) return;
        image.sprite = GetCircularGlowSprite(textureSize, falloffPower);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    /// <summary>
    /// Apply a diamond glow sprite to an Image component.
    /// </summary>
    public static void ApplyDiamondGlow(Image image, int textureSize = 64, float falloffPower = 2f)
    {
        if (image == null) return;
        image.sprite = GetDiamondGlowSprite(textureSize, falloffPower);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    // ==========================================
    // TILE SHINE (top-half gloss gradient)
    // ==========================================

    private static Dictionary<int, Sprite> cachedShineSprites = new Dictionary<int, Sprite>();

    /// <summary>
    /// Get or create a tile shine sprite — a horizontal gloss band across the
    /// top portion of the tile that fades to transparent below the midpoint.
    /// Gives tiles a classic Nintendo / candy-tile glossy look.
    /// </summary>
    /// <param name="width">Texture width in pixels</param>
    /// <param name="height">Texture height in pixels</param>
    /// <param name="coverage">How far down the tile the shine extends (0.0–1.0, default 0.45)</param>
    public static Sprite GetShineSprite(int width = 64, int height = 64, float coverage = 0.45f)
    {
        int cacheKey = width * 10000 + height * 10 + (int)(coverage * 100);
        if (cachedShineSprites.TryGetValue(cacheKey, out Sprite cached))
            return cached;

        Texture2D texture = CreateShineTexture(width, height, coverage);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = $"TileShine_{width}x{height}_{coverage:F2}";
        cachedShineSprites[cacheKey] = sprite;
        return sprite;
    }

    /// <summary>
    /// Creates a top-to-bottom gloss gradient texture.
    /// Full white at the very top, fading smoothly to fully transparent
    /// at the coverage boundary. Below coverage = fully transparent.
    /// </summary>
    private static Texture2D CreateShineTexture(int width, int height, float coverage)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            // UV: y=0 is bottom of texture, y=height-1 is top
            float normalizedY = (float)y / (height - 1); // 0 = bottom, 1 = top

            float alpha;
            if (normalizedY >= (1f - coverage))
            {
                // In the shine zone (top portion of tile)
                // Map from coverage boundary to top: 0→1
                float t = (normalizedY - (1f - coverage)) / coverage;
                // Smooth cubic falloff: strongest at top, fading toward boundary
                alpha = t * t * (3f - 2f * t); // smoothstep
            }
            else
            {
                alpha = 0f;
            }

            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Apply a shine sprite to an Image component.
    /// </summary>
    public static void ApplyShine(Image image, int textureSize = 64, float coverage = 0.45f)
    {
        if (image == null) return;
        image.sprite = GetShineSprite(textureSize, textureSize, coverage);
        image.type = Image.Type.Simple;
        image.preserveAspect = false; // Stretch to fill tile
    }

    /// <summary>
    /// Clear the texture cache (call on scene unload if needed).
    /// </summary>
    public static void ClearCache()
    {
        foreach (var sprite in cachedGlowSprites.Values)
        {
            if (sprite != null && sprite.texture != null)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
        }
        cachedGlowSprites.Clear();

        foreach (var sprite in cachedDiamondGlowSprites.Values)
        {
            if (sprite != null && sprite.texture != null)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
        }
        cachedDiamondGlowSprites.Clear();

        foreach (var sprite in cachedShineSprites.Values)
        {
            if (sprite != null && sprite.texture != null)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
        }
        cachedShineSprites.Clear();
    }
}
