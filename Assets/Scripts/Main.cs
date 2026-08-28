using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Donkey;
using GLTFast;
using Yvonta.UI;

public class Main : MonoBehaviour
{
    [Header("Server Endpoints")]
    [SerializeField] private string jsonRpcUrl = "https://ultireal.com/appapi/v2/xbot.php";
    [SerializeField] private string avatarGenUrl = "https://ultireal.com/appapi/v2/avatargen.php";
    [SerializeField] private string clothingUrl = "https://ultireal.com/appapi/v2/clothing.php";
    [SerializeField] private string hairUrl = "https://ultireal.com/appapi/v2/hair.php";
    [SerializeField] private string sttUrl = "https://ultireal.com/appapi/v2/stt.php";
    [SerializeField] private string ttsUrl = "https://ultireal.com/appapi/v2/tts.php";
    [SerializeField] private string llmUrl = "https://ultireal.com/appapi/v2/llm.php";
    [SerializeField] private string voiceCloningUrl = "https://ultireal.com/appapi/v2/voicecloning.php";

    [Header("UI References")]
    [SerializeField] private UILogin uiLogin;
    [SerializeField] private UIRegister uiRegister;

    [Header("Avatar Generation Parameters")]
    [SerializeField] private string faceImagePath = "Assets/Faces/dirkjan.jpg"; 
    [SerializeField] private float gender = 1.0f;
    [SerializeField] private float age = 0.8f;
    [SerializeField] private float weight = 0.2f;

    [Header("Customization Assets")]
    [SerializeField] private string clothingName = "green_tomato_rei_ayanami";
    [SerializeField] private string hairName = "o4saken_long01";

    private DonkeyAvatar _player;    
    private DonkeyAvatar _npc1;
    private DonkeyAvatar _npc2;

    private DonkeySession session;
    private AudioMic audioMic;

    private DonkeyTTSStreaming ttsStreamer;
    private UiVoiceCloning uiVoiceCloning;

    private void HandleAudioRecorded(byte[] audioData, string sentence, string voiceName)
    {
        // Hide the panel
        if (uiVoiceCloning != null)
        {
            uiVoiceCloning.SetVisible(false);
        }
        
        UploadAudioRoutine(audioData, sentence, voiceName);
    }

    private async void UploadAudioRoutine(byte[] audioData, string sentence, string voiceName)
    {
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("[Main] Audio data is empty, skipping upload.");
            return;
        }

        if (session == null)
        {
            Debug.LogError("[Main] Active session is null! Make sure the user is logged in before uploading.");
            if (uiVoiceCloning != null)
            {
                uiVoiceCloning.UpdateButtonText("Login Required");
                uiVoiceCloning.SetRecordButtonInteractable(true);
            }
            return;
        }

        if (uiVoiceCloning != null)
        {
            uiVoiceCloning.UpdateButtonText("Uploading...");
            uiVoiceCloning.SetRecordButtonInteractable(false);
        }

