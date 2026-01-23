using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Individual shop card with floating animation and click-to-select behavior.
/// Card structure: Title (bold) | Image/Icon | Description | Cost
/// Supports both UpgradeData and SnackData items.
/// </summary>
public class ShopCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum CardType { Upgrade, Snack }

    [Header("Card Content")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image typeBadge;
    [SerializeField] private TMP_Text typeBadgeText;

    [Header("Visual Settings")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Image cardBorder;

    [Header("Float Animation")]
    [SerializeField] private float floatSpeed = 1.2f;
    [SerializeField] private float floatAmount = 4f;

    [Header("Hover Effect")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float hoverDuration = 0.15f;

    [Header("Select Animation")]
    [SerializeField] private float selectScale = 1.3f;
    [SerializeField] private float selectDuration = 0.3f;

    // Runtime state
    private RectTransform rectTransform;
    private Vector2 basePosition;
    private float floatOffset;
    private bool isFloating = true;
    private bool isSelected = false;
    private bool isHovered = false;
    private Coroutine hoverCoroutine;

    // Card data - supports both types
    public string CardId { get; private set; }
    public string CardTitle { get; private set; }
    public int Cost { get; private set; }
    public CardType Type { get; private set; }
    public UpgradeData UpgradeData { get; private set; }
    public SnackData SnackData { get; private set; }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Wait for layout system to position the card before capturing base position
        StartCoroutine(CaptureBasePositionAfterLayout());
    }

    private IEnumerator CaptureBasePositionAfterLayout()
    {
        // Disable floating until we have the correct position
        isFloating = false;

        // Wait for end of frame to let layout system run
        yield return new WaitForEndOfFrame();

        // Force layout rebuild and wait another frame to be safe
        Canvas.ForceUpdateCanvases();
        yield return null;

        // Now capture the position set by the layout group
        basePosition = rectTransform.anchoredPosition;

        // Enable floating animation
        isFloating = true;
    }

    private void Update()
    {
        if (!isFloating || isSelected) return;

        // Gentle floating motion
        float yOffset = Mathf.Sin(Time.time * floatSpeed + floatOffset) * floatAmount;
        rectTransform.anchoredPosition = basePosition + new Vector2(0, yOffset);
    }

    /// <summary>
    /// Initialize the card with an UpgradeData asset.
    /// </summary>
    public void InitializeWithUpgrade(UpgradeData upgrade, float floatPhaseOffset)
    {
        if (upgrade == null) return;

        Type = CardType.Upgrade;
        UpgradeData = upgrade;
        SnackData = null;

        CardId = upgrade.id;
        CardTitle = upgrade.displayName;
        Cost = upgrade.GetEffectiveCost(PlayerInventory.Instance?.GetPriceModifier() ?? 1f);
        floatOffset = floatPhaseOffset;

        if (titleText != null)
            titleText.text = upgrade.displayName;

        if (descriptionText != null)
            descriptionText.text = upgrade.description;

        if (costText != null)
            costText.text = $"{Cost} BP";

        // Set icon or colored placeholder
        SetupIcon(upgrade.icon, GetUpgradeTypeColor(upgrade.upgradeType));

        // Set type badge
        SetupTypeBadge(upgrade.upgradeType.ToString(), GetUpgradeTypeColor(upgrade.upgradeType));

        // Tint card border based on type
        if (cardBorder != null)
            cardBorder.color = GetUpgradeTypeColor(upgrade.upgradeType);
    }

    /// <summary>
    /// Initialize the card with a SnackData asset.
    /// </summary>
    public void InitializeWithSnack(SnackData snack, float floatPhaseOffset)
    {
        if (snack == null) return;

        Type = CardType.Snack;
        SnackData = snack;
        UpgradeData = null;

        CardId = snack.id;
        CardTitle = snack.displayName;
        Cost = snack.GetEffectiveCost(PlayerInventory.Instance?.GetPriceModifier() ?? 1f);
        floatOffset = floatPhaseOffset;

        if (titleText != null)
            titleText.text = snack.displayName;

        if (descriptionText != null)
            descriptionText.text = snack.description;

        if (costText != null)
            costText.text = $"{Cost} BP";

        // Set icon or colored placeholder (snacks use teal/mint color)
        Color snackColor = GetSnackColor();
        SetupIcon(snack.icon, snackColor);

        // Set type badge
        SetupTypeBadge("SNACK", snackColor);

        // Tint card border
        if (cardBorder != null)
            cardBorder.color = snackColor;
    }

    /// <summary>
    /// Legacy initialize for placeholder data (backwards compatibility).
    /// </summary>
    public void Initialize(string id, string title, string description, int cost, float floatPhaseOffset)
    {
        CardId = id;
        CardTitle = title;
        Cost = cost;
        floatOffset = floatPhaseOffset;
        Type = CardType.Upgrade;
        UpgradeData = null;
        SnackData = null;

        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
            descriptionText.text = description;

        if (costText != null)
            costText.text = $"{cost} BP";

        // Icon stays empty/transparent for placeholder
        if (iconImage != null)
            iconImage.color = new Color(1, 1, 1, 0.1f);
    }

    private void SetupIcon(Sprite icon, Color placeholderColor)
    {
        if (iconImage == null) return;

        if (icon != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }
        else
        {
            // Colored placeholder
            iconImage.sprite = null;
            iconImage.color = new Color(placeholderColor.r, placeholderColor.g, placeholderColor.b, 0.3f);
        }
    }

    private void SetupTypeBadge(string typeText, Color color)
    {
        if (typeBadge != null)
        {
            typeBadge.color = color;
            typeBadge.gameObject.SetActive(true);
        }

        if (typeBadgeText != null)
        {
            typeBadgeText.text = typeText;
        }
    }

    private Color GetUpgradeTypeColor(UpgradeType type)
    {
        // Color coding philosophy:
        // - Warm colors (gold, orange, red) for offensive/scoring upgrades
        // - Cool colors (blue, cyan, purple) for defensive/utility upgrades
        // - Neutral (green) for growth/spawning
        return type switch
        {
            UpgradeType.EnhancedNumber => new Color(1f, 0.85f, 0.2f, 1f),    // Gold - ties to BP/value
            UpgradeType.Multiplier => new Color(0.7f, 0.3f, 0.9f, 1f),       // Purple - premium/powerful
            UpgradeType.Time => new Color(0.3f, 0.85f, 0.95f, 1f),           // Cyan - clock/time
            UpgradeType.TileWeight => new Color(0.3f, 0.8f, 0.4f, 1f),       // Green - growth/spawning
            UpgradeType.Combo => new Color(1f, 0.5f, 0.15f, 1f),             // Orange - energy/chains
            UpgradeType.RiskReward => new Color(0.95f, 0.25f, 0.25f, 1f),    // Red - danger/gambling
            UpgradeType.Information => new Color(0.5f, 0.7f, 0.95f, 1f),     // Light blue - knowledge
            UpgradeType.Defensive => new Color(0.4f, 0.65f, 0.5f, 1f),       // Teal - protection
            UpgradeType.BossFight => new Color(0.7f, 0.15f, 0.2f, 1f),       // Crimson - boss combat
            UpgradeType.Special => new Color(0.95f, 0.4f, 0.7f, 1f),         // Pink/Magenta - unique
            _ => new Color(0.5f, 0.5f, 0.55f, 1f)
        };
    }

    /// <summary>
    /// Get the snack card color (consistent teal/mint for consumables).
    /// </summary>
    public static Color GetSnackColor()
    {
        return new Color(0.2f, 0.75f, 0.65f, 1f); // Teal/Mint - refreshing/consumable
    }

    /// <summary>
    /// Create the card UI structure programmatically.
    /// </summary>
    public static ShopCard CreateCard(Transform parent, Vector2 size, Color bgColor, Color borderColor)
    {
        GameObject cardObj = new GameObject("ShopCard");
        cardObj.transform.SetParent(parent, false);

        RectTransform rt = cardObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;

        ShopCard card = cardObj.AddComponent<ShopCard>();

        // Card border (outer frame)
        card.cardBorder = cardObj.AddComponent<Image>();
        card.cardBorder.color = borderColor;

        // Inner background - border thickness proportional to card size
        GameObject innerBg = new GameObject("Background");
        innerBg.transform.SetParent(cardObj.transform, false);
        RectTransform innerRT = innerBg.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        float border = Mathf.Max(4f, size.x * 0.025f); // 2.5% border, min 4px
        innerRT.offsetMin = new Vector2(border, border);
        innerRT.offsetMax = new Vector2(-border, -border);

        card.cardBackground = innerBg.AddComponent<Image>();
        card.cardBackground.color = bgColor;

        // Content container with vertical layout
        GameObject content = new GameObject("Content");
        content.transform.SetParent(innerBg.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        float contentPad = size.x * 0.04f;
        contentRT.offsetMin = new Vector2(contentPad, contentPad);
        contentRT.offsetMax = new Vector2(-contentPad, -contentPad);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = size.y * 0.015f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        int padding = Mathf.RoundToInt(size.x * 0.04f);
        vlg.padding = new RectOffset(padding, padding, padding, padding);

        // === TYPE BADGE (top) ===
        GameObject badgeObj = new GameObject("TypeBadge");
        badgeObj.transform.SetParent(content.transform, false);

        LayoutElement badgeLE = badgeObj.AddComponent<LayoutElement>();
        badgeLE.preferredHeight = size.y * 0.06f;
        badgeLE.flexibleWidth = 1;

        card.typeBadge = badgeObj.AddComponent<Image>();
        card.typeBadge.color = new Color(0.4f, 0.4f, 0.5f, 0.8f);

        GameObject badgeTextObj = new GameObject("BadgeText");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        RectTransform badgeTextRT = badgeTextObj.AddComponent<RectTransform>();
        badgeTextRT.anchorMin = Vector2.zero;
        badgeTextRT.anchorMax = Vector2.one;
        badgeTextRT.offsetMin = Vector2.zero;
        badgeTextRT.offsetMax = Vector2.zero;

        card.typeBadgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        card.typeBadgeText.text = "TYPE";
        card.typeBadgeText.fontSize = size.y * 0.035f;
        card.typeBadgeText.fontStyle = FontStyles.Bold;
        card.typeBadgeText.color = Color.white;
        card.typeBadgeText.alignment = TextAlignmentOptions.Center;

        // === TITLE ===
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(content.transform, false);

        LayoutElement titleLE = titleObj.AddComponent<LayoutElement>();
        titleLE.preferredHeight = size.y * 0.10f;
        titleLE.flexibleWidth = 1;

        card.titleText = titleObj.AddComponent<TextMeshProUGUI>();
        card.titleText.text = "Card Title";
        card.titleText.fontStyle = FontStyles.Bold;
        card.titleText.color = Color.white;
        card.titleText.alignment = TextAlignmentOptions.Center;
        card.titleText.enableAutoSizing = true;
        card.titleText.fontSizeMin = 20;
        card.titleText.fontSizeMax = 56;

        // === ICON AREA ===
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(content.transform, false);

        LayoutElement iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredHeight = size.y * 0.35f;
        iconLE.flexibleHeight = 0;
        iconLE.flexibleWidth = 1;

        card.iconImage = iconObj.AddComponent<Image>();
        card.iconImage.color = new Color(1, 1, 1, 0.15f);
        card.iconImage.preserveAspect = true;

        // === DESCRIPTION ===
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(content.transform, false);

        LayoutElement descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = size.y * 0.28f;
        descLE.flexibleWidth = 1;
        descLE.flexibleHeight = 1;

        card.descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        card.descriptionText.text = "Card description goes here";
        card.descriptionText.fontStyle = FontStyles.Normal;
        card.descriptionText.color = new Color(0.85f, 0.85f, 0.9f);
        card.descriptionText.alignment = TextAlignmentOptions.Center;
        card.descriptionText.enableAutoSizing = true;
        card.descriptionText.fontSizeMin = 16;
        card.descriptionText.fontSizeMax = 32;
        card.descriptionText.textWrappingMode = TextWrappingModes.Normal;

        // === COST ===
        GameObject costObj = new GameObject("Cost");
        costObj.transform.SetParent(content.transform, false);

        LayoutElement costLE = costObj.AddComponent<LayoutElement>();
        costLE.preferredHeight = size.y * 0.09f;
        costLE.flexibleWidth = 1;

        // Cost background
        Image costBg = costObj.AddComponent<Image>();
        costBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        GameObject costTextObj = new GameObject("CostText");
        costTextObj.transform.SetParent(costObj.transform, false);
        RectTransform costTextRT = costTextObj.AddComponent<RectTransform>();
        costTextRT.anchorMin = Vector2.zero;
        costTextRT.anchorMax = Vector2.one;
        costTextRT.offsetMin = Vector2.zero;
        costTextRT.offsetMax = Vector2.zero;

        card.costText = costTextObj.AddComponent<TextMeshProUGUI>();
        card.costText.text = "100 BP";
        card.costText.fontSize = size.y * 0.055f;
        card.costText.fontStyle = FontStyles.Bold;
        card.costText.color = new Color(1f, 0.85f, 0.2f); // Gold
        card.costText.alignment = TextAlignmentOptions.Center;

        return card;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSelected) return;
        SelectCard();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;
        isHovered = true;

        if (hoverCoroutine != null)
            StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateScale(hoverScale, hoverDuration));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return;
        isHovered = false;

        if (hoverCoroutine != null)
            StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateScale(1f, hoverDuration));
    }

    private void SelectCard()
    {
        // Don't mark as selected yet - wait for confirmation
        AudioManager.Instance?.PlayButtonClick();

        // Notify ShopManager to show confirmation popup
        ShopManager.Instance?.OnCardSelected(this);
    }

    /// <summary>
    /// Called by ShopManager when purchase is confirmed.
    /// </summary>
    public void ConfirmPurchase()
    {
        isSelected = true;
        isFloating = false;

        // Play select animation then destroy
        StartCoroutine(SelectAnimation());
    }

    private IEnumerator SelectAnimation()
    {
        // Scale up
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = Vector3.one * selectScale;

        while (elapsed < selectDuration * 0.6f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (selectDuration * 0.6f);
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // Ease out

            transform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            yield return null;
        }

        // Fade out while shrinking slightly
        elapsed = 0f;
        startScale = transform.localScale;
        targetScale = Vector3.one * selectScale * 0.8f;
        CanvasGroup cg = gameObject.AddComponent<CanvasGroup>();

        while (elapsed < selectDuration * 0.4f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (selectDuration * 0.4f);

            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            cg.alpha = 1f - t;
            yield return null;
        }

        // Destroy self
        Destroy(gameObject);
    }

    private IEnumerator AnimateScale(float targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        Vector3 endScale = Vector3.one * targetScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = 1f - Mathf.Pow(1f - t, 2f); // Ease out

            transform.localScale = Vector3.Lerp(startScale, endScale, easedT);
            yield return null;
        }

        transform.localScale = endScale;
        hoverCoroutine = null;
    }

    /// <summary>
    /// Set the base position for floating (call after positioning).
    /// </summary>
    public void SetBasePosition(Vector2 position)
    {
        basePosition = position;
        if (rectTransform != null)
            rectTransform.anchoredPosition = position;
    }
}
