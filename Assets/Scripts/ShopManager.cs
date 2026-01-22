using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the shop UI between rounds.
/// Displays BP balance, upgrade cards, and handles purchases.
/// Auto-generates UI if not manually assigned.
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
    [SerializeField] private float bpFontSize = 36f;

    [Header("Card Settings")]
    [SerializeField] private Vector2 cardSize = new Vector2(520f, 760f);
    [SerializeField] private Color cardBackgroundColor = new Color(0.15f, 0.15f, 0.2f);
    [SerializeField] private Color cardBorderColor = new Color(0.4f, 0.4f, 0.5f);
    [SerializeField] private int cardCount = 3;

    [Header("Animation Settings")]
    [SerializeField] private float bpCountUpDuration = 0.8f;
    [SerializeField] private float bpCountUpDelay = 0.3f;
    [SerializeField] private float cardSpawnDelay = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioClip shopMusic; // Assign in inspector, falls back to menu music if null

    // Placeholder card data
    private readonly string[] placeholderTitles = { "Power Up", "Time Boost", "Multiplier" };
    private readonly string[] placeholderDescriptions = {
        "Increase your base score",
        "Add extra seconds to the clock",
        "Start with a higher multiplier"
    };
    private readonly int[] placeholderCosts = { 50, 75, 100 };

    // Runtime state
    private bool isInitialized = false;
    private Coroutine countUpCoroutine;
    private List<ShopCard> activeCards = new List<ShopCard>();
    private HorizontalLayoutGroup cardsLayoutGroup;
    private ContentSizeFitter cardsSizeFitter;

    // Confirmation popup
    private GameObject confirmationPopup;
    private TMP_Text confirmTitleText;
    private TMP_Text confirmCostText;
    private ShopCard pendingCard;

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

        // Create confirmation popup (hidden by default)
        CreateConfirmationPopup();

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
        // Cards container - centered in screen
        GameObject cardsObj = new GameObject("CardsContainer");
        cardsObj.transform.SetParent(shopPanel, false);

        cardsContainer = cardsObj.AddComponent<RectTransform>();
        // Anchor to center of screen
        cardsContainer.anchorMin = new Vector2(0.5f, 0.5f);
        cardsContainer.anchorMax = new Vector2(0.5f, 0.5f);
        cardsContainer.pivot = new Vector2(0.5f, 0.5f);
        cardsContainer.anchoredPosition = new Vector2(0f, 50f); // Slightly above center
        // Size will expand based on content
        cardsContainer.sizeDelta = new Vector2(800f, cardSize.y + 40f);

        // Add ContentSizeFitter to auto-size width based on children
        cardsSizeFitter = cardsObj.AddComponent<ContentSizeFitter>();
        cardsSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        cardsSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Horizontal layout for cards - centered (will be disabled after spawn)
        cardsLayoutGroup = cardsObj.AddComponent<HorizontalLayoutGroup>();
        cardsLayoutGroup.spacing = 30f;
        cardsLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
        cardsLayoutGroup.childControlWidth = false;
        cardsLayoutGroup.childControlHeight = false;
        cardsLayoutGroup.childForceExpandWidth = false;
        cardsLayoutGroup.childForceExpandHeight = false;
        cardsLayoutGroup.padding = new RectOffset(20, 20, 20, 20);
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

        TMP_Text buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "NEXT ROUND";
        buttonText.fontSize = 36;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;
    }

    private void CreateConfirmationPopup()
    {
        // Fullscreen overlay
        confirmationPopup = new GameObject("ConfirmationPopup");
        confirmationPopup.transform.SetParent(shopPanel, false);

        RectTransform popupRT = confirmationPopup.AddComponent<RectTransform>();
        popupRT.anchorMin = Vector2.zero;
        popupRT.anchorMax = Vector2.one;
        popupRT.offsetMin = Vector2.zero;
        popupRT.offsetMax = Vector2.zero;

        // Semi-transparent background
        Image popupBg = confirmationPopup.AddComponent<Image>();
        popupBg.color = new Color(0f, 0f, 0f, 0.7f);
        popupBg.raycastTarget = true;

        // Dialog box
        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(confirmationPopup.transform, false);

        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRT.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRT.pivot = new Vector2(0.5f, 0.5f);
        dialogRT.sizeDelta = new Vector2(600f, 400f);

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.15f, 0.15f, 0.2f);

        // Title text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);

        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.7f);
        titleRT.anchorMax = new Vector2(1f, 0.95f);
        titleRT.offsetMin = new Vector2(20f, 0f);
        titleRT.offsetMax = new Vector2(-20f, 0f);

        confirmTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        confirmTitleText.text = "Purchase Card?";
        confirmTitleText.fontSize = 42;
        confirmTitleText.fontStyle = FontStyles.Bold;
        confirmTitleText.color = titleColor;
        confirmTitleText.alignment = TextAlignmentOptions.Center;

        // Cost text
        GameObject costObj = new GameObject("Cost");
        costObj.transform.SetParent(dialog.transform, false);

        RectTransform costRT = costObj.AddComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0f, 0.4f);
        costRT.anchorMax = new Vector2(1f, 0.65f);
        costRT.offsetMin = new Vector2(20f, 0f);
        costRT.offsetMax = new Vector2(-20f, 0f);

        confirmCostText = costObj.AddComponent<TextMeshProUGUI>();
        confirmCostText.text = "Cost: 50 BP";
        confirmCostText.fontSize = 36;
        confirmCostText.fontStyle = FontStyles.Normal;
        confirmCostText.color = new Color(1f, 0.85f, 0.2f);
        confirmCostText.alignment = TextAlignmentOptions.Center;

        // Confirm button
        CreatePopupButton(dialog.transform, "ConfirmBtn", "CONFIRM", new Vector2(-100f, -130f),
            new Color(0.2f, 0.7f, 0.3f), OnConfirmPurchase);

        // Cancel button
        CreatePopupButton(dialog.transform, "CancelBtn", "CANCEL", new Vector2(100f, -130f),
            new Color(0.7f, 0.3f, 0.3f), OnCancelPurchase);

        // Hide by default
        confirmationPopup.SetActive(false);
    }

    private void CreatePopupButton(Transform parent, string name, string text, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRT = btnObj.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.5f);
        btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = position;
        btnRT.sizeDelta = new Vector2(180f, 60f);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = color;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(onClick);

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        btnText.fontSize = 28;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// Show the shop UI and populate with cards.
    /// </summary>
    public void ShowShop()
    {
        Debug.Log("[ShopManager] ShowShop called");

        EnsureUIExists();

        // Play shop music (or fallback to menu music)
        PlayShopMusic();

        // Re-enable layout components for fresh card positioning
        if (cardsLayoutGroup != null)
            cardsLayoutGroup.enabled = true;
        if (cardsSizeFitter != null)
            cardsSizeFitter.enabled = true;

        // Reset BP text before counting up
        if (bpAmountText != null)
            bpAmountText.text = "BP: 0";

        // Count up BP from 0 to current total after delay
        if (bpAmountText != null && RunManager.Instance != null)
        {
            if (countUpCoroutine != null)
                StopCoroutine(countUpCoroutine);
            countUpCoroutine = StartCoroutine(CountUpBPWithDelay(RunManager.Instance.CurrentBP));
        }

        // Spawn cards
        SpawnCards();
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

        ClearCards();
    }

    /// <summary>
    /// Next Round button pressed - return to game.
    /// </summary>
    public void OnNextRoundPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        AudioManager.Instance?.StopMusic(); // Stop shop music before transitioning
        SceneFlowManager.Instance?.TransitionFromShopToGame();
    }

    /// <summary>
    /// Called when a card is clicked - shows confirmation popup.
    /// </summary>
    public void OnCardSelected(ShopCard card)
    {
        Debug.Log($"[ShopManager] Card clicked: {card.CardId}, Cost: {card.Cost} BP");

        // Store pending card and show confirmation
        pendingCard = card;
        ShowConfirmationPopup(card);
    }

    private void ShowConfirmationPopup(ShopCard card)
    {
        if (confirmationPopup == null) return;

        // Update popup text
        if (confirmTitleText != null)
            confirmTitleText.text = $"Purchase {card.CardTitle}?";

        if (confirmCostText != null)
            confirmCostText.text = $"Cost: {card.Cost} BP";

        confirmationPopup.SetActive(true);
        AudioManager.Instance?.PlayButtonClick();
    }

    private void OnConfirmPurchase()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (pendingCard == null)
        {
            HideConfirmationPopup();
            return;
        }

        // Check if player can afford
        if (RunManager.Instance != null && RunManager.Instance.SpendBP(pendingCard.Cost))
        {
            Debug.Log($"[ShopManager] Purchased {pendingCard.CardTitle} for {pendingCard.Cost} BP");

            // Remove from active list
            activeCards.Remove(pendingCard);

            // Trigger card's disappear animation
            pendingCard.ConfirmPurchase();

            // Update BP display
            RefreshBPDisplay();
        }
        else
        {
            Debug.Log($"[ShopManager] Cannot afford {pendingCard.CardTitle} - need {pendingCard.Cost} BP");
            // Could show "insufficient funds" feedback here
        }

        pendingCard = null;
        HideConfirmationPopup();
    }

    private void OnCancelPurchase()
    {
        AudioManager.Instance?.PlayButtonClick();
        pendingCard = null;
        HideConfirmationPopup();
    }

    private void HideConfirmationPopup()
    {
        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);
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

    private void SpawnCards()
    {
        ClearCards();

        StartCoroutine(SpawnCardsSequentially());
    }

    private IEnumerator SpawnCardsSequentially()
    {
        for (int i = 0; i < cardCount; i++)
        {
            // Create card
            ShopCard card = ShopCard.CreateCard(
                cardsContainer,
                cardSize,
                cardBackgroundColor,
                cardBorderColor
            );

            // Initialize with placeholder data
            string title = placeholderTitles[i % placeholderTitles.Length];
            string desc = placeholderDescriptions[i % placeholderDescriptions.Length];
            int cost = placeholderCosts[i % placeholderCosts.Length];
            float floatOffset = i * 2.1f; // Different phase for each card

            card.Initialize($"card_{i}", title, desc, cost, floatOffset);

            activeCards.Add(card);

            // Small delay between card spawns for visual effect
            if (cardSpawnDelay > 0 && i < cardCount - 1)
            {
                yield return new WaitForSeconds(cardSpawnDelay);
            }
        }

        // Wait for layout to finalize positions
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        yield return null;

        // Freeze the container's current size before disabling layout
        if (cardsContainer != null)
        {
            // Store the current rect size set by the layout system
            Vector2 finalSize = cardsContainer.rect.size;

            // Disable layout components
            if (cardsSizeFitter != null)
                cardsSizeFitter.enabled = false;
            if (cardsLayoutGroup != null)
                cardsLayoutGroup.enabled = false;

            // Set the size explicitly to maintain it
            cardsContainer.sizeDelta = finalSize;

            // Ensure container stays centered
            cardsContainer.anchoredPosition = new Vector2(0f, 50f);
        }

        Debug.Log($"[ShopManager] Spawned {cardCount} cards, layout frozen");
    }

    private void ClearCards()
    {
        foreach (ShopCard card in activeCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        activeCards.Clear();
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
