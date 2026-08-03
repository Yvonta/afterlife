using System.IO;
using System.Threading.Tasks;
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
    [SerializeField] private float gender = 0.0f;
    [SerializeField] private float age = 0.8f;
    [SerializeField] private float weight = 0.2f;

    [Header("Customization Assets")]
    [SerializeField] private string clothingName = "green_tomato_rei_ayanami";
    [SerializeField] private string hairName = "o4saken_long01";

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
        DonkeyAvatar avatar = new DonkeyAvatar(avatarGenUrl, clothingUrl, hairUrl, session);

        // Try loading everything from cache first
        if (avatar.TryLoadFromCache(clothingName, hairName, gender, age, weight))
        {
            Debug.Log("[Workflow] Loaded complete avatar setup from cache. Skipping network calls.");
        }
        else
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, faceImagePath);
            if (!File.Exists(fullPath))
            {
                fullPath = faceImagePath;
            }

            if (!File.Exists(fullPath))
            {
                string errorMsg = $"Face image file not found at: {fullPath}";
                Debug.LogError(errorMsg);
                if (uiLogin != null)
                {
                    uiLogin.SetVisible(true);
                    uiLogin.SetStatusMessage(errorMsg);
                    uiLogin.SetInteractable(true);
                }
                return;
            }

            byte[] imageBytes = File.ReadAllBytes(fullPath);

            Debug.Log("Generating avatar via proxy...");
            await avatar.GenerateAvatarAsync(imageBytes, gender, age, weight);
            
            Debug.Log($"Fitting clothing: {clothingName}...");
            await avatar.FitClothingAsync(clothingName);

            Debug.Log($"Fitting hair: {hairName}...");
            await avatar.FitHairAsync(hairName);
        }

        // Instantiation with GLTFast remains identical...
        if (avatar.AvatarGlbData != null)
        {
            Debug.Log("Instantiating avatar in the scene using GLTFast...");

            Quaternion spawnRotation = Quaternion.Euler(0f, 180f, 0f);
            GameObject avatarObject = new GameObject($"DonkeyAvatar_{avatar.AvatarId}");
            avatarObject.transform.rotation = spawnRotation;
            
            var gltfImport = new GltfImport();
            if (await gltfImport.Load(avatar.AvatarGlbData))
            {
                await gltfImport.InstantiateMainSceneAsync(avatarObject.transform);
            }

            foreach (var clothingItem in avatar.ClothingItems)
            {
                if (clothingItem.GlbData != null)
                {
                    var clothingImport = new GltfImport();
                    if (await clothingImport.Load(clothingItem.GlbData))
                    {
                        await clothingImport.InstantiateMainSceneAsync(avatarObject.transform);
                    }
                }
            }

            if (avatar.HairGlbData != null)
            {
                var hairImport = new GltfImport();
                if (await hairImport.Load(avatar.HairGlbData))
                {
                    await hairImport.InstantiateMainSceneAsync(avatarObject.transform);
                }
            }
        }

        if (uiLogin != null)
        {
            uiLogin.SetStatusMessage("Avatar loaded successfully!");
            uiLogin.SetInteractable(true);
        }
    }
}