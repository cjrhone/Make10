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
        // Type badge may not exist in simplified floating box layout
        if (typeBadge != null)
        {
            typeBadge.color = color;
            typeBadge.gameObject.SetActive(true);
        }

        if (typeBadgeText != null)
        {
            typeBadgeText.text = typeText;
        }

        // Tint the accent bar with the type color
        if (cardBorder != null)
        {
            cardBorder.color = color;
        }
    }

    private Color GetUpgradeTypeColor(UpgradeType type) => UIStyleGuide.GetUpgradeTypeColor(type);

    /// <summary>
    /// Get the snack card color (consistent teal/mint for consumables).
    /// </summary>
    public static Color GetSnackColor() => UIStyleGuide.GetSnackColor();

    /// <summary>
    /// Create a simplified floating box card: icon/placeholder + title + cost.
    /// Clicking opens the confirmation window for full details.
    /// </summary>
    public static ShopCard CreateCard(Transform parent, Vector2 size, Color bgColor, Color borderColor)
    {
        GameObject cardObj = new GameObject("ShopCard");
        cardObj.transform.SetParent(parent, false);

        RectTransform rt = cardObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;

        ShopCard card = cardObj.AddComponent<ShopCard>();

        // Simple rounded-feel background (no separate border frame)
        card.cardBackground = cardObj.AddComponent<Image>();
        card.cardBackground.color = bgColor;

        // Thin accent bar at top for type color
        GameObject accentBar = new GameObject("AccentBar");
        accentBar.transform.SetParent(cardObj.transform, false);
        RectTransform accentRT = accentBar.AddComponent<RectTransform>();
        accentRT.anchorMin = new Vector2(0, 1);
        accentRT.anchorMax = new Vector2(1, 1);
        accentRT.pivot = new Vector2(0.5f, 1);
        accentRT.offsetMin = new Vector2(0, -6f);
        accentRT.offsetMax = Vector2.zero;
        card.cardBorder = accentBar.AddComponent<Image>();
        card.cardBorder.color = borderColor;

        // Content container with vertical layout
        GameObject content = new GameObject("Content");
        content.transform.SetParent(cardObj.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = new Vector2(8f, 8f);
        contentRT.offsetMax = new Vector2(-8f, -10f);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        // === ICON / PLACEHOLDER (large, central focus) ===
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(content.transform, false);

        LayoutElement iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredHeight = size.y * 0.50f;
        iconLE.flexibleHeight = 0;
        iconLE.flexibleWidth = 1;

        card.iconImage = iconObj.AddComponent<Image>();
        card.iconImage.color = new Color(1, 1, 1, 0.15f);
        card.iconImage.preserveAspect = true;

        // === TITLE (compact) ===
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(content.transform, false);

        LayoutElement titleLE = titleObj.AddComponent<LayoutElement>();
        titleLE.preferredHeight = size.y * 0.18f;
        titleLE.flexibleWidth = 1;

        card.titleText = titleObj.AddComponent<TextMeshProUGUI>();
        card.titleText.text = "Item";
        card.titleText.fontStyle = FontStyles.Bold;
        card.titleText.color = Color.white;
        card.titleText.alignment = TextAlignmentOptions.Center;
        card.titleText.enableAutoSizing = true;
        card.titleText.fontSizeMin = 16;
        card.titleText.fontSizeMax = 36;

        // === COST (small, at bottom) ===
        GameObject costObj = new GameObject("Cost");
        costObj.transform.SetParent(content.transform, false);

        LayoutElement costLE = costObj.AddComponent<LayoutElement>();
        costLE.preferredHeight = size.y * 0.12f;
        costLE.flexibleWidth = 1;

        card.costText = costObj.AddComponent<TextMeshProUGUI>();
        card.costText.text = "100 BP";
        card.costText.fontSize = size.y * 0.065f;
        card.costText.fontStyle = FontStyles.Bold;
        card.costText.color = new Color(1f, 0.85f, 0.2f); // Gold
        card.costText.alignment = TextAlignmentOptions.Center;

        // Hidden fields not used in simple box but kept for compatibility
        card.descriptionText = null;
        card.typeBadge = null;
        card.typeBadgeText = null;

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
