using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Handles all UI updates: score display, motivation bar, multiplier bar, game over screens.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetScoreText;
    
    [Header("Motivation Bar")]
    [SerializeField] private Slider motivationSlider;
    [SerializeField] private Image motivationFillImage;
    [SerializeField] private TMP_Text motivationText;
    
    [Header("Motivation Bar Colors")]
    [SerializeField] private Color healthyColor = new Color(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color warningColor = new Color(0.9f, 0.7f, 0.2f);
    [SerializeField] private Color dangerColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private float warningThreshold = 50f;
    [SerializeField] private float dangerThreshold = 25f;
    
    [Header("Multiplier Bar (NEW)")]
    [SerializeField] private GameObject multiplierPanel;
    [SerializeField] private Slider multiplierSlider;
    [SerializeField] private TMP_Text multiplierValueText; // Shows "x2", "x3", etc.
    [SerializeField] private TMP_Text multiplierTimerText; // Shows seconds remaining
    [SerializeField] private Image multiplierFillImage;
    
    [Header("Multiplier Bar Colors")]
    [SerializeField] private Color multiplierFullColor = new Color(1f, 0.8f, 0.2f);   // Gold
    [SerializeField] private Color multiplierLowColor = new Color(1f, 0.3f, 0.2f);    // Red-orange
    [SerializeField] private float multiplierLowThreshold = 2f; // seconds
    
    [Header("Multiplier Pulse Settings")]
    [SerializeField] private float pulseMinScale = 1.0f;
    [SerializeField] private float pulseMaxScale = 1.3f;
    [SerializeField] private float pulseSpeed = 4f;
    
    [Header("Score Popup")]
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Transform scorePopupParent;
    
    [Header("Game Over Screens")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private TMP_Text finalScoreText;
    
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    
    private Coroutine motivationPulseCoroutine;
    private Coroutine multiplierPulseCoroutine;
    
    private void Start()
    {
        StartCoroutine(InitializeAfterDelay());
    }
    
    private IEnumerator InitializeAfterDelay()
    {
        yield return null;
        
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }
        
        if (gameManager == null)
        {
            Debug.LogError("UIManager: Could not find GameManager!");
            yield break;
        }
        
        // Subscribe to events
        gameManager.OnScoreChanged += HandleScoreChanged;
        gameManager.OnMotivationChanged += HandleMotivationChanged;
        gameManager.OnMultiplierChanged += HandleMultiplierChanged;
        gameManager.OnGameWon += HandleGameWon;
        gameManager.OnGameLost += HandleGameLost;
        
        InitializeUI();
        
        Debug.Log("UIManager initialized successfully!");
    }
    
    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnScoreChanged -= HandleScoreChanged;
            gameManager.OnMotivationChanged -= HandleMotivationChanged;
            gameManager.OnMultiplierChanged -= HandleMultiplierChanged;
            gameManager.OnGameWon -= HandleGameWon;
            gameManager.OnGameLost -= HandleGameLost;
        }
    }
    
    /// <summary>
    /// Set up initial UI state.
    /// </summary>
    private void InitializeUI()
    {
        // Hide game over screens
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        
        // Hide multiplier panel initially
        if (multiplierPanel != null) multiplierPanel.SetActive(false);
        
        // Set target score text dynamically from GameManager
        if (targetScoreText != null && gameManager != null)
        {
            targetScoreText.text = $"/ {gameManager.WinScore}";
        }
        
        // Initialize motivation bar
        if (motivationSlider != null)
        {
            motivationSlider.maxValue = 100f;
            motivationSlider.value = 100f;
        }
        
        // Initialize multiplier bar - read duration from GameManager
        if (multiplierSlider != null && gameManager != null)
        {
            multiplierSlider.maxValue = gameManager.MultiplierDuration;
            multiplierSlider.value = gameManager.MultiplierDuration;
        }
        
        UpdateScoreDisplay(0);
        UpdateMotivationBar(100f);
    }
    
    /// <summary>
    /// Handle score changes.
    /// </summary>
    private void HandleScoreChanged(int newScore, int delta)
    {
        UpdateScoreDisplay(newScore);
        
        if (delta > 0)
        {
            SpawnScorePopup(delta);
        }
    }
    
    /// <summary>
    /// Handle motivation changes.
    /// </summary>
    private void HandleMotivationChanged(float motivation)
    {
        UpdateMotivationBar(motivation);
    }
    
    /// <summary>
    /// Handle multiplier bar state changes.
    /// </summary>
    private void HandleMultiplierChanged(bool active, float multiplier, float timer)
    {
        UpdateMultiplierBar(active, multiplier, timer);
    }
    
    /// <summary>
    /// Update the score display.
    /// </summary>
    private void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
            StartCoroutine(PunchScale(scoreText.transform, 1.2f, 0.15f));
        }
    }
    
    /// <summary>
    /// Update the motivation bar.
    /// </summary>
    private void UpdateMotivationBar(float motivation)
    {
        if (motivationSlider != null)
        {
            motivationSlider.value = motivation;
        }
        
        if (motivationText != null)
        {
            motivationText.text = $"{Mathf.RoundToInt(motivation)}%";
        }
        
        if (motivationFillImage != null)
        {
            if (motivation <= dangerThreshold)
            {
                motivationFillImage.color = dangerColor;
                StartDangerPulse();
            }
            else if (motivation <= warningThreshold)
            {
                motivationFillImage.color = warningColor;
                StopDangerPulse();
            }
            else
            {
                motivationFillImage.color = healthyColor;
                StopDangerPulse();
            }
        }
    }
    
    /// <summary>
    /// Update the multiplier bar display.
    /// </summary>
    private void UpdateMultiplierBar(bool active, float multiplier, float timer)
    {
        if (multiplierPanel == null) return;
        
        if (active)
        {
            // Show panel
            if (!multiplierPanel.activeSelf)
            {
                multiplierPanel.SetActive(true);
                StartMultiplierPulse();
                
                // Animate panel appearing
                StartCoroutine(PunchScale(multiplierPanel.transform, 1.15f, 0.2f));
            }
            
            // Update slider
            if (multiplierSlider != null)
            {
                multiplierSlider.value = timer;
            }
            
            // Update multiplier text
            if (multiplierValueText != null)
            {
                multiplierValueText.text = $"x{multiplier:F2}";
            }
            
            // Update timer countdown text
            if (multiplierTimerText != null)
            {
                multiplierTimerText.text = $"{timer:F1}s";
            }
            
            // Update bar color based on time remaining
            if (multiplierFillImage != null)
            {
                if (timer <= multiplierLowThreshold)
                {
                    multiplierFillImage.color = Color.Lerp(multiplierLowColor, multiplierFullColor, timer / multiplierLowThreshold);
                }
                else
                {
                    multiplierFillImage.color = multiplierFullColor;
                }
            }
        }
        else
        {
            // Hide panel
            if (multiplierPanel.activeSelf)
            {
                StopMultiplierPulse();
                multiplierPanel.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Start pulsing the multiplier text with energy.
    /// </summary>
    private void StartMultiplierPulse()
    {
        if (multiplierPulseCoroutine == null && multiplierValueText != null)
        {
            multiplierPulseCoroutine = StartCoroutine(MultiplierPulseCoroutine());
        }
    }
    
    /// <summary>
    /// Stop the multiplier pulse.
    /// </summary>
    private void StopMultiplierPulse()
    {
        if (multiplierPulseCoroutine != null)
        {
            StopCoroutine(multiplierPulseCoroutine);
            multiplierPulseCoroutine = null;
            
            if (multiplierValueText != null)
            {
                multiplierValueText.transform.localScale = Vector3.one;
            }
        }
    }
    
    /// <summary>
    /// Energetic pulse animation for multiplier text.
    /// </summary>
    private IEnumerator MultiplierPulseCoroutine()
    {
        while (true)
        {
            if (multiplierValueText != null)
            {
                // Smooth sine wave pulse
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
                multiplierValueText.transform.localScale = Vector3.one * scale;
                
                // Optional: slight color intensity pulse
                Color baseColor = Color.white;
                Color brightColor = new Color(1f, 1f, 0.7f); // Slight yellow glow
                multiplierValueText.color = Color.Lerp(baseColor, brightColor, t);
            }
            yield return null;
        }
    }
    
    /// <summary>
    /// Start pulsing the motivation bar when in danger.
    /// </summary>
    private void StartDangerPulse()
    {
        if (motivationPulseCoroutine == null)
        {
            motivationPulseCoroutine = StartCoroutine(DangerPulseCoroutine());
        }
    }
    
    /// <summary>
    /// Stop the danger pulse.
    /// </summary>
    private void StopDangerPulse()
    {
        if (motivationPulseCoroutine != null)
        {
            StopCoroutine(motivationPulseCoroutine);
            motivationPulseCoroutine = null;
            
            if (motivationSlider != null)
            {
                motivationSlider.transform.localScale = Vector3.one;
            }
        }
    }
    
    /// <summary>
    /// Pulse animation for danger state.
    /// </summary>
    private IEnumerator DangerPulseCoroutine()
    {
        while (true)
        {
            if (motivationSlider != null)
            {
                float t = (Mathf.Sin(Time.time * 8f) + 1f) / 2f;
                float scale = Mathf.Lerp(1f, 1.05f, t);
                motivationSlider.transform.localScale = Vector3.one * scale;
            }
            yield return null;
        }
    }
    
    /// <summary>
    /// Spawn floating score popup.
    /// </summary>
    private void SpawnScorePopup(int points)
    {
        if (scorePopupPrefab == null || scorePopupParent == null) return;
        
        GameObject popup = Instantiate(scorePopupPrefab, scorePopupParent);
        TMP_Text popupText = popup.GetComponent<TMP_Text>();
        
        if (popupText != null)
        {
            popupText.text = $"+{points}";
        }
        
        StartCoroutine(AnimateScorePopup(popup));
    }
    
    /// <summary>
    /// Animate score popup floating up and fading.
    /// </summary>
    private IEnumerator AnimateScorePopup(GameObject popup)
    {
        RectTransform rt = popup.GetComponent<RectTransform>();
        TMP_Text text = popup.GetComponent<TMP_Text>();
        
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 50f);
        float duration = 0.8f;
        float elapsed = 0f;
        
        Color startColor = text.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            text.color = Color.Lerp(startColor, endColor, t);
            
            yield return null;
        }
        
        Destroy(popup);
    }
    
    /// <summary>
    /// Quick scale punch effect.
    /// </summary>
    private IEnumerator PunchScale(Transform target, float punchScale, float duration)
    {
        float elapsed = 0f;
        float halfDuration = duration / 2f;
        
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(1f, punchScale, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(punchScale, 1f, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        
        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Handle win state.
    /// </summary>
    private void HandleGameWon()
    {
        if (winScreen != null)
        {
            winScreen.SetActive(true);
        }
        
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Final Score: {gameManager.Score}";
        }
    }
    
    /// <summary>
    /// Handle lose state.
    /// </summary>
    private void HandleGameLost()
    {
        if (loseScreen != null)
        {
            loseScreen.SetActive(true);
        }
        
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Score: {gameManager.Score}";
        }
    }
    
    /// <summary>
    /// Called by restart button.
    /// </summary>
    public void OnRestartButtonClicked()
    {
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        
        if (gameManager != null)
        {
            gameManager.StartNewGame();
        }
        
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }
    }
}
