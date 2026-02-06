using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the shop UI between rounds.
/// Displays BP balance, upgrade/snack cards, and handles purchases.
/// Loads real data from Assets/Data/ folders.
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

    [Header("Card Settings")]
    [SerializeField] private Vector2 upgradeCardSize = new Vector2(260f, 300f);
    [SerializeField] private Vector2 snackCardSize = new Vector2(240f, 280f);
    [SerializeField] private Color cardBackgroundColor = new Color(0.12f, 0.12f, 0.18f);
    [SerializeField] private Color cardBorderColor = new Color(0.4f, 0.4f, 0.5f);

    [Header("Pyramid Layout Settings")]
    [SerializeField] private int topRowUpgrades = 2;      // Premium/rare upgrades
    [SerializeField] private int middleRowUpgrades = 2;   // Standard upgrades
    [SerializeField] private int bottomRowSnacks = 2;     // Consumable snacks
    [SerializeField] private float rowSpacing = 30f;
    [SerializeField] private float cardSpacingHorizontal = 25f;
    [SerializeField] private float pyramidTopOffset = 120f; // How far down from center the top row starts

    [Header("Shop Pool Settings")]
    [Tooltip("Only show items available for current stage")]
    [SerializeField] private bool filterByStage = true;

    [Header("Animation Settings")]
    [SerializeField] private float bpCountUpDuration = 0.8f;
    [SerializeField] private float bpCountUpDelay = 0.3f;
    [SerializeField] private float cardSpawnDelay = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioClip shopMusic;

    // Data pools - loaded at runtime
    private List<UpgradeData> availableUpgrades = new List<UpgradeData>();
    private List<SnackData> availableSnacks = new List<SnackData>();
    private bool dataLoaded = false;

    // Runtime state
    private bool isInitialized = false;
    private Coroutine countUpCoroutine;
    private List<ShopCard> activeCards = new List<ShopCard>();

    // Track what's been offered this shop visit (to avoid duplicates)
    private HashSet<string> offeredThisVisit = new HashSet<string>();

    // Legacy confirmation popup (replaced by UpgradeConfirmWindow but kept for fallback)
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
        LoadShopData();
        EnsureUIExists();
    }

    /// <summary>
    /// Load all upgrade and snack data from Assets/Data folders.
    /// </summary>
    private void LoadShopData()
    {
        if (dataLoaded) return;

        availableUpgrades = DataLoader.LoadUpgrades();
        availableSnacks = DataLoader.LoadSnacks();

        dataLoaded = true;
        Debug.Log($"[ShopManager] Loaded {availableUpgrades.Count} upgrades and {availableSnacks.Count} snacks");
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

        // Ensure data is loaded
        LoadShopData();
        EnsureUIExists();

        // Clear tracking for this shop visit
        offeredThisVisit.Clear();

        // Play shop music (or fallback to menu music)
        PlayShopMusic();

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

        // Update button text based on campaign state
        UpdateNextRoundButton();

        // Spawn cards with real data
        SpawnCards();
    }

    /// <summary>
    /// Update the Next Round button text based on campaign state.
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
            if (CampaignManager.Instance != null && CampaignManager.Instance.AreAllRoundsComplete())
            {
                nextRoundButtonText.text = "☕ CHILL ZONE";
                // Change button color to indicate chill zone
                Image buttonImg = nextRoundButton?.GetComponent<Image>();
                if (buttonImg != null)
                {
                    buttonImg.color = new Color(0.4f, 0.6f, 0.8f); // Calm blue
                }
            }
            else
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
    /// Next Round button pressed - return to game or go to chill zone.
    /// </summary>
    public void OnNextRoundPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        AudioManager.Instance?.StopMusic(); // Stop shop music before transitioning

        // Check if all rounds in this stage are complete (time for boss)
        if (CampaignManager.Instance != null && CampaignManager.Instance.AreAllRoundsComplete())
        {
            Debug.Log("[ShopManager] All rounds complete - transitioning to Chill Zone");
            SceneFlowManager.Instance?.TransitionToChillZone();
        }
        else
        {
            // Normal flow - next round
            CampaignManager.Instance?.AdvanceRound();
            SceneFlowManager.Instance?.TransitionFromShopToGame();
        }
    }

    /// <summary>
    /// Called when a card is clicked - shows confirmation popup.
    /// Uses UpgradeConfirmWindow for upgrades, legacy popup for snacks.
    /// </summary>
    public void OnCardSelected(ShopCard card)
    {
        Debug.Log($"[ShopManager] Card clicked: {card.CardId}, Type: {card.Type}, Cost: {card.Cost} BP");

        pendingCard = card;

        // Use the fancy UpgradeConfirmWindow for upgrades
        if (card.Type == ShopCard.CardType.Upgrade && card.UpgradeData != null && UpgradeConfirmWindow.Instance != null)
        {
            UpgradeConfirmWindow.Instance.ShowUpgrade(
                card.UpgradeData,
                onConfirm: () => CompletePurchase(card),
                onCancel: () => { pendingCard = null; }
            );
        }
        // For snacks or if UpgradeConfirmWindow isn't available, use legacy popup
        else
        {
            ShowConfirmationPopup(card);
        }
    }

    /// <summary>
    /// Complete the purchase of an item.
    /// </summary>
    private void CompletePurchase(ShopCard card)
    {
        if (card == null) return;

        int cost = card.Cost;

        // Check if player can afford
        if (RunManager.Instance == null || !RunManager.Instance.SpendBP(cost))
        {
            Debug.Log($"[ShopManager] Cannot afford {card.CardTitle} - need {cost} BP");
            return;
        }

        Debug.Log($"[ShopManager] Purchased {card.CardTitle} for {cost} BP");

        // Add to inventory based on type
        bool success = false;
        if (card.Type == ShopCard.CardType.Upgrade && card.UpgradeData != null)
        {
            success = PlayerInventory.Instance?.AddUpgrade(card.UpgradeData) ?? false;
        }
        else if (card.Type == ShopCard.CardType.Snack && card.SnackData != null)
        {
            success = PlayerInventory.Instance?.AddSnack(card.SnackData) ?? false;
        }

        if (success)
        {
            Debug.Log($"[ShopManager] Added {card.CardTitle} to inventory");

            // If an upgrade was purchased, refresh all tiles' enhanced status
            // (in case it was an EnhancedNumber upgrade)
            if (card.Type == ShopCard.CardType.Upgrade)
            {
                Tile.RefreshAllEnhancedStatus();
            }
        }
        else
        {
            Debug.LogWarning($"[ShopManager] Failed to add {card.CardTitle} to inventory!");
            // Refund the BP if add failed
            RunManager.Instance?.AddBP(cost);
            return;
        }

        // Remove from active list
        activeCards.Remove(card);

        // Trigger card's disappear animation
        card.ConfirmPurchase();

        // Update BP display
        RefreshBPDisplay();

        pendingCard = null;
    }

    private void ShowConfirmationPopup(ShopCard card)
    {
        if (confirmationPopup == null) return;

        // Update popup text
        if (confirmTitleText != null)
        {
            string typeLabel = card.Type == ShopCard.CardType.Snack ? "Snack" : "Upgrade";
            confirmTitleText.text = $"Purchase {typeLabel}?\n{card.CardTitle}";
        }

        if (confirmCostText != null)
            confirmCostText.text = $"Cost: {card.Cost} BP";

        confirmationPopup.SetActive(true);
        AudioManager.Instance?.PlayButtonClick();
    }

    private void OnConfirmPurchase()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (pendingCard != null)
        {
            CompletePurchase(pendingCard);
        }

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
        int currentStage = CampaignManager.Instance?.CurrentStage ?? 1;
        int cardIndex = 0;

        // Calculate total height needed
        float totalHeight = upgradeCardSize.y + rowSpacing + upgradeCardSize.y + rowSpacing + snackCardSize.y;
        float startY = totalHeight / 2f - upgradeCardSize.y / 2f;

        // ===== TOP ROW: Premium/Rare Upgrades (2 cards) =====
        float topRowY = startY;
        float topRowWidth = topRowUpgrades * upgradeCardSize.x + (topRowUpgrades - 1) * cardSpacingHorizontal;
        float topRowStartX = -topRowWidth / 2f + upgradeCardSize.x / 2f;

        for (int i = 0; i < topRowUpgrades; i++)
        {
            ShopCard card = ShopCard.CreateCard(
                cardsContainer,
                upgradeCardSize,
                cardBackgroundColor,
                cardBorderColor
            );

            // Position the card
            RectTransform cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            float xPos = topRowStartX + i * (upgradeCardSize.x + cardSpacingHorizontal);
            cardRT.anchoredPosition = new Vector2(xPos, topRowY);

            float floatOffset = cardIndex * 2.1f;
            UpgradeData upgrade = GetRandomAvailableUpgrade(currentStage);
            if (upgrade != null)
            {
                card.InitializeWithUpgrade(upgrade, floatOffset);
                offeredThisVisit.Add(upgrade.id);
                Debug.Log($"[ShopManager] Top Row {i}: {upgrade.displayName}");
            }
            else
            {
                card.Initialize($"empty_{cardIndex}", "Sold Out", "No upgrades available", 0, floatOffset);
            }

            activeCards.Add(card);
            cardIndex++;

            if (cardSpawnDelay > 0)
                yield return new WaitForSeconds(cardSpawnDelay);
        }

        // ===== MIDDLE ROW: Standard Upgrades (2 cards) =====
        float middleRowY = topRowY - upgradeCardSize.y - rowSpacing;
        float middleRowWidth = middleRowUpgrades * upgradeCardSize.x + (middleRowUpgrades - 1) * cardSpacingHorizontal;
        float middleRowStartX = -middleRowWidth / 2f + upgradeCardSize.x / 2f;

        for (int i = 0; i < middleRowUpgrades; i++)
        {
            ShopCard card = ShopCard.CreateCard(
                cardsContainer,
                upgradeCardSize,
                cardBackgroundColor,
                cardBorderColor
            );

            RectTransform cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            float xPos = middleRowStartX + i * (upgradeCardSize.x + cardSpacingHorizontal);
            cardRT.anchoredPosition = new Vector2(xPos, middleRowY);

            float floatOffset = cardIndex * 2.1f;
            UpgradeData upgrade = GetRandomAvailableUpgrade(currentStage);
            if (upgrade != null)
            {
                card.InitializeWithUpgrade(upgrade, floatOffset);
                offeredThisVisit.Add(upgrade.id);
                Debug.Log($"[ShopManager] Middle Row {i}: {upgrade.displayName}");
            }
            else
            {
                card.Initialize($"empty_{cardIndex}", "Sold Out", "No upgrades available", 0, floatOffset);
            }

            activeCards.Add(card);
            cardIndex++;

            if (cardSpawnDelay > 0)
                yield return new WaitForSeconds(cardSpawnDelay);
        }

        // ===== BOTTOM ROW: Snacks (2 cards) =====
        float bottomRowY = middleRowY - upgradeCardSize.y - rowSpacing;
        float bottomRowWidth = bottomRowSnacks * snackCardSize.x + (bottomRowSnacks - 1) * cardSpacingHorizontal;
        float bottomRowStartX = -bottomRowWidth / 2f + snackCardSize.x / 2f;

        for (int i = 0; i < bottomRowSnacks; i++)
        {
            ShopCard card = ShopCard.CreateCard(
                cardsContainer,
                snackCardSize,
                cardBackgroundColor,
                cardBorderColor
            );

            RectTransform cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            float xPos = bottomRowStartX + i * (snackCardSize.x + cardSpacingHorizontal);
            cardRT.anchoredPosition = new Vector2(xPos, bottomRowY);

            float floatOffset = cardIndex * 2.1f;
            SnackData snack = GetRandomAvailableSnack(currentStage);
            if (snack != null)
            {
                card.InitializeWithSnack(snack, floatOffset);
                offeredThisVisit.Add(snack.id);
                Debug.Log($"[ShopManager] Bottom Row {i}: Snack - {snack.displayName}");
            }
            else
            {
                // Fall back to upgrade if no snacks
                UpgradeData upgrade = GetRandomAvailableUpgrade(currentStage);
                if (upgrade != null)
                {
                    card.InitializeWithUpgrade(upgrade, floatOffset);
                    offeredThisVisit.Add(upgrade.id);
                }
                else
                {
                    card.Initialize($"empty_{cardIndex}", "Sold Out", "Nothing available", 0, floatOffset);
                }
            }

            activeCards.Add(card);
            cardIndex++;

            if (cardSpawnDelay > 0)
                yield return new WaitForSeconds(cardSpawnDelay);
        }

        // Wait for layout to finalize
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        yield return null;

        int totalCards = topRowUpgrades + middleRowUpgrades + bottomRowSnacks;
        Debug.Log($"[ShopManager] Spawned {totalCards} cards in pyramid layout (Top: {topRowUpgrades}, Middle: {middleRowUpgrades}, Bottom: {bottomRowSnacks})");
    }

    /// <summary>
    /// Get a random upgrade that's available for the current stage and not already offered.
    /// </summary>
    private UpgradeData GetRandomAvailableUpgrade(int currentStage)
    {
        var candidates = availableUpgrades
            .Where(u => u != null)
            .Where(u => !offeredThisVisit.Contains(u.id))
            .Where(u => !filterByStage || u.IsAvailableInStage(currentStage))
            .Where(u => !PlayerInventory.Instance?.HasUpgrade(u.id) ?? true || u.isStackable)
            .ToList();

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Get a random snack that's available and not already owned.
    /// </summary>
    private SnackData GetRandomAvailableSnack(int currentStage)
    {
        var candidates = availableSnacks
            .Where(s => s != null)
            .Where(s => !offeredThisVisit.Contains(s.id))
            .Where(s => !filterByStage || s.IsAvailableInStage(currentStage))
            .Where(s => !PlayerInventory.Instance?.HasSnack(s.id) ?? true) // Snacks are unique
            .ToList();

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
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
