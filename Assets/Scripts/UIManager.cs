using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Handles all UI updates: score display, motivation bar, multiplier popups, game over screens.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetScoreText;
    
    [Header("Motivation Bar")]
    [SerializeField] private Slider motivationSlider;
    [SerializeField] private Image motivationFillImage;
    [SerializeField] private TMP_Text motivationText; // Optional: show percentage
    
    [Header("Motivation Bar Colors")]
    [SerializeField] private Color healthyColor = new Color(0.3f, 0.8f, 0.3f);    // Green
    [SerializeField] private Color warningColor = new Color(0.9f, 0.7f, 0.2f);    // Yellow
    [SerializeField] private Color dangerColor = new Color(0.9f, 0.2f, 0.2f);     // Red
    [SerializeField] private float warningThreshold = 50f;
    [SerializeField] private float dangerThreshold = 25f;
    
    [Header("Multiplier Popup")]
    [SerializeField] private GameObject multiplierPopup;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private float popupDuration = 1f;
    
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
    private Coroutine multiplierPopupCoroutine;
    
    private void Start()
{
    StartCoroutine(InitializeAfterDelay());
}

private IEnumerator InitializeAfterDelay()
{
    // Wait one frame to ensure GameManager.Instance is set
    yield return null;
    
    // Auto-find GameManager if not assigned
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
    gameManager.OnMultiplierApplied += HandleMultiplierApplied;
    gameManager.OnGameWon += HandleGameWon;
    gameManager.OnGameLost += HandleGameLost;
    
    // Initialize UI
    InitializeUI();
    
    Debug.Log("UIManager initialized successfully!");
    
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnScoreChanged -= HandleScoreChanged;
            gameManager.OnMotivationChanged -= HandleMotivationChanged;
            gameManager.OnMultiplierApplied -= HandleMultiplierApplied;
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
        if (multiplierPopup != null) multiplierPopup.SetActive(false);
        
        // Set target score text
        if (targetScoreText != null)
        {
            targetScoreText.text = "/ 200";
        }
        
        // Initialize motivation bar
        if (motivationSlider != null)
        {
            motivationSlider.maxValue = 100f;
            motivationSlider.value = 100f;
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
    /// Handle multiplier display.
    /// </summary>
    private void HandleMultiplierApplied(float speedMult, float cascadeMult)
    {
        // Only show popup for notable multipliers
        if (speedMult > 1f || cascadeMult > 1f)
        {
            ShowMultiplierPopup(speedMult, cascadeMult);
        }
    }
    
    /// <summary>
    /// Update the score display.
    /// </summary>
    private void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
            
            // Punch effect on score change
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
        
        // Update bar color based on level
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
            
            // Reset scale
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
            // Pulse scale
            if (motivationSlider != null)
            {
                float t = (Mathf.Sin(Time.time * 8f) + 1f) / 2f; // 0 to 1 oscillation
                float scale = Mathf.Lerp(1f, 1.05f, t);
                motivationSlider.transform.localScale = Vector3.one * scale;
            }
            yield return null;
        }
    }
    
    /// <summary>
    /// Show multiplier popup.
    /// </summary>
    private void ShowMultiplierPopup(float speedMult, float cascadeMult)
    {
        if (multiplierPopup == null || multiplierText == null) return;
        
        // Build multiplier string
        string text = "";
        
        if (speedMult >= 3f)
        {
            text = "HOT STREAK!\n×3";
        }
        else if (speedMult >= 2f)
        {
            text = "QUICK!\n×2";
        }
        else if (cascadeMult > 1f)
        {
            text = $"CHAIN ×{cascadeMult:F1}";
        }
        else
        {
            return; // Nothing notable to show
        }
        
        multiplierText.text = text;
        
        if (multiplierPopupCoroutine != null)
        {
            StopCoroutine(multiplierPopupCoroutine);
        }
        multiplierPopupCoroutine = StartCoroutine(ShowPopupCoroutine(multiplierPopup, popupDuration));
    }
    
    /// <summary>
    /// Show a popup for a duration then hide it.
    /// </summary>
    private IEnumerator ShowPopupCoroutine(GameObject popup, float duration)
    {
        popup.SetActive(true);
        
        // Scale in
        popup.transform.localScale = Vector3.zero;
        float elapsed = 0f;
        float scaleInTime = 0.15f;
        
        while (elapsed < scaleInTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleInTime;
            float scale = Mathf.Lerp(0f, 1.1f, t);
            popup.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        
        popup.transform.localScale = Vector3.one;
        
        yield return new WaitForSeconds(duration);
        
        // Scale out
        elapsed = 0f;
        float scaleOutTime = 0.1f;
        
        while (elapsed < scaleOutTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleOutTime;
            float scale = Mathf.Lerp(1f, 0f, t);
            popup.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        
        popup.SetActive(false);
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
        
        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(1f, punchScale, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        
        // Scale down
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
        // Hide screens
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        
        // Restart game
        if (gameManager != null)
        {
            gameManager.StartNewGame();
        }
        
        // Tell GridManager to reset (you'll need to implement this connection)
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }
    }
}