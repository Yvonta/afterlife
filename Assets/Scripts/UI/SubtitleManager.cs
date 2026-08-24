using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Image backgroundPanel; // Optional background panel behind text

    [Header("Settings")]
    [SerializeField] private bool hideBackgroundWhenEmpty = true;
    [SerializeField] private float defaultTimeoutSeconds = 12.0f; // Default 3-second timeout

    private Coroutine activeDisplayRoutine;
    
    // Call this from your game bootstrap/loader script
    public static SubtitleManager Initialize()
    {
        if (Instance != null) return Instance;

        // 1. Create Manager GameObject
        GameObject managerGO = new GameObject("SubtitleManager");
        Instance = managerGO.AddComponent<SubtitleManager>();
        DontDestroyOnLoad(managerGO);

        // 2. Setup Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("SubtitleCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);
        }

        // 3. Create TextMeshPro Element
        GameObject textGO = new GameObject("SubtitleText");
        textGO.transform.SetParent(canvas.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.color = Color.white;

        // Anchor at bottom center of screen
        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.05f);
        rect.anchorMax = new Vector2(0.9f, 0.15f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Assign reference to manager instance
        Instance.subtitleText = tmp;

        return Instance;
    }

    private void Awake()
    {
        // Enforce Singleton Pattern
        if (Instance != null && Instance != UnityEngine.Object.FindAnyObjectByType<SubtitleManager>())
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ClearSubtitle();
    }

    /// <summary>
    /// Displays a subtitle immediately, clearing any active subtitle.
    /// Times out after duration (defaults to 3 seconds).
    /// </summary>
    /// <param name="message">The text to display.</param>
    /// <param name="duration">Timeout in seconds. Defaults to 3 seconds.</param>
    /// <param name="textColor">Optional text color (defaults to white).</param>
    public void DisplaySubtitle(string message, float duration = -1f, Color? textColor = null)
    {
        // If no custom duration provided, fall back to default 3 seconds
        float showDuration = (duration > 0f) ? duration : defaultTimeoutSeconds;

        // Cancel the current timer if a message is already showing
        if (activeDisplayRoutine != null)
        {
            StopCoroutine(activeDisplayRoutine);
        }

        // Display the new message immediately with the timeout
        activeDisplayRoutine = StartCoroutine(ShowSubtitleRoutine(message, showDuration, textColor ?? Color.white));
    }

    /// <summary>
    /// Instantly clears the subtitle text on screen.
    /// </summary>
    public void ClearSubtitle()
    {
        if (activeDisplayRoutine != null)
        {
            StopCoroutine(activeDisplayRoutine);
            activeDisplayRoutine = null;
        }

        if (subtitleText != null)
        {
            subtitleText.text = string.Empty;
        }

        SetVisibility(false);
    }

    private IEnumerator ShowSubtitleRoutine(string message, float duration, Color textColor)
    {
        subtitleText.color = textColor;
        subtitleText.text = message;
        SetVisibility(true);

        // Wait for 3 seconds (or custom duration) before hiding
        yield return new WaitForSeconds(duration);

        ClearSubtitle();
    }

    private void SetVisibility(bool visible)
    {
        if (subtitleText != null)
        {
            subtitleText.enabled = visible;
        }

        if (backgroundPanel != null && hideBackgroundWhenEmpty)
        {
            backgroundPanel.enabled = visible;
        }
    }
}