using UnityEngine;

/// <summary>
/// Enforces a fixed aspect ratio by adjusting the camera's viewport rect.
/// Creates natural letterbox/pillarbox bars using the camera's background color.
/// Attach to the Main Camera.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraLetterbox : MonoBehaviour
{
    [Header("Target Resolution")]
    [SerializeField] private float targetWidth = 1024f;
    [SerializeField] private float targetHeight = 768f;
    
    [Header("Bar Color")]
    [SerializeField] private Color barColor = Color.black;
    
    private Camera mainCamera;
    private Camera backgroundCamera;
    private float targetAspect;
    
    void Awake()
    {
        Initialize();
    }
    
    void Start()
    {
        Initialize();
    }
    
    void Initialize()
    {
        mainCamera = GetComponent<Camera>();
        targetAspect = targetWidth / targetHeight;
        
        // Create background camera for the letterbox bars
        CreateBackgroundCamera();
        
        // Apply the letterbox
        UpdateLetterbox();
    }
    
    void CreateBackgroundCamera()
    {
        // Check if background camera already exists
        GameObject bgCamObj = GameObject.Find("LetterboxBackgroundCamera");
        
        if (bgCamObj == null)
        {
            bgCamObj = new GameObject("LetterboxBackgroundCamera");
            bgCamObj.transform.SetParent(transform.parent);
        }
        
        backgroundCamera = bgCamObj.GetComponent<Camera>();
        if (backgroundCamera == null)
        {
            backgroundCamera = bgCamObj.AddComponent<Camera>();
        }
        
        // Configure background camera
        backgroundCamera.depth = mainCamera.depth - 1; // Render behind main camera
        backgroundCamera.cullingMask = 0; // Don't render anything
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = barColor;
        backgroundCamera.orthographic = true;
    }
    
    void Update()
    {
        UpdateLetterbox();
    }
    
    void UpdateLetterbox()
    {
        if (mainCamera == null) return;
        
        float screenAspect = (float)Screen.width / Screen.height;
        float scaleHeight = screenAspect / targetAspect;
        
        Rect rect = new Rect();
        
        if (scaleHeight < 1.0f)
        {
            // Screen is taller than target - add letterbox (top/bottom bars)
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
        }
        else
        {
            // Screen is wider than target - add pillarbox (left/right bars)
            float scaleWidth = 1.0f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
        }
        
        mainCamera.rect = rect;
        
        // Update background camera color if changed
        if (backgroundCamera != null)
        {
            backgroundCamera.backgroundColor = barColor;
        }
    }
    
    void OnValidate()
    {
        if (mainCamera != null)
        {
            targetAspect = targetWidth / targetHeight;
            UpdateLetterbox();
        }
    }
    
    void OnDestroy()
    {
        // Clean up background camera in play mode
        if (Application.isPlaying && backgroundCamera != null)
        {
            Destroy(backgroundCamera.gameObject);
        }
    }
}
