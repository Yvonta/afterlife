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
    [SerializeField] private string jsonRpcUrl = "https://yvonta.ai/appapi/v2/xbot.php";
    [SerializeField] private string avatarGenUrl = "https://yvonta.ai/appapi/v2/avatargen.php";
    [SerializeField] private string clothingUrl = "https://yvonta.ai/appapi/v2/clothing.php";
    [SerializeField] private string hairUrl = "https://yvonta.ai/appapi/v2/hair.php";

    [Header("UI References")]
    [SerializeField] private UILogin uiLogin;
    [SerializeField] private UIRegister uiRegister;

    [Header("Avatar Generation Parameters")]
    [SerializeField] private string faceImagePath; 
    [SerializeField] private float gender = 1.0f;
    [SerializeField] private float age = 0.8f;
    [SerializeField] private float weight = 0.2f;

    [Header("Customization Assets")]
    [SerializeField] private string clothingName = "green_tomato_rei_ayanami";
    [SerializeField] private string hairName = "o4saken_long01";
    [SerializeField] private string animationPath = "1.bvh";

    private DonkeyAvatar _player;    
    private DonkeyAvatar _npc1;
    private DonkeyAvatar _npc2;
    
    private void Awake()
    {
        GameObject canvasObj = GameObject.Find("GeneratedCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("GeneratedCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

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

        if (uiLogin != null) uiLogin.SetVisible(true);
        if (uiRegister != null) uiRegister.SetVisible(false);
    }

    private async void Start()
    {        
        if (uiLogin != null)
        {
            uiLogin.SetVisible(false);
            uiLogin.SetInteractable(false);
            uiLogin.SetStatusMessage("Checking existing session...");
        }

        DonkeySession session = new DonkeySession(jsonRpcUrl);

        if (!string.IsNullOrEmpty(session.StoredCookie))
        {
            try
            {
                Debug.Log("Validating existing session...");
                await session.IsLoggedInAsync();
                Debug.Log("Session is valid! Running avatar workflow.");

                if (uiLogin != null) uiLogin.SetVisible(false);

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
            uiLogin.SetVisible(true);
            uiLogin.SetInteractable(true);
            uiLogin.SetStatusMessage("Please log in.");
        }
    }

    private void OnEnable()
    {
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

        DonkeySession session = new DonkeySession(jsonRpcUrl);

        try
        {
            await session.RegisterAsync(email, password, name, genderInput, ageInput);
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

        DonkeySession session = new DonkeySession(jsonRpcUrl);

        try
        {
            Debug.Log("Logging in via JSON-RPC...");
            await session.LoginAsync(email, password);
            Debug.Log("Login successful! Session stored.");

            uiLogin.SetVisible(false);

            await RunAvatarWorkflow(session);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An error occurred during the Donkey workflow: {ex.Message}");
            uiLogin.SetVisible(true);
            if (uiLogin != null)
            {
                uiLogin.SetStatusMessage($"Error: {ex.Message}");
                uiLogin.SetInteractable(true);
            }
        }
    }

    private async Task RunAvatarWorkflow(DonkeySession session)
    {
        _player = await LoadAndInitializeAvatar(session, 0f, 0f, 0f, gender, faceImagePath, clothingName, hairName, Vector3.zero);
        _npc1 = await LoadAndInitializeAvatar(session, -0.5f, 0f, 0f, 1f, "/home/dirkjan/2.jpg", "punkduck_wetsuit", "cortu_straight_bangs", Vector3.zero);
        // Specifieke hoogte-offset toegevoegd voor _npc2 zodat het pak en haar op de juiste hoogte aansluiten
        _npc2 = await LoadAndInitializeAvatar(session, 0.5f, 0f, 0f, 1f, "/home/dirkjan/2.jpg", "toigo_female_suit_2", "punkduck_alpha7_curly", new Vector3(0f, 0.0f, 0f));
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
                        // Pas de optionele offset toe zodat afwijkende modellen netjes uitlijnen
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

                    Destroy(tempAccObj);
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
            Debug.Log($"[Main] Avatar '{avatarObject.name}' succesvol geladen op ({x}, {y}, {z}).");
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