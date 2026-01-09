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
    [SerializeField] private float titleRotateAmount = 3f;
    
    [Header("Banner Settings")]
    [SerializeField] private RectTransform topBanner;
    [SerializeField] private RectTransform topBannerDuplicate; // Second copy for seamless loop
    [SerializeField] private RectTransform bottomBanner;
    [SerializeField] private RectTransform bottomBannerDuplicate; // Second copy for seamless loop
    [SerializeField] private float bannerScrollSpeed = 100f;
    [SerializeField] private float bannerWidth = 1500f; // Width of ONE banner text
    
    [Header("Button References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    
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
        
        // Start animations
        StartCoroutine(AnimateTitle());
    }
    
    private void Update()
    {
        // Scroll banners continuously (seamless loop with duplicates)
        ScrollBannerPair(topBanner, topBannerDuplicate, 1f);  // Scroll right
        ScrollBannerPair(bottomBanner, bottomBannerDuplicate, -1f); // Scroll left
    }
    
    /// <summary>
    /// Setup button click listeners.
    /// NOTE: Disabled - buttons are wired via Inspector onClick instead.
    /// Keeping this code for reference if needed later.
    /// </summary>
    private void SetupButtons()
    {
        // Buttons are wired in Inspector - don't double-wire here
        // If you want to use code-based wiring instead, uncomment below
        // and remove the Inspector onClick events
        
        /*
        if (playButton != null)
        {
            playButton.onClick.AddListener(() => {
                if (SceneFlowManager.Instance != null)
                    SceneFlowManager.Instance.OnPlayPressed();
            });
        }
        
        if (optionsButton != null)
        {
            optionsButton.onClick.AddListener(() => {
                if (SceneFlowManager.Instance != null)
                    SceneFlowManager.Instance.OnOptionsPressed();
            });
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() => {
                if (SceneFlowManager.Instance != null)
                    SceneFlowManager.Instance.OnQuitPressed();
            });
        }
        */
    }
    
    /// <summary>
    /// Continuous bouncing animation for the title.
    /// </summary>
    private IEnumerator AnimateTitle()
    {
        while (true)
        {
            if (titleCard != null)
            {
                float t = Time.time * bounceSpeed;
                
                // Bounce up and down
                float yOffset = Mathf.Sin(t) * bounceHeight;
                titleCard.anchoredPosition = titleStartPos + new Vector2(0, yOffset);
                
                // Slight rotation wobble
                float rotation = Mathf.Sin(t * 1.3f) * titleRotateAmount;
                titleCard.localEulerAngles = new Vector3(0, 0, rotation);
                
                // Subtle scale pulse
                float scale = 1f + Mathf.Sin(t * 0.8f) * 0.03f;
                titleCard.localScale = Vector3.one * scale;
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
