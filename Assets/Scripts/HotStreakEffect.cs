using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hot Streak fire effect for the multiplier panel.
/// Creates rising flames, floating embers, color pulses, and a hovering panel.
/// Intensity scales with multiplier value - higher multiplier = more fire!
/// 
/// SETUP: Add this component to your MultiplierPanel GameObject.
/// It will auto-find references if not assigned.
/// The panel will appear wherever you position it in the editor.
/// </summary>
public class HotStreakEffect : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [SerializeField] private RectTransform panelTransform;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private Image barFillImage;
    
    [Header("Flame Settings")]
    [SerializeField] private int baseFlameCount = 8;
    [SerializeField] private int maxFlameCount = 20;
    [SerializeField] private float flameSpawnRate = 0.08f;
    [SerializeField] private float flameLifetime = 0.6f;
    [SerializeField] private float flameRiseSpeed = 120f;
    [SerializeField] private float flameWobbleAmount = 15f;
    [SerializeField] private Vector2 flameSizeRange = new Vector2(12f, 24f);
    
    [Header("Ember Settings")]
    [SerializeField] private int baseEmberCount = 4;
    [SerializeField] private int maxEmberCount = 12;
    [SerializeField] private float emberSpawnRate = 0.15f;
    [SerializeField] private float emberLifetime = 1.2f;
    [SerializeField] private float emberRiseSpeed = 60f;
    [SerializeField] private float emberDriftAmount = 30f;
    [SerializeField] private Vector2 emberSizeRange = new Vector2(4f, 8f);
    
    [Header("Panel Float Settings")]
    [SerializeField] private float floatAmount = 6f;
    [SerializeField] private float floatSpeed = 3f;
    [SerializeField] private float shakeIntensity = 2f;
    [SerializeField] private float shakeSpeed = 25f;
    
    [Header("Color Settings")]
    [SerializeField] private Color flameCore = new Color(1f, 0.95f, 0.4f);
    [SerializeField] private Color flameMid = new Color(1f, 0.5f, 0.1f);
    [SerializeField] private Color flameEdge = new Color(0.9f, 0.2f, 0.1f);
    [SerializeField] private Color emberColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private Color barPulseCoolColor = new Color(1f, 0.5f, 0.1f);   // Orange
    [SerializeField] private Color barPulseHotColor = new Color(0.8f, 0.1f, 0.6f);  // Purple/magenta
    [SerializeField] private float colorPulseSpeed = 4f;
    
    // Runtime state
    private bool isActive = false;
    private float currentIntensity = 0f;
    private Vector2 panelOriginalPosition;
    private bool hasOriginalPosition = false;
    private Color barOriginalColor;
    private RectTransform flameContainer;
    
    private List<FlameParticle> flames = new List<FlameParticle>();
    private List<EmberParticle> embers = new List<EmberParticle>();
    private List<FlameParticle> textFlames = new List<FlameParticle>();
    
    private Coroutine flameSpawnerCoroutine;
    private Coroutine emberSpawnerCoroutine;
    private Coroutine textFlameCoroutine;
    
    private class FlameParticle
    {
        public RectTransform transform;
        public Image image;
        public float lifetime;
        public float maxLifetime;
        public float wobbleOffset;
        public float startX;
    }
    
    private class EmberParticle
    {
        public RectTransform transform;
        public Image image;
        public float lifetime;
        public float maxLifetime;
        public float driftDirection;
        public float twinkleOffset;
    }
    
    private void Awake()
    {
        // Auto-find references
        if (panelTransform == null)
            panelTransform = GetComponent<RectTransform>();
        
        if (multiplierText == null)
            multiplierText = GetComponentInChildren<TMP_Text>();
        
        if (barFillImage == null)
        {
            // Try to find a slider's fill
            var slider = GetComponentInChildren<Slider>();
            if (slider != null && slider.fillRect != null)
                barFillImage = slider.fillRect.GetComponent<Image>();
        }
    }
    
    private void Start()
    {
        // DON'T capture panel position here - we capture it in Activate()
        // This ensures the panel appears wherever the user placed it in the editor
        
        if (barFillImage != null)
            barOriginalColor = barFillImage.color;
        
        // Create a container for flame particles (renders behind panel content)
        CreateFlameContainer();
    }
    
    private void CreateFlameContainer()
    {
        // Create container as first child (renders behind everything else in panel)
        GameObject containerObj = new GameObject("FlameContainer");
        containerObj.transform.SetParent(panelTransform, false);
        containerObj.transform.SetAsFirstSibling();
        
        flameContainer = containerObj.AddComponent<RectTransform>();
        flameContainer.anchorMin = Vector2.zero;
        flameContainer.anchorMax = Vector2.one;
        flameContainer.offsetMin = Vector2.zero;
        flameContainer.offsetMax = Vector2.zero;
    }
    
    private void Update()
    {
        if (!isActive) return;
        
        float time = Time.time;
        
        // Panel floating & shake
        if (panelTransform != null && hasOriginalPosition)
        {
            float floatY = Mathf.Sin(time * floatSpeed) * floatAmount * currentIntensity;
            float shakeX = Mathf.Sin(time * shakeSpeed) * shakeIntensity * currentIntensity;
            float shakeY = Mathf.Cos(time * shakeSpeed * 1.3f) * shakeIntensity * 0.5f * currentIntensity;
            
            panelTransform.anchoredPosition = panelOriginalPosition + new Vector2(shakeX, floatY + shakeY);
            
            float rotWobble = Mathf.Sin(time * floatSpeed * 0.7f) * 1.5f * currentIntensity;
            panelTransform.localEulerAngles = new Vector3(0, 0, rotWobble);
        }
        
        // Bar color pulse - intensity affects how hot the color gets
        if (barFillImage != null)
        {
            float pulse = (Mathf.Sin(time * colorPulseSpeed) + 1f) / 2f;
            
            // Base color shifts from cool (orange) to hot (purple) based on intensity
            Color baseColor = Color.Lerp(barPulseCoolColor, barPulseHotColor, currentIntensity);
            
            // Pulse between the base color and a brighter version
            Color brightColor = Color.Lerp(baseColor, Color.white, 0.3f);
            Color pulseColor = Color.Lerp(baseColor, brightColor, pulse);
            
            barFillImage.color = pulseColor;
        }
        
        UpdateFlames();
        UpdateEmbers();
        UpdateTextFlames();
    }
    
    private void UpdateFlames()
    {
        float time = Time.time;
        
        for (int i = flames.Count - 1; i >= 0; i--)
        {
            FlameParticle flame = flames[i];
            if (flame.transform == null)
            {
                flames.RemoveAt(i);
                continue;
            }
            
            flame.lifetime -= Time.deltaTime;
            
            if (flame.lifetime <= 0)
            {
                Destroy(flame.transform.gameObject);
                flames.RemoveAt(i);
                continue;
            }
            
            float lifePercent = flame.lifetime / flame.maxLifetime;
            float deathPercent = 1f - lifePercent;
            
            // Rise upward
            Vector2 pos = flame.transform.anchoredPosition;
            pos.y += flameRiseSpeed * Time.deltaTime;
            
            // Wobble side to side
            float wobble = Mathf.Sin(time * 8f + flame.wobbleOffset) * flameWobbleAmount * lifePercent;
            pos.x = flame.startX + wobble;
            
            flame.transform.anchoredPosition = pos;
            
            // Scale: grows then shrinks
            float scaleMultiplier = lifePercent < 0.3f 
                ? Mathf.Lerp(0.3f, 1f, lifePercent / 0.3f)
                : Mathf.Lerp(1f, 0f, (deathPercent - 0.3f) / 0.7f);
            flame.transform.localScale = Vector3.one * scaleMultiplier;
            
            // Color gradient: yellow → orange → red
            Color flameColor;
            if (lifePercent > 0.6f)
                flameColor = Color.Lerp(flameMid, flameCore, (lifePercent - 0.6f) / 0.4f);
            else if (lifePercent > 0.3f)
                flameColor = Color.Lerp(flameEdge, flameMid, (lifePercent - 0.3f) / 0.3f);
            else
                flameColor = flameEdge;
            
            flameColor.a = Mathf.Clamp01(lifePercent * 2f);
            flame.image.color = flameColor;
        }
    }
    
    private void UpdateEmbers()
    {
        float time = Time.time;
        
        for (int i = embers.Count - 1; i >= 0; i--)
        {
            EmberParticle ember = embers[i];
            if (ember.transform == null)
            {
                embers.RemoveAt(i);
                continue;
            }
            
            ember.lifetime -= Time.deltaTime;
            
            if (ember.lifetime <= 0)
            {
                Destroy(ember.transform.gameObject);
                embers.RemoveAt(i);
                continue;
            }
            
            float lifePercent = ember.lifetime / ember.maxLifetime;
            
            // Drift upward and sideways
            Vector2 pos = ember.transform.anchoredPosition;
            pos.y += emberRiseSpeed * Time.deltaTime;
            pos.x += ember.driftDirection * emberDriftAmount * Time.deltaTime;
            ember.transform.anchoredPosition = pos;
            
            // Twinkle
            float twinkle = (Mathf.Sin(time * 20f + ember.twinkleOffset) + 1f) / 2f;
            float alpha = lifePercent * (0.5f + twinkle * 0.5f);
            
            Color c = emberColor;
            c.a = alpha;
            ember.image.color = c;
            
            // Shrink
            float scale = Mathf.Lerp(0.3f, 1f, lifePercent);
            ember.transform.localScale = Vector3.one * scale;
        }
    }
    
    private void UpdateTextFlames()
    {
        if (multiplierText == null) return;
        
        float time = Time.time;
        
        for (int i = textFlames.Count - 1; i >= 0; i--)
        {
            FlameParticle flame = textFlames[i];
            if (flame.transform == null)
            {
                textFlames.RemoveAt(i);
                continue;
            }
            
            flame.lifetime -= Time.deltaTime;
            
            if (flame.lifetime <= 0)
            {
                Destroy(flame.transform.gameObject);
                textFlames.RemoveAt(i);
                continue;
            }
            
            float lifePercent = flame.lifetime / flame.maxLifetime;
            
            Vector2 pos = flame.transform.anchoredPosition;
            pos.y += flameRiseSpeed * 0.7f * Time.deltaTime;
            pos.x = flame.startX + Mathf.Sin(time * 10f + flame.wobbleOffset) * 8f * lifePercent;
            flame.transform.anchoredPosition = pos;
            
            float scale = lifePercent * 0.8f;
            flame.transform.localScale = Vector3.one * scale;
            
            Color c = Color.Lerp(flameEdge, flameCore, lifePercent);
            c.a = lifePercent;
            flame.image.color = c;
        }
    }
    
    public void Activate(float multiplier)
    {
        if (!isActive)
        {
            isActive = true;
            
            // Capture original position NOW when the panel is being shown
            // This respects wherever the user placed it in the editor
            if (panelTransform != null)
            {
                panelOriginalPosition = panelTransform.anchoredPosition;
                hasOriginalPosition = true;
            }
            
            if (flameContainer == null)
                CreateFlameContainer();
            
            flameSpawnerCoroutine = StartCoroutine(FlameSpawnerLoop());
            emberSpawnerCoroutine = StartCoroutine(EmberSpawnerLoop());
            textFlameCoroutine = StartCoroutine(TextFlameSpawnerLoop());
        }
        
        currentIntensity = Mathf.Clamp01((multiplier - 1.25f) / 1.75f);
    }
    
    public void UpdateIntensity(float multiplier)
    {
        currentIntensity = Mathf.Clamp01((multiplier - 1.25f) / 1.75f);
    }
    
    public void Deactivate()
    {
        isActive = false;
        
        if (flameSpawnerCoroutine != null)
            StopCoroutine(flameSpawnerCoroutine);
        if (emberSpawnerCoroutine != null)
            StopCoroutine(emberSpawnerCoroutine);
        if (textFlameCoroutine != null)
            StopCoroutine(textFlameCoroutine);
        
        // Clean up particles
        foreach (var flame in flames)
            if (flame.transform != null)
                Destroy(flame.transform.gameObject);
        flames.Clear();
        
        foreach (var ember in embers)
            if (ember.transform != null)
                Destroy(ember.transform.gameObject);
        embers.Clear();
        
        foreach (var flame in textFlames)
            if (flame.transform != null)
                Destroy(flame.transform.gameObject);
        textFlames.Clear();
        
        // Reset panel to its original position
        if (panelTransform != null && hasOriginalPosition)
        {
            panelTransform.anchoredPosition = panelOriginalPosition;
            panelTransform.localEulerAngles = Vector3.zero;
        }
        
        if (barFillImage != null)
            barFillImage.color = barOriginalColor;
    }
    
    private IEnumerator FlameSpawnerLoop()
    {
        while (isActive)
        {
            int flameCount = Mathf.RoundToInt(Mathf.Lerp(baseFlameCount, maxFlameCount, currentIntensity));
            
            if (flames.Count < flameCount)
                SpawnFlame();
            
            float adjustedRate = flameSpawnRate / (1f + currentIntensity);
            yield return new WaitForSeconds(adjustedRate);
        }
    }
    
    private IEnumerator EmberSpawnerLoop()
    {
        while (isActive)
        {
            int emberCount = Mathf.RoundToInt(Mathf.Lerp(baseEmberCount, maxEmberCount, currentIntensity));
            
            if (embers.Count < emberCount)
                SpawnEmber();
            
            float adjustedRate = emberSpawnRate / (1f + currentIntensity * 0.5f);
            yield return new WaitForSeconds(adjustedRate);
        }
    }
    
    private IEnumerator TextFlameSpawnerLoop()
    {
        while (isActive)
        {
            if (multiplierText != null && currentIntensity > 0.2f)
                SpawnTextFlame();
            
            yield return new WaitForSeconds(0.1f / (1f + currentIntensity));
        }
    }
    
    private void SpawnFlame()
    {
        if (flameContainer == null) return;
        
        GameObject flameObj = new GameObject("Flame");
        flameObj.transform.SetParent(flameContainer, false);
        
        RectTransform rt = flameObj.AddComponent<RectTransform>();
        
        // Get panel dimensions
        Rect panelRect = panelTransform.rect;
        
        // Spawn along the bottom of the panel
        float spawnX = Random.Range(-panelRect.width / 2f * 0.9f, panelRect.width / 2f * 0.9f);
        float spawnY = -panelRect.height / 2f;
        
        rt.anchoredPosition = new Vector2(spawnX, spawnY);
        
        float size = Random.Range(flameSizeRange.x, flameSizeRange.y);
        rt.sizeDelta = new Vector2(size, size * 1.5f);
        
        Image img = flameObj.AddComponent<Image>();
        img.color = flameCore;
        img.raycastTarget = false;
        
        FlameParticle flame = new FlameParticle
        {
            transform = rt,
            image = img,
            lifetime = flameLifetime * Random.Range(0.8f, 1.2f),
            maxLifetime = flameLifetime,
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
            startX = spawnX
        };
        
        flames.Add(flame);
    }
    
    private void SpawnEmber()
    {
        if (flameContainer == null) return;
        
        GameObject emberObj = new GameObject("Ember");
        emberObj.transform.SetParent(flameContainer, false);
        
        RectTransform rt = emberObj.AddComponent<RectTransform>();
        
        Rect panelRect = panelTransform.rect;
        
        // Spawn around the panel area
        float spawnX = Random.Range(-panelRect.width / 2f, panelRect.width / 2f);
        float spawnY = Random.Range(-panelRect.height / 2f, panelRect.height / 4f);
        
        rt.anchoredPosition = new Vector2(spawnX, spawnY);
        
        float size = Random.Range(emberSizeRange.x, emberSizeRange.y);
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond
        
        Image img = emberObj.AddComponent<Image>();
        img.color = emberColor;
        img.raycastTarget = false;
        
        EmberParticle ember = new EmberParticle
        {
            transform = rt,
            image = img,
            lifetime = emberLifetime * Random.Range(0.7f, 1.3f),
            maxLifetime = emberLifetime,
            driftDirection = Random.Range(-1f, 1f),
            twinkleOffset = Random.Range(0f, Mathf.PI * 2f)
        };
        
        embers.Add(ember);
    }
    
    private void SpawnTextFlame()
    {
        if (multiplierText == null) return;
        
        GameObject flameObj = new GameObject("TextFlame");
        flameObj.transform.SetParent(multiplierText.transform, false);
        
        RectTransform rt = flameObj.AddComponent<RectTransform>();
        
        Rect textRect = multiplierText.rectTransform.rect;
        float spawnX = Random.Range(-textRect.width / 2f, textRect.width / 2f);
        float spawnY = textRect.height / 2f;
        
        rt.anchoredPosition = new Vector2(spawnX, spawnY);
        rt.sizeDelta = new Vector2(8f, 12f);
        
        Image img = flameObj.AddComponent<Image>();
        img.color = flameCore;
        img.raycastTarget = false;
        
        FlameParticle flame = new FlameParticle
        {
            transform = rt,
            image = img,
            lifetime = 0.4f,
            maxLifetime = 0.4f,
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
            startX = spawnX
        };
        
        textFlames.Add(flame);
    }
    
    private void OnDisable()
    {
        Deactivate();
    }
    
    private void OnDestroy()
    {
        // Clean up the flame container
        if (flameContainer != null)
            Destroy(flameContainer.gameObject);
    }
}
