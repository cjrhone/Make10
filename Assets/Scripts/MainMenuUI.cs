using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Handles Main Menu animations: bouncing title, scrolling banners.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Title Animation")]
    [SerializeField] private RectTransform titleCard;
    [SerializeField] private float bounceHeight = 20f;
    [SerializeField] private float bounceSpeed = 2f;
    #pragma warning disable CS0414 // Kept for Inspector visibility, rotation removed in L0
    [SerializeField] private float titleRotateAmount = 3f;
    #pragma warning restore CS0414
    
    [Header("Banner Settings")]
    [SerializeField] private RectTransform topBanner;
    [SerializeField] private RectTransform topBannerDuplicate; // Second copy for seamless loop
    [SerializeField] private RectTransform bottomBanner;
    [SerializeField] private RectTransform bottomBannerDuplicate; // Second copy for seamless loop
    [SerializeField] private float bannerScrollSpeed = 100f;
    [SerializeField] private float bannerWidth = 1500f; // Width of ONE banner text
    
    [Header("Button References")]
    [SerializeField] private Button playButton;         // Arcade Mode — wire to SceneFlowManager.OnPlayPressed()
    [SerializeField] private Button zenButton;           // Zen Mode — wire to SceneFlowManager.OnZenPressed()
    [SerializeField] private Button creditsButton;       // Credits — wire to SceneFlowManager.OnCreditsPressed()
    [SerializeField] private Button shopButton;          // Shop (greyed out) — wire to SceneFlowManager.OnShopPressed()
    [SerializeField] private Button optionsButton;       // Legacy — kept for backward compatibility
    [SerializeField] private Button quitButton;          // Legacy — kept for backward compatibility

    [Header("BP Display")]
    [SerializeField] private TMP_Text bpDisplayText;     // Shows "BP: X,XXX" on main menu bottom-left

    [Header("High Score Display")]
    [SerializeField] private TMP_Text highScoreDisplayText;

    private Vector2 titleStartPos;
    private float titleStartRotation;

    private void Start()
    {
        // Store initial title position
        if (titleCard != null)
        {
            titleStartPos = titleCard.anchoredPosition;
            titleStartRotation = titleCard.localEulerAngles.z;
        }

        // Setup button listeners
        SetupButtons();

        // Configure shop button as greyed out
        SetupShopButton();

        // Show high score on menu
        UpdateHighScoreDisplay();

        // Show BP currency
        UpdateBPDisplay();

        // Subscribe to BP changes so display updates after rounds
        if (RunManager.Instance != null)
            RunManager.Instance.OnBPChanged += OnBPChanged;

        // Start animations
        StartCoroutine(AnimateTitle());
    }

    private void OnEnable()
    {
        // Refresh high score and BP every time menu becomes visible
        UpdateHighScoreDisplay();
        UpdateBPDisplay();
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (RunManager.Instance != null)
            RunManager.Instance.OnBPChanged -= OnBPChanged;
    }

    private void OnBPChanged(int _)
    {
        UpdateBPDisplay();
        UpdateHighScoreDisplay();
    }

    /// <summary>
    /// Update the high score display on the main menu.
    /// Shows BP-based high scores (total BP per round, not raw score).
    /// </summary>
    private void UpdateHighScoreDisplay()
    {
        if (highScoreDisplayText == null) return;

        // Use BP high score keys (the real player-facing score including bonuses)
        int arcadeBestBP = PlayerPrefs.GetInt("Make10_HighScoreBP", 0);
        int arcadeGames = PlayerPrefs.GetInt("Make10_TotalGames", 0);
        int zenBestBP = PlayerPrefs.GetInt("Make10_ZenHighScoreBP", 0);
        int zenGames = PlayerPrefs.GetInt("Make10_ZenTotalGames", 0);

        if (arcadeGames > 0 || zenGames > 0)
        {
            string display = "";
            if (arcadeGames > 0)
                display += $"Arcade Best: {arcadeBestBP:N0} BP";
            if (zenGames > 0)
            {
                if (display.Length > 0) display += "  |  ";
                display += $"Zen Best: {zenBestBP:N0} BP";
            }
            highScoreDisplayText.text = display;
            highScoreDisplayText.gameObject.SetActive(true);
        }
        else
        {
            highScoreDisplayText.gameObject.SetActive(false);
        }
    }
    
    private void Update()
    {
        // Scroll banners continuously (seamless loop with duplicates)
        ScrollBannerPair(topBanner, topBannerDuplicate, 1f);  // Scroll right
        ScrollBannerPair(bottomBanner, bottomBannerDuplicate, -1f); // Scroll left
    }
    
    /// <summary>
    /// Setup button click listeners.
    /// Buttons can be wired in Inspector OR set up here in code.
    /// Code-based wiring only runs for buttons that have a reference assigned
    /// but no Inspector onClick events.
    /// </summary>
    private void SetupButtons()
    {
        // Wire Zen button if assigned but not yet wired
        if (zenButton != null && zenButton.onClick.GetPersistentEventCount() == 0)
        {
            zenButton.onClick.AddListener(() => {
                if (SceneFlowManager.Instance != null)
                    SceneFlowManager.Instance.OnZenPressed();
            });
        }

        // Wire Credits button if assigned but not yet wired
        if (creditsButton != null && creditsButton.onClick.GetPersistentEventCount() == 0)
        {
            creditsButton.onClick.AddListener(() => {
                if (SceneFlowManager.Instance != null)
                    SceneFlowManager.Instance.OnCreditsPressed();
            });
        }

        // Wire Shop button if assigned but not yet wired
        if (shopButton != null && shopButton.onClick.GetPersistentEventCount() == 0)
        {
            shopButton.onClick.AddListener(() => {
                if (SceneFlowManager.Instance != null)
                    SceneFlowManager.Instance.OnShopPressed();
            });
        }
    }

    /// <summary>
    /// Configure the shop button as greyed out with "Coming Soon" state.
    /// </summary>
    private void SetupShopButton()
    {
        if (shopButton == null) return;

        shopButton.interactable = false;

        // Grey out the button visuals
        ColorBlock colors = shopButton.colors;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        shopButton.colors = colors;

        // Add "Coming Soon" label if the button has a text child
        TMP_Text buttonText = shopButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = "Shop - Coming Soon";
            buttonText.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        }
    }

    /// <summary>
    /// Update the BP currency display on the main menu.
    /// Shows persistent spendable BP from RunManager.
    /// </summary>
    private void UpdateBPDisplay()
    {
        if (bpDisplayText == null) return;

        int spendableBP = RunManager.Instance != null
            ? RunManager.Instance.SpendableBP
            : PlayerPrefs.GetInt("Make10_SpendableBP", 0);
        bpDisplayText.text = $"BP: {spendableBP:N0}";
        bpDisplayText.gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Continuous smooth bob animation for the title.
    /// Single EaseInOutCubic oscillation — no rotation wobble, no scale pulse.
    /// </summary>
    private IEnumerator AnimateTitle()
    {
        while (true)
        {
            if (titleCard != null)
            {
                // Map sine wave through EaseInOutCubic for smooth acceleration/deceleration
                float rawT = (Mathf.Sin(Time.time * bounceSpeed) + 1f) / 2f; // 0→1 oscillation
                float easedT = AnimationUtilities.EaseInOutCubic(rawT);
                float yOffset = Mathf.Lerp(-bounceHeight, bounceHeight, easedT);
                titleCard.anchoredPosition = titleStartPos + new Vector2(0, yOffset);

                // Clean: no rotation, no scale pulse
                titleCard.localEulerAngles = Vector3.zero;
                titleCard.localScale = Vector3.one;
            }

            yield return null;
        }
    }
    
    /// <summary>
    /// Scroll a banner pair horizontally for seamless looping.
    /// When one banner scrolls off-screen, it repositions behind the other.
    /// </summary>
    private void ScrollBannerPair(RectTransform banner1, RectTransform banner2, float direction)
    {
        if (banner1 == null) return;
        
        // Move banner 1
        Vector2 pos1 = banner1.anchoredPosition;
        pos1.x += direction * bannerScrollSpeed * Time.deltaTime;
        banner1.anchoredPosition = pos1;
        
        // Move banner 2 (if exists)
        if (banner2 != null)
        {
            Vector2 pos2 = banner2.anchoredPosition;
            pos2.x += direction * bannerScrollSpeed * Time.deltaTime;
            banner2.anchoredPosition = pos2;
            
            // Check if either banner needs to wrap around
            if (direction > 0) // Scrolling right
            {
                if (pos1.x > bannerWidth)
                {
                    pos1.x = pos2.x - bannerWidth;
                    banner1.anchoredPosition = pos1;
                }
                if (pos2.x > bannerWidth)
                {
                    pos2.x = pos1.x - bannerWidth;
                    banner2.anchoredPosition = pos2;
                }
            }
            else // Scrolling left
            {
                if (pos1.x < -bannerWidth)
                {
                    pos1.x = pos2.x + bannerWidth;
                    banner1.anchoredPosition = pos1;
                }
                if (pos2.x < -bannerWidth)
                {
                    pos2.x = pos1.x + bannerWidth;
                    banner2.anchoredPosition = pos2;
                }
            }
        }
        else
        {
            // Fallback for single banner (will have gaps)
            if (direction > 0 && pos1.x > bannerWidth / 2f)
            {
                pos1.x -= bannerWidth;
                banner1.anchoredPosition = pos1;
            }
            else if (direction < 0 && pos1.x < -bannerWidth / 2f)
            {
                pos1.x += bannerWidth;
                banner1.anchoredPosition = pos1;
            }
        }
    }
    
    /// <summary>
    /// Button hover effect (optional, call from EventTrigger).
    /// </summary>
    public void OnButtonHover(RectTransform button)
    {
        if (button != null)
        {
            button.localScale = Vector3.one * 1.1f;
        }
    }
    
    /// <summary>
    /// Button exit hover effect.
    /// </summary>
    public void OnButtonExit(RectTransform button)
    {
        if (button != null)
        {
            button.localScale = Vector3.one;
        }
    }
}
