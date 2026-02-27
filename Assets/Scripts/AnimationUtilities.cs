using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Shared animation utilities for consistent UI animations across the game.
/// Use these static methods to reduce code duplication.
/// </summary>
public static class AnimationUtilities
{
    // ==========================================
    // EASING FUNCTIONS
    // ==========================================

    /// <summary>Landing, settling — decelerates into rest.</summary>
    public static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    /// <summary>Smooth S-curve — accelerates then decelerates.</summary>
    public static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    /// <summary>Overshoot then settle — popups, UI panels.</summary>
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>Springy bounce — tile landing, elastic settle.</summary>
    public static float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    /// <summary>Slow start, fast end — anticipation before shrink.</summary>
    public static float EaseInCubic(float t) => t * t * t;

    // ==========================================
    // ANIMATION COROUTINES
    // ==========================================

    /// <summary>
    /// Punch scale effect - snappy scale up (EaseOutBack) then settle down (EaseOutCubic).
    /// </summary>
    public static IEnumerator PunchScale(Transform target, float punchScale = 1.2f, float duration = 0.15f)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        float halfDuration = duration / 2f;

        // Scale up with overshoot feel
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = EaseOutBack(t);
            target.localScale = Vector3.one * Mathf.LerpUnclamped(1f, punchScale, eased);
            yield return null;
        }

        // Scale down with smooth deceleration
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = EaseOutCubic(t);
            target.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, eased);
            yield return null;
        }

        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Scale in from zero with EaseOutBack overshoot (keep curve override for callers that need it).
    /// </summary>
    public static IEnumerator ScaleIn(Transform target, float duration = 0.3f, float overshoot = 1.0f, AnimationCurve curve = null)
    {
        if (target == null) yield break;

        target.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (curve != null)
                t = curve.Evaluate(t);
            else
                t = EaseOutBack(t); // Default: overshoot then settle

            float scale = Mathf.LerpUnclamped(0f, overshoot, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }

        // If overshoot > 1, settle back to 1 with smooth deceleration
        if (overshoot > 1f)
        {
            elapsed = 0f;
            float settleDuration = duration * 0.3f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleDuration);
                float eased = EaseOutCubic(t);
                float scale = Mathf.Lerp(overshoot, 1f, eased);
                target.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Scale out to zero with EaseInCubic — slow anticipation then fast shrink.
    /// </summary>
    public static IEnumerator ScaleOut(Transform target, float duration = 0.2f)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = target.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInCubic(t);
            target.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        target.localScale = Vector3.zero;
    }
    
    /// <summary>
    /// Pop in effect - EaseOutBack pop to overshoot, EaseOutCubic settle to 1.
    /// </summary>
    public static IEnumerator PopIn(Transform target, float overshoot = 1.2f, float popDuration = 0.2f, float settleDuration = 0.1f)
    {
        if (target == null) yield break;

        target.localScale = Vector3.zero;

        // Pop to overshoot with EaseOutBack
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = EaseOutBack(t);
            target.localScale = Vector3.one * Mathf.LerpUnclamped(0f, overshoot, eased);
            yield return null;
        }

        // Settle to normal with EaseOutCubic
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float eased = EaseOutCubic(t);
            target.localScale = Vector3.one * Mathf.Lerp(overshoot, 1f, eased);
            yield return null;
        }

        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Fade a CanvasGroup in or out.
    /// </summary>
    public static IEnumerator FadeCanvasGroup(CanvasGroup group, bool fadeIn, float duration = 0.2f)
    {
        if (group == null) yield break;
        
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        float elapsed = 0f;
        
        group.alpha = startAlpha;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            group.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }
        
        group.alpha = endAlpha;
    }
    
    /// <summary>
    /// Float and fade animation — EaseOutCubic rise (decelerates), EaseInCubic fade (slow start).
    /// </summary>
    public static IEnumerator FloatAndFade(RectTransform target, TMP_Text text, float floatDistance = 50f, float duration = 0.8f)
    {
        if (target == null) yield break;

        Vector2 startPos = target.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, floatDistance);

        Color startColor = text != null ? text.color : Color.white;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Position decelerates as it rises
            float posEased = EaseOutCubic(t);
            target.anchoredPosition = Vector2.Lerp(startPos, endPos, posEased);

            // Alpha fades slowly at first, then fast at end
            if (text != null)
            {
                float alphaEased = EaseInCubic(t);
                text.color = Color.Lerp(startColor, endColor, alphaEased);
            }

            yield return null;
        }
    }
    
    /// <summary>
    /// Continuous pulse animation. Returns the coroutine so caller can stop it.
    /// </summary>
    public static IEnumerator PulseLoop(Transform target, float minScale = 1.0f, float maxScale = 1.15f, float speed = 8f)
    {
        if (target == null) yield break;
        
        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            float scale = Mathf.Lerp(minScale, maxScale, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
    }
    
    /// <summary>
    /// Continuous pulse with color shift. Returns the coroutine so caller can stop it.
    /// </summary>
    public static IEnumerator PulseLoopWithColor(Transform target, Graphic graphic,
        float minScale = 1.0f, float maxScale = 1.3f, float speed = 4f,
        Color? baseColor = null, Color? brightColor = null)
    {
        if (target == null) yield break;

        Color base_c = baseColor ?? Color.white;
        Color bright_c = brightColor ?? new Color(1f, 1f, 0.7f);

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            float scale = Mathf.Lerp(minScale, maxScale, t);
            target.localScale = Vector3.one * scale;

            if (graphic != null)
                graphic.color = Color.Lerp(base_c, bright_c, t);

            yield return null;
        }
    }

    /// <summary>
    /// Count up animation for numbers in text (Balatro-style score reveal).
    /// </summary>
    public static IEnumerator CountUp(TMP_Text text, int startValue, int endValue, float duration, string format = "{0}")
    {
        if (text == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out for satisfying feel
            t = 1f - Mathf.Pow(1f - t, 3f);
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, endValue, t));
            text.text = string.Format(format, currentValue);
            yield return null;
        }
        text.text = string.Format(format, endValue);
    }

    /// <summary>
    /// Drop in animation - EaseOutElastic for a clean springy bounce into place.
    /// </summary>
    public static IEnumerator DropIn(RectTransform target, float dropDistance = 50f, float duration = 0.3f)
    {
        if (target == null) yield break;

        Vector2 endPos = target.anchoredPosition;
        Vector2 startPos = endPos + new Vector2(0, dropDistance);

        target.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutElastic(t);
            target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            yield return null;
        }
        target.anchoredPosition = endPos;
    }
}

/// <summary>
/// Extension methods for easier AudioManager access with null safety.
/// </summary>
public static class AudioExtensions
{
    public static void PlayButtonClickSafe(this AudioManager audio)
    {
        if (audio != null) audio.PlayButtonClick();
    }
    
    public static void PlaySafe(this AudioManager audio, System.Action playAction)
    {
        if (audio != null) playAction?.Invoke();
    }
}
