using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UiVoiceCloning : MonoBehaviour
{
    // Triggered strictly when the user clicks the Upload button
    // Passes: audioBytes, targetSentence, voiceName
    public event Action<byte[], string, string> OnAudioRecorded;

    [Header("Recording Settings")]
    [SerializeField] private int maxRecordingDuration = 30;
    [SerializeField] private int sampleRate = 44100;
    [SerializeField] private float volumeSensitivity = 10f;

    [Header("Phonetic Sentence")]
    [TextArea(3, 5)]
    [SerializeField] private string targetSentence = 
        "The quick brown fox jumps over the lazy dog, while five boxing wizards jump quickly with vibrant joy.";

    // UI References
    private CanvasGroup canvasGroup;
    private GameObject panelObj;
    private Text sentenceText;
    private InputField voiceNameInput;
    private Button recordButton;
    private Text recordButtonText;
    private Button previewButton;
    private Text previewButtonText;
    private Button uploadButton;
    private Text uploadButtonText;
    private Slider volumeMeterSlider;
    private AudioSource previewAudioSource;

    // Recording State
    private AudioClip recordedClip;
    private AudioClip trimmedClip;
    private byte[] cachedWavBytes;
    private string selectedMicrophone;
    private bool isRecording = false;

    private void Awake()
    {
        EnsureEventSystem();
        SetupAudioSource();
    }

    private void Start()
    {
        if (sentenceText != null)
        {
            sentenceText.text = targetSentence;
        }

        if (Microphone.devices.Length > 0)
        {
            selectedMicrophone = Microphone.devices[0];
            Debug.Log($"[UiVoiceCloning] Selected Microphone: {selectedMicrophone}");
            
            SetupButtonListeners();
            UpdateButtonText("Start Recording");
        }
        else
        {
            Debug.LogError("[UiVoiceCloning] No microphone input devices found!");
            UpdateButtonText("No Mic Found");
            DisableAllButtons();
        }
    }

    private void Update()
    {
        if (isRecording)
        {
            UpdateVolumeMeter();
        }
    }

    // --- Dynamic UI Construction ---

    public void BuildUI(Transform parentTransform)
    {
        if (panelObj != null) return;

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

        // Main Panel
        panelObj = new GameObject("VoiceCloningPanel");
        panelObj.transform.SetParent(parentTransform, false);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(600, 560);
        
        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        canvasGroup = panelObj.AddComponent<CanvasGroup>();

        // Sentence Display Text
        GameObject sentenceObj = new GameObject("SentenceText");
        sentenceObj.transform.SetParent(panelObj.transform, false);
        RectTransform sentenceRect = sentenceObj.AddComponent<RectTransform>();
        sentenceRect.anchoredPosition = new Vector2(0, 180);
        sentenceRect.sizeDelta = new Vector2(520, 100);
        sentenceText = sentenceObj.AddComponent<Text>();
        sentenceText.font = defaultFont;
        sentenceText.fontSize = 18;
        sentenceText.alignment = TextAnchor.MiddleCenter;
        sentenceText.color = Color.white;
        sentenceText.text = targetSentence;

        // Voice Name Input Field
        GameObject nameObj = new GameObject("VoiceNameInput");
        nameObj.transform.SetParent(panelObj.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchoredPosition = new Vector2(0, 100);
        nameRect.sizeDelta = new Vector2(300, 40);

        Image nameBg = nameObj.AddComponent<Image>();
        nameBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        voiceNameInput = nameObj.AddComponent<InputField>();

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(nameObj.transform, false);
        RectTransform phRect = placeholderObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.sizeDelta = Vector2.zero;
        Text phText = placeholderObj.AddComponent<Text>();
        phText.font = defaultFont;
        phText.fontSize = 14;
        phText.text = "Enter Voice Name...";
        phText.alignment = TextAnchor.MiddleCenter;
        phText.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(nameObj.transform, false);
        RectTransform textInputRect = inputTextObj.AddComponent<RectTransform>();
        textInputRect.anchorMin = Vector2.zero;
        textInputRect.anchorMax = Vector2.one;
        textInputRect.sizeDelta = Vector2.zero;
        Text inText = inputTextObj.AddComponent<Text>();
        inText.font = defaultFont;
        inText.fontSize = 16;
        inText.alignment = TextAnchor.MiddleCenter;
        inText.color = Color.white;

        voiceNameInput.textComponent = inText;
        voiceNameInput.placeholder = phText;
        voiceNameInput.text = "Samanta";

        // Volume Meter Slider
        GameObject sliderObj = new GameObject("VolumeMeter");
        sliderObj.transform.SetParent(panelObj.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(0, 40);
        sliderRect.sizeDelta = new Vector2(400, 16);

        volumeMeterSlider = sliderObj.AddComponent<Slider>();
        volumeMeterSlider.interactable = false;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.3f, 1f);

        volumeMeterSlider.fillRect = fillRect;

        // Action Buttons
        recordButton = CreateUIButton(panelObj.transform, "RecordButton", "Start Recording", new Vector2(0, -30), new Vector2(240, 45), defaultFont, out recordButtonText);
        previewButton = CreateUIButton(panelObj.transform, "PreviewButton", "Play Preview", new Vector2(0, -90), new Vector2(240, 40), defaultFont, out previewButtonText);
        uploadButton = CreateUIButton(panelObj.transform, "UploadButton", "Upload Voice", new Vector2(0, -150), new Vector2(240, 45), defaultFont, out uploadButtonText);

        // Customize Upload Button styling
        ColorBlock uploadColors = uploadButton.colors;
        uploadColors.normalColor = new Color(0.15f, 0.55f, 0.25f, 1f);
        uploadColors.highlightedColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        uploadColors.pressedColor = new Color(0.1f, 0.4f, 0.2f, 1f);
        uploadColors.disabledColor = new Color(0.2f, 0.3f, 0.2f, 0.4f);
        uploadButton.colors = uploadColors;

        if (Microphone.devices.Length > 0)
        {
            selectedMicrophone = Microphone.devices[0];
            SetupButtonListeners();
            previewButton.interactable = false;
            uploadButton.interactable = false;
        }
    }

    private void SetupButtonListeners()
    {
        if (recordButton != null)
        {
            recordButton.onClick.RemoveAllListeners();
            recordButton.onClick.AddListener(ToggleRecording);
        }
        if (previewButton != null)
        {
            previewButton.onClick.RemoveAllListeners();
            previewButton.onClick.AddListener(PlayRecordedPreview);
        }
        if (uploadButton != null)
        {
            uploadButton.onClick.RemoveAllListeners();
            uploadButton.onClick.AddListener(UploadRecordedAudio);
        }
    }

    private void DisableAllButtons()
    {
        if (recordButton != null) recordButton.interactable = false;
        if (previewButton != null) previewButton.interactable = false;
        if (uploadButton != null) uploadButton.interactable = false;
    }

    public void SetVisible(bool isVisible)
    {
        if (canvasGroup == null && panelObj != null)
        {
            canvasGroup = panelObj.GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }
        else if (panelObj != null)
        {
            panelObj.SetActive(isVisible);
        }
    }

    public void UpdateButtonText(string text)
    {
        if (recordButtonText != null)
        {
            recordButtonText.text = text;
        }
    }

    public void SetRecordButtonInteractable(bool interactable)
    {
        if (recordButton != null) recordButton.interactable = interactable;
    }

    public void SetUploadButtonInteractable(bool interactable)
    {
        if (uploadButton != null) uploadButton.interactable = interactable;
    }

    // --- Recording & Processing ---

    public void ToggleRecording()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopAndProcessRecording();
        }
    }

    private void StartRecording()
    {
        if (string.IsNullOrEmpty(selectedMicrophone)) return;
        if (Microphone.IsRecording(selectedMicrophone)) return;

        if (previewAudioSource != null && previewAudioSource.isPlaying)
        {
            previewAudioSource.Stop();
        }

        if (previewButton != null) previewButton.interactable = false;
        if (uploadButton != null) uploadButton.interactable = false;

        cachedWavBytes = null;
        isRecording = true;
        UpdateButtonText("Stop Recording");

        recordedClip = Microphone.Start(selectedMicrophone, false, maxRecordingDuration, sampleRate);
        Debug.Log("[UiVoiceCloning] Recording started...");
        StartCoroutine(AutoStopRoutine(maxRecordingDuration));
    }

    private void StopAndProcessRecording()
    {
        if (!isRecording) return;

        int recordPosition = Microphone.GetPosition(selectedMicrophone);
        Microphone.End(selectedMicrophone);
        isRecording = false;

        ResetVolumeMeter();
        UpdateButtonText("Processing...");
        recordButton.interactable = false;

        Debug.Log($"[UiVoiceCloning] Stopped recording. Total samples: {recordPosition}");

        if (recordPosition <= 0)
        {
            Debug.LogWarning("[UiVoiceCloning] Recorded audio contains 0 samples!");
            UpdateButtonText("Start Recording");
            recordButton.interactable = true;
            return;
        }

        trimmedClip = TrimClipToActualSamples(recordedClip, recordPosition);

        if (previewAudioSource != null)
        {
            previewAudioSource.clip = trimmedClip;
        }

        // Encode and cache WAV bytes locally
        cachedWavBytes = EncodeToWav(trimmedClip);

        // Re-enable record, preview, and upload buttons
        UpdateButtonText("Start Recording");
        recordButton.interactable = true;
        if (previewButton != null) previewButton.interactable = true;
        if (uploadButton != null) uploadButton.interactable = true;
    }

    private void PlayRecordedPreview()
    {
        if (previewAudioSource == null)
        {
            SetupAudioSource();
        }

        if (trimmedClip == null)
        {
            Debug.LogWarning("[UiVoiceCloning] No trimmed clip available for preview!");
            return;
        }

        previewAudioSource.clip = trimmedClip;
        previewAudioSource.Stop();
        previewAudioSource.Play();
        Debug.Log($"[UiVoiceCloning] Playing preview ({trimmedClip.length:F2} seconds)...");
    }

    private void UploadRecordedAudio()
    {
        if (cachedWavBytes == null || cachedWavBytes.Length == 0)
        {
            Debug.LogWarning("[UiVoiceCloning] No recorded WAV data available to upload!");
            return;
        }

        string voiceName = voiceNameInput != null && !string.IsNullOrEmpty(voiceNameInput.text)
            ? voiceNameInput.text 
            : "DefaultVoice";

        Debug.Log($"[UiVoiceCloning] Upload button clicked for voice '{voiceName}'...");

        // Fire event to Main.cs
        OnAudioRecorded?.Invoke(cachedWavBytes, targetSentence, voiceName);
    }

    private void UpdateVolumeMeter()
    {
        if (volumeMeterSlider == null || recordedClip == null || string.IsNullOrEmpty(selectedMicrophone)) return;

        int micPosition = Microphone.GetPosition(selectedMicrophone);
        int sampleWindow = 128;
        
        if (micPosition < sampleWindow) return;

        float[] waveData = new float[sampleWindow];
        recordedClip.GetData(waveData, micPosition - sampleWindow);

        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            sum += waveData[i] * waveData[i];
        }

        float rmsVolume = Mathf.Sqrt(sum / sampleWindow);
        volumeMeterSlider.value = Mathf.Clamp01(rmsVolume * volumeSensitivity);
    }

    private void ResetVolumeMeter()
    {
        if (volumeMeterSlider != null)
        {
            volumeMeterSlider.value = 0f;
        }
    }

    private AudioClip TrimClipToActualSamples(AudioClip sourceClip, int actualSamplesRecorded)
    {
        if (actualSamplesRecorded <= 0) return sourceClip;

        float[] sampleBuffer = new float[actualSamplesRecorded * sourceClip.channels];
        sourceClip.GetData(sampleBuffer, 0);

        AudioClip trimmed = AudioClip.Create(
            sourceClip.name + "_trimmed", 
            actualSamplesRecorded, 
            sourceClip.channels, 
            sourceClip.frequency, 
            false
        );

        trimmed.SetData(sampleBuffer, 0);
        return trimmed;
    }

    private IEnumerator AutoStopRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isRecording)
        {
            StopAndProcessRecording();
        }
    }

    private void SetupAudioSource()
    {
        previewAudioSource = GetComponent<AudioSource>();
        if (previewAudioSource == null)
        {
            previewAudioSource = gameObject.AddComponent<AudioSource>();
        }
        previewAudioSource.playOnAwake = false;
        previewAudioSource.spatialBlend = 0f; // 2D Sound
        previewAudioSource.volume = 1f;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }
    }

    private Button CreateUIButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Font font, out Text buttonText)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        Button btn = btnObj.AddComponent<Button>();

        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        buttonText = textObj.AddComponent<Text>();
        buttonText.font = font;
        buttonText.fontSize = 16;
        buttonText.text = label;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;

        return btn;
    }

    private byte[] EncodeToWav(AudioClip clip)
    {
        int totalSamples = clip.samples * clip.channels;
        int pcmByteCount = totalSamples * 2;

        using (MemoryStream stream = new MemoryStream(44 + pcmByteCount))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            float[] samples = new float[totalSamples];
            clip.GetData(samples, 0);

            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + pcmByteCount);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((ushort)1); // PCM
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((ushort)(clip.channels * 2));
            writer.Write((ushort)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(pcmByteCount);

            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short sample = (short)(clamped * 32767f);
                writer.Write(sample);
            }

            return stream.ToArray();
        }
    }
}