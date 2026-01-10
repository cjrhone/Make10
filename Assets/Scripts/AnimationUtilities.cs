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
    /// <summary>
    /// Punch scale effect - scale up then back to normal.
    /// </summary>
    public static IEnumerator PunchScale(Transform target, float punchScale = 1.2f, float duration = 0.15f)
    {
        if (target == null) yield break;
        
        float elapsed = 0f;
        float halfDuration = duration / 2f;
        
        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            target.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, t);
            yield return null;
        }
        
        // Scale down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            target.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, t);
            yield return null;
        }
        
        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Scale in from zero (or custom start scale).
    /// </summary>
    public static IEnumerator ScaleIn(Transform target, float duration = 0.3f, float overshoot = 1.0f, AnimationCurve curve = null)
    {
        if (target == null) yield break;
        
        target.localScale = Vector3.zero;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            if (curve != null)
                t = curve.Evaluate(t);
            
            float scale = Mathf.Lerp(0f, overshoot, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        
        // If overshoot > 1, settle back to 1
        if (overshoot > 1f)
        {
            elapsed = 0f;
            float settleDuration = duration * 0.3f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / settleDuration;
                float scale = Mathf.Lerp(overshoot, 1f, t);
                target.localScale = Vector3.one * scale;
                yield return null;
            }
        }
        
        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Scale out to zero.
    /// </summary>
    public static IEnumerator ScaleOut(Transform target, float duration = 0.2f)
    {
        if (target == null) yield break;
        
        float elapsed = 0f;
        Vector3 startScale = target.localScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
        
        target.localScale = Vector3.zero;
    }
    
    /// <summary>
    /// Pop in effect - scale from 0 to overshoot, then settle to 1.
    /// </summary>
    public static IEnumerator PopIn(Transform target, float overshoot = 1.2f, float popDuration = 0.2f, float settleDuration = 0.1f)
    {
        if (target == null) yield break;
        
        target.localScale = Vector3.zero;
        
        // Pop to overshoot
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            target.localScale = Vector3.one * Mathf.Lerp(0f, overshoot, t);
            yield return null;
        }
        
        // Settle to normal
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            target.localScale = Vector3.one * Mathf.Lerp(overshoot, 1f, t);
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
    /// Float and fade animation (for score popups, etc).
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
            float t = elapsed / duration;
            
            target.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            
            if (text != null)
                text.color = Color.Lerp(startColor, endColor, t);
            
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
