using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Manages the shop UI between rounds (empty shell).
/// Displays BP balance and basic shop interface.
/// No card/purchase logic.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("UI References (Auto-generated if empty)")]
    [SerializeField] private RectTransform shopPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bpAmountText;
    [SerializeField] private RectTransform cardsContainer;
    [SerializeField] private Button nextRoundButton;
    [SerializeField] private Image bpBackdrop;

    [Header("Styling")]
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
    [SerializeField] private Color titleColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private Color bpColor = new Color(0.3f, 0.9f, 0.5f);
    [SerializeField] private Color bpBackdropColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.6f, 0.9f);
    [SerializeField] private float titleFontSize = 72f;

    [Header("Animation Settings")]
    [SerializeField] private float bpCountUpDuration = 0.8f;
    [SerializeField] private float bpCountUpDelay = 0.3f;

    [Header("Audio")]
    [SerializeField] private AudioClip shopMusic;

    // Runtime state
    private bool isInitialized = false;
    private Coroutine countUpCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        EnsureUIExists();
    }

    /// <summary>
    /// Creates shop UI elements if they don't exist.
    /// </summary>
    private void EnsureUIExists()
    {
        if (isInitialized) return;

        // Find or validate shop panel
        if (shopPanel == null)
        {
            shopPanel = GetComponent<RectTransform>();
            if (shopPanel == null)
            {
                Debug.LogError("[ShopManager] No RectTransform found! ShopManager should be on the shop panel.");
                return;
            }
        }

        // Create background if panel doesn't have one
        Image bgImage = shopPanel.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = shopPanel.gameObject.AddComponent<Image>();
            bgImage.color = backgroundColor;
            bgImage.raycastTarget = true;
        }

        // Create BP display in top-right corner with black backdrop
        CreateBPDisplay();

        // Create title at top center
        CreateTitle();

        // Create cards container in the CENTER of the screen (not in a layout group)
        CreateCardsContainer();

        // Create Next Round button at bottom
        CreateNextRoundButton();

        isInitialized = true;
        Debug.Log("[ShopManager] UI auto-generated successfully");
    }

    private void CreateTitle()
    {
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(shopPanel, false);

        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f); // Top center
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -40f);
        titleRT.sizeDelta = new Vector2(400f, 100f);

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "SHOP";
        titleText.fontSize = titleFontSize;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.Center;
    }

    private void CreateCardsContainer()
    {
        // Cards container - centered in screen, covers most of the shop area
        GameObject cardsObj = new GameObject("CardsContainer");
        cardsObj.transform.SetParent(shopPanel, false);

        cardsContainer = cardsObj.AddComponent<RectTransform>();
        // Anchor to fill most of the screen
        cardsContainer.anchorMin = new Vector2(0.05f, 0.15f);
        cardsContainer.anchorMax = new Vector2(0.95f, 0.85f);
        cardsContainer.offsetMin = Vector2.zero;
        cardsContainer.offsetMax = Vector2.zero;

        // No layout group - we'll position cards manually in pyramid formation
    }

    private void CreateBPDisplay()
    {
        // Container for BP (bottom-left corner)
        GameObject bpContainer = new GameObject("BPContainer");
        bpContainer.transform.SetParent(shopPanel, false);

        RectTransform bpContainerRT = bpContainer.AddComponent<RectTransform>();
        bpContainerRT.anchorMin = new Vector2(0f, 0f); // Bottom-left
        bpContainerRT.anchorMax = new Vector2(0f, 0f);
        bpContainerRT.pivot = new Vector2(0f, 0f);
        bpContainerRT.anchoredPosition = new Vector2(20f, 140f); // Above the Next Round button
        bpContainerRT.sizeDelta = new Vector2(300f, 80f);

        // Black backdrop with higher opacity
        bpBackdrop = bpContainer.AddComponent<Image>();
        bpBackdrop.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        // BP text with better styling
        GameObject bpTextObj = new GameObject("BPText");
        bpTextObj.transform.SetParent(bpContainer.transform, false);

        RectTransform bpTextRT = bpTextObj.AddComponent<RectTransform>();
        bpTextRT.anchorMin = Vector2.zero;
        bpTextRT.anchorMax = Vector2.one;
        bpTextRT.offsetMin = new Vector2(15f, 10f);
        bpTextRT.offsetMax = new Vector2(-15f, -10f);

        bpAmountText = bpTextObj.AddComponent<TextMeshProUGUI>();
        bpAmountText.text = "BP: 0";
        bpAmountText.fontSize = 48f; // Larger font
        bpAmountText.fontStyle = FontStyles.Bold;
        bpAmountText.color = new Color(1f, 0.85f, 0.2f); // Bright gold for visibility
        bpAmountText.alignment = TextAlignmentOptions.Center;
        bpAmountText.enableAutoSizing = false;

        // Add outline for better readability
        bpAmountText.outlineWidth = 0.25f;
        bpAmountText.outlineColor = new Color32(0, 0, 0, 220);
    }

    private void CreateNextRoundButton()
    {
        // Button container at bottom
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(shopPanel, false);

        RectTransform buttonContainerRT = buttonContainer.AddComponent<RectTransform>();
        buttonContainerRT.anchorMin = new Vector2(0.5f, 0f); // Bottom-center
        buttonContainerRT.anchorMax = new Vector2(0.5f, 0f);
        buttonContainerRT.pivot = new Vector2(0.5f, 0f);
        buttonContainerRT.anchoredPosition = new Vector2(0f, 40f);
        buttonContainerRT.sizeDelta = new Vector2(350f, 80f);

        Image buttonImage = buttonContainer.AddComponent<Image>();
        buttonImage.color = buttonColor;

        nextRoundButton = buttonContainer.AddComponent<Button>();
        nextRoundButton.targetGraphic = buttonImage;

        // Button colors
        ColorBlock colors = nextRoundButton.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(buttonColor.r + 0.1f, buttonColor.g + 0.1f, buttonColor.b + 0.1f);
        colors.pressedColor = new Color(buttonColor.r - 0.1f, buttonColor.g - 0.1f, buttonColor.b - 0.1f);
        nextRoundButton.colors = colors;

        nextRoundButton.onClick.AddListener(OnNextRoundPressed);

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonContainer.transform, false);

        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        nextRoundButtonText = textObj.AddComponent<TextMeshProUGUI>();
        nextRoundButtonText.text = "NEXT ROUND";
        nextRoundButtonText.fontSize = 36;
        nextRoundButtonText.fontStyle = FontStyles.Bold;
        nextRoundButtonText.color = Color.white;
        nextRoundButtonText.alignment = TextAlignmentOptions.Center;
    }

    // Reference to the next round button text for dynamic updates
    private TMP_Text nextRoundButtonText;


    /// <summary>
    /// Show the shop UI.
    /// </summary>
    public void ShowShop()
    {
        Debug.Log("[ShopManager] ShowShop called");
        EnsureUIExists();
        PlayShopMusic();

        // Reset BP text before counting up
        if (bpAmountText != null)
            bpAmountText.text = "BP: 0";

        // Count up BP
        if (bpAmountText != null && RunManager.Instance != null)
        {
            if (countUpCoroutine != null)
                StopCoroutine(countUpCoroutine);
            countUpCoroutine = StartCoroutine(CountUpBPWithDelay(RunManager.Instance.CurrentBP));
        }

        UpdateNextRoundButton();
    }

    /// <summary>
    /// Update the Next Round button text.
    /// </summary>
    private void UpdateNextRoundButton()
    {
        if (nextRoundButtonText == null)
        {
            // Try to find it
            if (nextRoundButton != null)
            {
                nextRoundButtonText = nextRoundButton.GetComponentInChildren<TMP_Text>();
            }
        }

        if (nextRoundButtonText != null)
        {
            nextRoundButtonText.text = "NEXT ROUND";
            // Standard button color
            Image buttonImg = nextRoundButton?.GetComponent<Image>();
            if (buttonImg != null)
            {
                buttonImg.color = buttonColor;
            }
        }
    }

    private void PlayShopMusic()
    {
        if (shopMusic != null)
        {
            // Use custom shop music
            AudioManager.Instance?.PlayMusic(shopMusic);
        }
        else
        {
            // Fallback to menu music
            AudioManager.Instance?.PlayMenuMusic();
        }
    }

    /// <summary>
    /// Hide the shop UI.
    /// </summary>
    public void HideShop()
    {
        Debug.Log("[ShopManager] HideShop called");

        if (countUpCoroutine != null)
        {
            StopCoroutine(countUpCoroutine);
            countUpCoroutine = null;
        }
    }

    /// <summary>
    /// Next Round button pressed.
    /// </summary>
    public void OnNextRoundPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        AudioManager.Instance?.StopMusic();
        CampaignManager.Instance?.AdvanceRound();
        SceneFlowManager.Instance?.TransitionFromShopToGame();
    }


    private IEnumerator CountUpBPWithDelay(int targetBP)
    {
        // Initial delay for dramatic effect
        yield return new WaitForSeconds(bpCountUpDelay);

        float elapsed = 0f;
        int startBP = 0;

        // Play a sound at the start of count-up
        AudioManager.Instance?.PlayButtonClick();

        while (elapsed < bpCountUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bpCountUpDuration;

            // Ease out for satisfying feel
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            int currentBP = Mathf.RoundToInt(Mathf.Lerp(startBP, targetBP, easedT));
            bpAmountText.text = $"BP: {currentBP:N0}";

            yield return null;
        }

        bpAmountText.text = $"BP: {targetBP:N0}";

        // Punch scale on completion
        if (bpAmountText != null)
        {
            StartCoroutine(AnimationUtilities.PunchScale(bpAmountText.transform.parent, 1.15f, 0.2f));
        }

        countUpCoroutine = null;
    }


    /// <summary>
    /// Update the BP display immediately (used when spending BP).
    /// </summary>
    public void RefreshBPDisplay()
    {
        if (bpAmountText != null && RunManager.Instance != null)
        {
            bpAmountText.text = $"BP: {RunManager.Instance.CurrentBP:N0}";
        }
    }
}
