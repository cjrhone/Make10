using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Dreamy parallax background for the game panel.
/// Creates layered floating elements: deep "10"s, mid-layer math symbols/numbers,
/// and near-layer geometric shapes. All procedurally generated, seamlessly looping.
/// 
/// Creative vision: A mathematical dreamscape that whispers the game's theme
/// without shouting over the puzzle grid.
/// 
/// SETUP: 
/// 1. Add this script to an empty GameObject as a SIBLING to your grid (not parent/child)
/// 2. Place it ABOVE the grid in hierarchy (renders behind)
/// 3. Or: Add to a panel with a Canvas component, set Sort Order to -1
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private RectTransform container;
    [SerializeField] private Vector2 bounds = new Vector2(1200f, 900f);
    
    [Header("Deep Layer - The Tens")]
    [SerializeField] private int deepCount = 6;
    [SerializeField] private float deepSpeed = 15f;
    [SerializeField] private float deepScale = 120f;
    [SerializeField] private float deepAlpha = 0.08f;
    [SerializeField] private Color deepColor = new Color(0.4f, 0.4f, 0.5f);
    
    [Header("Mid Layer - Numbers & Symbols")]
    [SerializeField] private int midCount = 20;
    [SerializeField] private float midSpeedMin = 25f;
    [SerializeField] private float midSpeedMax = 45f;
    [SerializeField] private float midScaleMin = 24f;
    [SerializeField] private float midScaleMax = 40f;
    [SerializeField] private float midAlpha = 0.10f;
    
    [Header("Near Layer - Geometric Whispers")]
    [SerializeField] private int nearCount = 12;
    [SerializeField] private float nearSpeedMin = 50f;
    [SerializeField] private float nearSpeedMax = 70f;
    [SerializeField] private float nearScaleMin = 16f;
    [SerializeField] private float nearScaleMax = 28f;
    [SerializeField] private float nearAlpha = 0.06f;
    
    [Header("Movement")]
    [SerializeField] private Vector2 driftDirection = new Vector2(-1f, -0.3f);
    [SerializeField] private float verticalWobbleAmount = 20f;
    [SerializeField] private float wobbleSpeed = 0.5f;
    [SerializeField] private float rotationDrift = 5f;
    
    [Header("Activation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool generateOnEnable = false;
    
    private class FloatingElement
    {
        public RectTransform transform;
        public TMP_Text text;
        public Image image;
        public float speed;
        public float wobbleOffset;
        public float rotationSpeed;
        public Vector2 basePosition;
        public int layer;
    }
    
    private List<FloatingElement> elements = new List<FloatingElement>();
    private bool isGenerated = false;
    
    private readonly string[] mathSymbols = { "+", "=", "×", "·" };
    private readonly string[] gameNumbers = { "0", "1", "2", "3", "4", "5", "6" };
    
    private readonly Color[] numberTints = new Color[]
    {
        new Color(0.5f, 0.5f, 0.5f),
        new Color(0.7f, 0.55f, 0.2f),
        new Color(0.25f, 0.4f, 0.7f),
        new Color(0.3f, 0.6f, 0.35f),
        new Color(0.7f, 0.3f, 0.3f),
        new Color(0.75f, 0.45f, 0.2f),
        new Color(0.5f, 0.3f, 0.6f)
    };
    
    private void Start()
    {
        if (container == null)
            container = GetComponent<RectTransform>();
        
        if (generateOnStart && !isGenerated)
            GenerateElements();
    }
    
    private void OnEnable()
    {
        if (generateOnEnable && !isGenerated && container != null)
            GenerateElements();
    }
    
    private void Update()
    {
        if (elements.Count == 0) return;
        
        float time = Time.time;
        
        foreach (var element in elements)
        {
            if (element.transform == null) continue;
            
            Vector2 drift = driftDirection.normalized * element.speed * Time.deltaTime;
            
            float wobble = Mathf.Sin(time * wobbleSpeed + element.wobbleOffset) * verticalWobbleAmount * 0.01f;
            Vector2 perpendicular = new Vector2(-driftDirection.y, driftDirection.x).normalized;
            drift += perpendicular * wobble * element.speed * Time.deltaTime;
            
            element.basePosition += drift;
            element.transform.anchoredPosition = element.basePosition;
            
            if (element.rotationSpeed != 0)
            {
                float currentRot = element.transform.localEulerAngles.z;
                element.transform.localEulerAngles = new Vector3(0, 0, currentRot + element.rotationSpeed * Time.deltaTime);
            }
            
            WrapElement(element);
        }
    }
    
    /// <summary>
    /// Public method to trigger generation (call from SceneFlowManager if needed).
    /// </summary>
    public void Initialize()
    {
        if (!isGenerated)
            GenerateElements();
    }
    
    private void GenerateElements()
    {
        if (container == null)
        {
            Debug.LogError("ParallaxBackground: No container assigned!");
            return;
        }
        
        ClearElements();
        
        // Deep layer: The sacred "10"s
        for (int i = 0; i < deepCount; i++)
        {
            CreateTextElement("10", 0, deepScale, deepSpeed, deepColor, deepAlpha);
        }
        
        // Mid layer: Numbers and math symbols
        for (int i = 0; i < midCount; i++)
        {
            float speed = Random.Range(midSpeedMin, midSpeedMax);
            float scale = Random.Range(midScaleMin, midScaleMax);
            
            if (Random.value < 0.6f)
            {
                int numIndex = Random.Range(0, gameNumbers.Length);
                string num = gameNumbers[numIndex];
                Color tint = numberTints[numIndex];
                CreateTextElement(num, 1, scale, speed, tint, midAlpha);
            }
            else
            {
                string symbol = mathSymbols[Random.Range(0, mathSymbols.Length)];
                Color symbolColor = new Color(0.5f, 0.5f, 0.55f);
                CreateTextElement(symbol, 1, scale * 0.9f, speed, symbolColor, midAlpha * 0.8f);
            }
        }
        
        // Near layer: Geometric tile echoes
        for (int i = 0; i < nearCount; i++)
        {
            float speed = Random.Range(nearSpeedMin, nearSpeedMax);
            float scale = Random.Range(nearScaleMin, nearScaleMax);
            CreateSquareElement(2, scale, speed, nearAlpha);
        }
        
        // Equation whispers
        for (int i = 0; i < 3; i++)
        {
            float speed = Random.Range(midSpeedMin, midSpeedMax);
            CreateEquationElement(1, speed);
        }
        
        isGenerated = true;
        Debug.Log($"ParallaxBackground: Created {elements.Count} floating elements");
    }
    
    private void CreateTextElement(string content, int layer, float fontSize, float speed, Color color, float alpha)
    {
        GameObject obj = new GameObject($"Parallax_{content}_{elements.Count}");
        obj.transform.SetParent(container, false);
        
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(fontSize * 2f, fontSize * 1.5f);
        
        Vector2 startPos = new Vector2(
            Random.Range(-bounds.x / 2f, bounds.x / 2f),
            Random.Range(-bounds.y / 2f, bounds.y / 2f)
        );
        rt.anchoredPosition = startPos;
        rt.localEulerAngles = new Vector3(0, 0, Random.Range(-15f, 15f));
        
        TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(color.r, color.g, color.b, alpha);
        text.raycastTarget = false;
        
        FloatingElement element = new FloatingElement
        {
            transform = rt,
            text = text,
            speed = speed,
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
            rotationSpeed = Random.Range(-rotationDrift, rotationDrift),
            basePosition = startPos,
            layer = layer
        };
        
        elements.Add(element);
    }
    
    private void CreateSquareElement(int layer, float size, float speed, float alpha)
    {
        GameObject obj = new GameObject($"Parallax_Square_{elements.Count}");
        obj.transform.SetParent(container, false);
        
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        
        Vector2 startPos = new Vector2(
            Random.Range(-bounds.x / 2f, bounds.x / 2f),
            Random.Range(-bounds.y / 2f, bounds.y / 2f)
        );
        rt.anchoredPosition = startPos;
        rt.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 45f));
        
        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.6f, 0.6f, 0.65f, alpha);
        img.raycastTarget = false;
        
        FloatingElement element = new FloatingElement
        {
            transform = rt,
            image = img,
            speed = speed,
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
            rotationSpeed = Random.Range(-rotationDrift * 2f, rotationDrift * 2f),
            basePosition = startPos,
            layer = layer
        };
        
        elements.Add(element);
    }
    
    private void CreateEquationElement(int layer, float speed)
    {
        string equation = GenerateEquationThatMakesTen();
        
        GameObject obj = new GameObject($"Parallax_Equation_{elements.Count}");
        obj.transform.SetParent(container, false);
        
        RectTransform rt = obj.AddComponent<RectTransform>();
        float fontSize = Random.Range(16f, 22f);
        rt.sizeDelta = new Vector2(fontSize * equation.Length * 0.6f, fontSize * 1.5f);
        
        Vector2 startPos = new Vector2(
            Random.Range(-bounds.x / 2f, bounds.x / 2f),
            Random.Range(-bounds.y / 2f, bounds.y / 2f)
        );
        rt.anchoredPosition = startPos;
        
        TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
        text.text = equation;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.5f, 0.55f, 0.5f, midAlpha * 0.7f);
        text.raycastTarget = false;
        
        FloatingElement element = new FloatingElement
        {
            transform = rt,
            text = text,
            speed = speed * 0.8f,
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f),
            rotationSpeed = Random.Range(-rotationDrift * 0.3f, rotationDrift * 0.3f),
            basePosition = startPos,
            layer = layer
        };
        
        elements.Add(element);
    }
    
    private string GenerateEquationThatMakesTen()
    {
        int[] nums = new int[5];
        int sum = 0;
        
        for (int i = 0; i < 4; i++)
        {
            nums[i] = Random.Range(0, 5);
            sum += nums[i];
        }
        
        int needed = 10 - sum;
        if (needed < 0 || needed > 6)
            return "2+2+2+2+2";
        
        nums[4] = needed;
        
        for (int i = nums.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (nums[i], nums[j]) = (nums[j], nums[i]);
        }
        
        return $"{nums[0]}+{nums[1]}+{nums[2]}+{nums[3]}+{nums[4]}";
    }
    
    private void WrapElement(FloatingElement element)
    {
        Vector2 pos = element.basePosition;
        bool wrapped = false;
        float buffer = 100f;
        
        if (pos.x < -bounds.x / 2f - buffer)
        {
            pos.x = bounds.x / 2f + buffer;
            wrapped = true;
        }
        else if (pos.x > bounds.x / 2f + buffer)
        {
            pos.x = -bounds.x / 2f - buffer;
            wrapped = true;
        }
        
        if (pos.y < -bounds.y / 2f - buffer)
        {
            pos.y = bounds.y / 2f + buffer;
            wrapped = true;
        }
        else if (pos.y > bounds.y / 2f + buffer)
        {
            pos.y = -bounds.y / 2f - buffer;
            wrapped = true;
        }
        
        if (wrapped)
        {
            element.basePosition = pos;
            element.transform.anchoredPosition = pos;
            
            if (Mathf.Abs(driftDirection.x) > Mathf.Abs(driftDirection.y))
            {
                element.basePosition = new Vector2(pos.x, Random.Range(-bounds.y / 2f, bounds.y / 2f));
            }
        }
    }
    
    private void ClearElements()
    {
        foreach (var element in elements)
        {
            if (element.transform != null)
                Destroy(element.transform.gameObject);
        }
        elements.Clear();
        isGenerated = false;
    }
    
    private void OnDestroy()
    {
        ClearElements();
    }
    
    [ContextMenu("Regenerate Elements")]
    public void Regenerate()
    {
        ClearElements();
        GenerateElements();
    }
}
