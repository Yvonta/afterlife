using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Donkey;
using GLTFast;

public class Main : MonoBehaviour
{
    [Header("Server Endpoints")]
    [SerializeField] private string jsonRpcUrl = "https://yvonta.ai/appapi/v2/xbot.php";
    [SerializeField] private string avatarGenUrl = "https://yvonta.ai/appapi/v2/avatargen.php";
    [SerializeField] private string clothingUrl = "https://yvonta.ai/appapi/v2/clothing.php";
    [SerializeField] private string hairUrl = "https://yvonta.ai/appapi/v2/hair.php";

    [Header("Login Credentials")]
    [SerializeField] private string userEmail = "user@yvonta.ai";
    [SerializeField] private string userPassword = "password";

    [Header("Avatar Generation Parameters")]
    [SerializeField] private string faceImagePath; 
    [SerializeField] private float gender = 0.0f;
    [SerializeField] private float age = 0.8f;
    [SerializeField] private float weight = 0.2f;

    [Header("Customization Assets")]
    [SerializeField] private string clothingName = "green_tomato_rei_ayanami";
    [SerializeField] private string hairName = "o4saken_long01";

    private async void Start()
    {
        DonkeySession session = new DonkeySession(jsonRpcUrl);

        try
        {
            Debug.Log("Logging in via JSON-RPC...");
            await session.LoginAsync(userEmail, userPassword);
            Debug.Log("Login successful! Session cookie stored.");

            DonkeyAvatar avatar = new DonkeyAvatar(avatarGenUrl, clothingUrl, hairUrl, session);

            string fullPath = Path.Combine(Application.streamingAssetsPath, faceImagePath);
            if (!File.Exists(fullPath))
            {
                fullPath = faceImagePath;
            }

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"Face image file not found at: {fullPath}");
                return;
            }

            byte[] imageBytes = File.ReadAllBytes(fullPath);

            Debug.Log("Generating avatar via proxy...");
            await avatar.GenerateAvatarAsync(imageBytes, gender, age, weight);
            Debug.Log($"Avatar generated successfully! Avatar ID: {avatar.AvatarId}");

            Debug.Log($"Fitting clothing: {clothingName}...");
            await avatar.FitClothingAsync(clothingName);

            Debug.Log($"Fitting hair: {hairName}...");
            await avatar.FitHairAsync(hairName);

            // Instantiate base avatar using GLTFast's generic Load method for byte arrays
            if (avatar.AvatarGlbData != null)
            {
                Debug.Log("Instantiating avatar in the scene using GLTFast...");

                GameObject avatarObject = new GameObject($"DonkeyAvatar_{avatar.AvatarId}");
                var gltfImport = new GltfImport();
                
                // Use the updated generic Load method instead of LoadGltfBinary
                bool success = await gltfImport.Load(avatar.AvatarGlbData);
                
                if (success)
                {
                    success = await gltfImport.InstantiateMainSceneAsync(avatarObject.transform);
                    if (!success)
                    {
                        Debug.LogError("Failed to instantiate GLTFast main scene.");
                    }
                }
                else
                {
                    Debug.LogError("Failed to load GLB bytes via GltfImport.");
                }
            }

// Instantiate base avatar using GLTFast's generic Load method for byte arrays
            if (avatar.AvatarGlbData != null)
            {
                Debug.Log("Instantiating avatar in the scene using GLTFast...");

                GameObject avatarObject = new GameObject($"DonkeyAvatar_{avatar.AvatarId}");
                var gltfImport = new GltfImport();
                
                bool success = await gltfImport.Load(avatar.AvatarGlbData);
                
                if (success)
                {
                    success = await gltfImport.InstantiateMainSceneAsync(avatarObject.transform);
                    if (!success)
                    {
                        Debug.LogError("Failed to instantiate GLTFast main scene.");
                    }
                }
                else
                {
                    Debug.LogError("Failed to load GLB bytes via GltfImport.");
                }

                // Render all fitted clothing items
                foreach (var clothingItem in avatar.ClothingItems)
                {
                    if (clothingItem.GlbData != null)
                    {
                        Debug.Log($"Instantiating clothing: {clothingItem.Name}...");
                        var clothingImport = new GltfImport();
                        bool clothingSuccess = await clothingImport.Load(clothingItem.GlbData);
                        if (clothingSuccess)
                        {
                            await clothingImport.InstantiateMainSceneAsync(avatarObject.transform);
                        }
                        else
                        {
                            Debug.LogError($"Failed to load GLB bytes for clothing: {clothingItem.Name}");
                        }
                    }
                }

                // Render fitted hair
                if (avatar.HairGlbData != null)
                {
                    Debug.Log("Instantiating hair...");
                    var hairImport = new GltfImport();
                    bool hairSuccess = await hairImport.Load(avatar.HairGlbData);
                    if (hairSuccess)
                    {
                        await hairImport.InstantiateMainSceneAsync(avatarObject.transform);
                    }
                    else
                    {
                        Debug.LogError("Failed to load GLB bytes for hair.");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An error occurred during the Donkey workflow: {ex.Message}");
        }
    }
}