        try
        {
            Debug.Log($"[Main] Uploading voice clone for '{voiceName}' ({audioData.Length} bytes)...");

            DonkeyVoiceCloning voiceclone = new DonkeyVoiceCloning();
            voiceclone.Initialize(session, voiceCloningUrl);
            
            string response = await voiceclone.CloneVoiceAsync(
                audioBytes: audioData,
                mimeType: "audio/wav",
                voiceName: voiceName,
                languageCode: "en"
            );

            if (!string.IsNullOrEmpty(response))
            {
                Debug.Log($"[Main] Voice Clone Response: {response}");
            }

            ttsStreamer.SetVoice(voiceName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Main] Voice cloning upload failed: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            await Task.Delay(2000); 

            if (uiVoiceCloning != null)
            {
                uiVoiceCloning.UpdateButtonText("Start Recording");
                uiVoiceCloning.SetRecordButtonInteractable(true);
            }
        }
    }

    private void Update()
    {
        // Delegate frame update to AudioMic instance safely
        audioMic?.Update();
    }

    private void HandleWavData(AudioClip myAudioClip)
    {
        Debug.Log($"Received AudioClip '{myAudioClip.name}' (Length: {myAudioClip.length:F2}s, Frequency: {myAudioClip.frequency}Hz) via callback.");

        DonkeySTT client = new DonkeySTT( 
            session,
            sttUrl,
            "auto",
            "large",
            280,
            this
        );

        // Send AudioClip for Speech-To-Text transcription
        client.SendAudioClipForTranscription(
            myAudioClip, 
            onSuccess: (text) => {
                        
                Debug.Log($"Transcribed: {text}");
                if (!string.IsNullOrEmpty(llmUrl))
                {
                    try
                    {
                        // Get or automatically attach DonkeyTTSStreaming component
                        DonkeyLLMStreaming llmStreamer = GetComponent<DonkeyLLMStreaming>();
                        if (llmStreamer == null)
                        {
                            llmStreamer = gameObject.AddComponent<DonkeyLLMStreaming>();
                        }

                        
                        ttsStreamer.Initialize(session, pauseBetweenSentences: 0.2f, pauseBetweenParagraphs: 0.6f);

                        // Pass AddSentence delegate into RequestStream
                        llmStreamer.RequestStream(
                            text, 
                            ttsStreamer.AddSentence,     
                            llmUrl
                        );

                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[DonkeyCompletions] Failed to send prompt: {ex.Message}");
                    }
                }
            },
            onError: (err) => Debug.LogError($"STT Error: {err}")
        );
    }

    private void Awake()
    {
        
        ttsStreamer = GetComponent<DonkeyTTSStreaming>();
        if (ttsStreamer == null)
        {
            ttsStreamer = gameObject.AddComponent<DonkeyTTSStreaming>();
        }
        
        // 1. Get or add the component to this GameObject
        uiVoiceCloning = GetComponent<UiVoiceCloning>();
        if (uiVoiceCloning == null)
        {
            uiVoiceCloning = gameObject.AddComponent<UiVoiceCloning>();
        }

        SubtitleManager.Initialize();

        // 2. Locate or create Canvas
        GameObject canvasObj = GameObject.Find("GeneratedCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("GeneratedCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 3. Initialize Login & Register UI
        if (uiLogin == null)
        {
            uiLogin = canvasObj.GetComponent<UILogin>();
            if (uiLogin == null)
            {
                uiLogin = canvasObj.AddComponent<UILogin>();
                uiLogin.BuildUI(canvasObj.transform);
            }
        }

        if (uiRegister == null)
        {
            uiRegister = canvasObj.GetComponent<UIRegister>();
            if (uiRegister == null)
            {
                uiRegister = canvasObj.AddComponent<UIRegister>();
                uiRegister.BuildUI(canvasObj.transform);
            }
        }

        // 4. Build and initial setup for Voice Cloning UI
        uiVoiceCloning.BuildUI(canvasObj.transform); 

        // Hide it by default until the user is logged in
        uiVoiceCloning.SetVisible(false);

        if (uiLogin != null) uiLogin.SetVisible(true);
        if (uiRegister != null) uiRegister.SetVisible(false);

        // Initialize AudioMic
        audioMic = new AudioMic(
            deviceName: null, 
            sampleRate: 44100, 
            maxRecordingLengthSeconds: 120, 
            onAudioRecorded: HandleWavData
        );
    }

    private async void Start()
    {        
        bool islogin = false;
        
        if (uiLogin != null)
        {
            uiLogin.SetVisible(false);
            uiLogin.SetInteractable(false);
            uiLogin.SetStatusMessage("Checking existing session...");
        }

        this.session = new DonkeySession(jsonRpcUrl);

        if (!string.IsNullOrEmpty(session.StoredCookie))
        {
            try
            {
                Debug.Log("Validating existing session...");
                islogin = await session.IsLoggedInAsync();
                Debug.Log("Session is valid! Running avatar workflow.");

                if (uiLogin != null) uiLogin.SetVisible(!islogin);

                await RunAvatarWorkflow(session);
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Stored session is invalid or expired: {ex.Message}. Requiring manual login.");
                session.ClearSession();
            }
        }

        if (uiLogin != null)
        {
            uiLogin.SetVisible(!islogin);
            uiLogin.SetInteractable(!islogin);
            uiLogin.SetStatusMessage("Please log in.");
        }
    }

    private void OnEnable()
    {
        if (uiVoiceCloning != null)
        {
            uiVoiceCloning.OnAudioRecorded += HandleAudioRecorded;
        }


        if (uiLogin != null)
        {
            uiLogin.OnLoginSubmitted.AddListener(HandleLoginSubmitted);
            uiLogin.OnRegisterClicked.AddListener(HandleShowRegisterClicked);
        }

        if (uiRegister != null)
        {
            uiRegister.OnRegisterSubmitted += HandleRegisterSubmitted;
            uiRegister.OnBackClicked += HandleBackToLoginClicked;
        }
    }

    private void OnDisable()
    {
        if (uiVoiceCloning != null)
        {
            uiVoiceCloning.OnAudioRecorded -= HandleAudioRecorded;
        }

        if (uiLogin != null)
        {
            uiLogin.OnLoginSubmitted.RemoveListener(HandleLoginSubmitted);
            uiLogin.OnRegisterClicked.RemoveListener(HandleShowRegisterClicked);
        }

        if (uiRegister != null)
        {
            uiRegister.OnRegisterSubmitted -= HandleRegisterSubmitted;
            uiRegister.OnBackClicked -= HandleBackToLoginClicked;
        }
    }

    private void HandleShowRegisterClicked()
    {
        if (uiLogin != null) uiLogin.SetVisible(false);
        if (uiRegister != null) uiRegister.SetVisible(true);
    }

    private void HandleBackToLoginClicked()
    {
        if (uiRegister != null) uiRegister.SetVisible(false);
        if (uiLogin != null) uiLogin.SetVisible(true);
    }

    private async void HandleRegisterSubmitted(string email, string password, string name, string genderInput, string ageInput)
    {
        Debug.Log($"Register submitted for: {email}, Name: {name}");

        DonkeySession newSession = new DonkeySession(jsonRpcUrl);

        try
        {
            await newSession.RegisterAsync(email, password, name, genderInput, ageInput);
            uiRegister.SetVisible(false);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Registration failed: {ex.Message}");
        }
    }

    private async void HandleLoginSubmitted(string email, string password)
    {
        if (uiLogin != null)
        {
            uiLogin.SetInteractable(false);
            uiLogin.SetStatusMessage("Logging in via JSON-RPC...");
        }

        if (this.session == null)
        {
            this.session = new DonkeySession(jsonRpcUrl);
        }

        try
        {
            Debug.Log("Logging in via JSON-RPC...");
            await this.session.LoginAsync(email, password);
            Debug.Log("Login successful! Session stored.");

            if (uiLogin != null) uiLogin.SetVisible(false);

            await RunAvatarWorkflow(this.session);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An error occurred during the Donkey workflow: {ex.Message}");
            if (uiLogin != null)
            {
                uiLogin.SetVisible(true);
                uiLogin.SetStatusMessage($"Error: {ex.Message}");
                uiLogin.SetInteractable(true);
            }
        }
    }

    private async Task RunAvatarWorkflow(DonkeySession session)
    {
        // Enable and show the Voice Cloning UI after login/session is validated
        if (uiVoiceCloning != null)
        {
            uiVoiceCloning.gameObject.SetActive(true);
            uiVoiceCloning.SetVisible(true); 
        }

        _player = await LoadAndInitializeAvatar(
            session, 
            0f, 0f, 0f, 
            gender, 
            faceImagePath, 
            clothingName, 
            hairName, 
            Vector3.zero
        );
    }

    private async Task<DonkeyAvatar> LoadAndInitializeAvatar(DonkeySession session, float x, float y, float z, float avatarGender, string avatarFaceImagePath, string targetClothing, string targetHair, Vector3 accessoryPositionOffset)
    {
        DonkeyAvatar avatar = new DonkeyAvatar(avatarGenUrl, clothingUrl, hairUrl, session);

        if (avatar.TryLoadFromCache(targetClothing, targetHair, avatarGender, age, weight))
        {
            Debug.Log($"[Workflow] Loaded avatar setup from cache for position ({x},{y},{z}).");
        }
        else
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, avatarFaceImagePath);
            if (!File.Exists(fullPath))
            {
                fullPath = avatarFaceImagePath;
            }

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"Face image file not found at: {fullPath}");
                return null;
            }

            byte[] imageBytes = File.ReadAllBytes(fullPath);

            Debug.Log($"Generating avatar via proxy for position ({x},{y},{z})...");
            await avatar.GenerateAvatarAsync(imageBytes, avatarGender, age, weight);
            
            Debug.Log($"Fitting clothing: {targetClothing}...");
            await avatar.FitClothingAsync(targetClothing);

            Debug.Log($"Fitting hair: {targetHair}...");
            await avatar.FitHairAsync(targetHair);
        }

        GameObject avatarObject = null;
        Transform mainArmatureRoot = null;

        if (avatar.AvatarGlbData != null)
        {
            Debug.Log($"Instantiating avatar at position ({x}, {y}, {z})...");

            Quaternion spawnRotation = Quaternion.Euler(0f, 180f, 0f);
            Vector3 spawnPosition = new Vector3(x, y, z);          

            avatarObject = new GameObject($"DonkeyAvatar_{avatar.AvatarId}");
            avatarObject.transform.position = spawnPosition;
            avatarObject.transform.rotation = spawnRotation;

            var gltfImport = new GltfImport();
            if (await gltfImport.Load(avatar.AvatarGlbData))
            {
                await gltfImport.InstantiateMainSceneAsync(avatarObject.transform);
                mainArmatureRoot = FindDeepChild(avatarObject.transform, "Hips") ?? avatarObject.transform;
            }

            async Task MergeAccessoryIntoAvatar(byte[] glbData)
            {
                if (glbData == null || mainArmatureRoot == null) return;

                var accImport = new GltfImport();
                if (await accImport.Load(glbData))
                {
                    var tempAccObj = new GameObject("TempAccessory");
                    await accImport.InstantiateMainSceneAsync(tempAccObj.transform);

                    SkinnedMeshRenderer[] accSmrs = tempAccObj.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var accSmr in accSmrs)
                    {
                        accSmr.transform.SetParent(avatarObject.transform, true);
                        accSmr.transform.localPosition = accessoryPositionOffset;
                        accSmr.transform.localRotation = Quaternion.identity;
                        accSmr.transform.localScale = Vector3.one;

                        Transform[] mainBones = avatarObject.GetComponentsInChildren<Transform>();
                        Transform[] newBones = new Transform[accSmr.bones.Length];
                        
                        for (int i = 0; i < accSmr.bones.Length; i++)
                        {
                            if (accSmr.bones[i] != null)
                            {
                                foreach (var b in mainBones)
                                {
                                    if (b.name.Equals(accSmr.bones[i].name, System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        newBones[i] = b;
                                        break;
                                    }
                                }
                                if (newBones[i] == null) newBones[i] = accSmr.bones[i];
                            }
                        }

                        accSmr.bones = newBones;
                        if (accSmr.rootBone != null)
                        {
                            foreach (var b in mainBones)
                            {
                                if (b.name.Equals(accSmr.rootBone.name, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    accSmr.rootBone = b;
                                    break;
                                }
                            }
                        }
                    }

                    MeshRenderer[] accMrs = tempAccObj.GetComponentsInChildren<MeshRenderer>();
                    foreach (var mr in accMrs)
                    {
                        mr.transform.SetParent(avatarObject.transform, true);
                        mr.transform.localPosition = accessoryPositionOffset;
                        mr.transform.localRotation = Quaternion.identity;
                        mr.transform.localScale = Vector3.one;
                    }

                    if (tempAccObj != null)
                    {
                        Destroy(tempAccObj);
                    }
                }
            }

            foreach (var clothingItem in avatar.ClothingItems)
            {
                await MergeAccessoryIntoAvatar(clothingItem.GlbData);
            }

            if (avatar.HairGlbData != null)
            {
                await MergeAccessoryIntoAvatar(avatar.HairGlbData);
            }
        }

        if (avatarObject != null)
        {
            Debug.Log($"[Main] Avatar '{avatarObject.name}' successfully loaded at ({x}, {y}, {z}).");
        }

        if (uiLogin != null)
        {
            uiLogin.SetStatusMessage("Avatar loaded successfully!");
            uiLogin.SetInteractable(true);
        }

        return avatar;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}