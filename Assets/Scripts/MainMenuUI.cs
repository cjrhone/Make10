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
    [SerializeField] private RectTransform bottomBanner;
    [SerializeField] private float bannerScrollSpeed = 100f;
    [SerializeField] private float bannerWidth = 2000f; // Width of the banner text
    
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
        // Scroll banners continuously
        ScrollBanner(topBanner, 1f);  // Scroll right
        ScrollBanner(bottomBanner, -1f); // Scroll left
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
    /// Scroll a banner horizontally, looping seamlessly.
    /// </summary>
    private void ScrollBanner(RectTransform banner, float direction)
    {
        if (banner == null) return;
        
        Vector2 pos = banner.anchoredPosition;
        pos.x += direction * bannerScrollSpeed * Time.deltaTime;
        
        // Loop when scrolled past banner width
        if (direction > 0 && pos.x > bannerWidth / 2f)
        {
            pos.x -= bannerWidth;
        }
        else if (direction < 0 && pos.x < -bannerWidth / 2f)
        {
            pos.x += bannerWidth;
        }
        
        banner.anchoredPosition = pos;
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
