using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Individual shop card with floating animation and click-to-select behavior.
/// Card structure: Title (bold) | Image (empty) | Description
/// </summary>
public class ShopCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Card Content")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text descriptionText;

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

    // Card data
    public string CardId { get; private set; }
    public string CardTitle { get; private set; }
    public int Cost { get; private set; }

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
    /// Initialize the card with data.
    /// </summary>
    public void Initialize(string id, string title, string description, int cost, float floatPhaseOffset)
    {
        CardId = id;
        CardTitle = title;
        Cost = cost;
        floatOffset = floatPhaseOffset;

        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
            descriptionText.text = $"{description}\n\n<color=#FFD700>Cost: {cost} BP</color>";

        // Icon stays empty/transparent as requested
        if (iconImage != null)
            iconImage.color = new Color(1, 1, 1, 0.1f); // Very faint placeholder
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
        float border = Mathf.Max(3f, size.x * 0.02f); // 2% border, min 3px
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
        float contentPad = size.x * 0.04f; // 4% content padding
        contentRT.offsetMin = new Vector2(contentPad, contentPad);
        contentRT.offsetMax = new Vector2(-contentPad, -contentPad);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = size.y * 0.02f; // 2% of card height
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        int padding = Mathf.RoundToInt(size.x * 0.05f); // 5% padding
        vlg.padding = new RectOffset(padding, padding, padding, padding);

        // Title - uses auto-sizing to fit card width
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(content.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();

        LayoutElement titleLE = titleObj.AddComponent<LayoutElement>();
        titleLE.preferredHeight = size.y * 0.12f; // 12% of card height
        titleLE.flexibleWidth = 1;

        card.titleText = titleObj.AddComponent<TextMeshProUGUI>();
        card.titleText.text = "Card Title";
        card.titleText.fontStyle = FontStyles.Bold;
        card.titleText.color = Color.white;
        card.titleText.alignment = TextAlignmentOptions.Center;
        card.titleText.enableAutoSizing = true;
        card.titleText.fontSizeMin = 18;
        card.titleText.fontSizeMax = 72;

        // Icon area (empty/transparent) - flexible middle section
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(content.transform, false);
        RectTransform iconRT = iconObj.AddComponent<RectTransform>();

        LayoutElement iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredHeight = size.y * 0.45f; // 45% of card height
        iconLE.flexibleHeight = 1;
        iconLE.flexibleWidth = 1;

        card.iconImage = iconObj.AddComponent<Image>();
        card.iconImage.color = new Color(1, 1, 1, 0.1f); // Faint placeholder

        // Description - uses auto-sizing to fit card width
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(content.transform, false);
        RectTransform descRT = descObj.AddComponent<RectTransform>();

        LayoutElement descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = size.y * 0.25f; // 25% of card height
        descLE.flexibleWidth = 1;

        card.descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        card.descriptionText.text = "Card description goes here";
        card.descriptionText.fontStyle = FontStyles.Normal;
        card.descriptionText.color = new Color(0.8f, 0.8f, 0.8f);
        card.descriptionText.alignment = TextAlignmentOptions.Center;
        card.descriptionText.enableAutoSizing = true;
        card.descriptionText.fontSizeMin = 14;
        card.descriptionText.fontSizeMax = 48;
        card.descriptionText.textWrappingMode = TextWrappingModes.Normal;

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
