using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    public class DonkeyAvatar
    {
        private readonly string _avatarGenUrl;
        private readonly string _clothingUrl;
        private readonly string _hairUrl;
        private readonly DonkeySession _session;

        public string AvatarId { get; private set; }
        public byte[] AvatarGlbData { get; private set; }
        public byte[] HairGlbData { get; private set; }
        public List<ClothingItem> ClothingItems { get; private set; } = new List<ClothingItem>();

        public class ClothingItem
        {
            public string Name { get; set; }
            public byte[] GlbData { get; set; }
        }

        public DonkeyAvatar(string avatarGenUrl, string clothingUrl, string hairUrl, DonkeySession session)
        {
            _avatarGenUrl = avatarGenUrl;
            _clothingUrl = clothingUrl;
            _hairUrl = hairUrl;
            _session = session;
        }

        /// <summary>
        /// Tries to load the avatar, clothing, and hair from local persistent storage cache.
        /// Returns true if all required cached files are successfully loaded.
        /// </summary>
        public bool TryLoadFromCache(string clothingName, string hairName, float gender, float age, float weight)
        {
            string avatarCachePath = Path.Combine(Application.persistentDataPath, $"avatar_body_{gender}_{age}_{weight}.glb");
            string clothingCachePath = Path.Combine(Application.persistentDataPath, $"clothing_{clothingName}.glb");
            string hairCachePath = Path.Combine(Application.persistentDataPath, $"hair_{hairName}.glb");

            if (File.Exists(avatarCachePath) && File.Exists(clothingCachePath) && File.Exists(hairCachePath))
            {
                try
                {
                    AvatarGlbData = File.ReadAllBytes(avatarCachePath);
                    
                    byte[] clothingData = File.ReadAllBytes(clothingCachePath);
                    ClothingItems.Clear();
                    ClothingItems.Add(new ClothingItem { Name = clothingName, GlbData = clothingData });

                    HairGlbData = File.ReadAllBytes(hairCachePath);
                    
                    // Assign a generic or cached identifier
                    AvatarId = "cached_avatar";

                    Debug.Log("[DonkeyAvatar] Successfully loaded avatar, clothing, and hair from local cache.");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DonkeyAvatar] Failed to read cached files, falling back to network: {ex.Message}");
                }
            }

            return false;
        }

        public async Task GenerateAvatarAsync(byte[] imageBytes, float gender = 0.0f, float age = 0.8f, float weight = 0.2f)
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("gender", gender.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new MultipartFormDataSection("age", age.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new MultipartFormDataSection("weight", weight.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new MultipartFormFileSection("file", imageBytes, "face.jpg", "image/jpeg")
            };

            using (UnityWebRequest www = UnityWebRequest.Post(_avatarGenUrl, formData))
            {
                if (!string.IsNullOrEmpty(_session.StoredCookie))
                {
                    Debug.Log($"[Avatar] Sending Cookie: {_session.StoredCookie}");
                    www.SetRequestHeader("Cookie", _session.StoredCookie);
                }

                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Avatar Generation Error: {www.error} | Response: {www.downloadHandler.text}");
                }

                AvatarGlbData = www.downloadHandler.data;
                AvatarId = www.GetResponseHeader("X-Avatar-Id");

                if (string.IsNullOrEmpty(AvatarId))
                {
                    throw new Exception("ERROR: No X-Avatar-Id header found in response.");
                }

                // Save avatar body to cache
                try
                {
                    string avatarCachePath = Path.Combine(Application.persistentDataPath, $"avatar_body_{gender}_{age}_{weight}.glb");
                    File.WriteAllBytes(avatarCachePath, AvatarGlbData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DonkeyAvatar] Failed to cache avatar body: {ex.Message}");
                }
            }
        }

        public async Task FitClothingAsync(string clothingName)
        {
            if (string.IsNullOrEmpty(AvatarId))
            {
                throw new Exception("Avatar must be generated before fitting clothing.");
            }

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("avatar_id", AvatarId),
                new MultipartFormDataSection("clothes_name", clothingName)
            };

            using (UnityWebRequest www = UnityWebRequest.Post(_clothingUrl, formData))
            {
                if (!string.IsNullOrEmpty(_session.StoredCookie))
                {
                    Debug.Log($"[Clothing] Sending Cookie: {_session.StoredCookie}");
                    www.SetRequestHeader("Cookie", _session.StoredCookie);
                }

                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success || www.responseCode != 200)
                {
                    throw new Exception($"Clothing Fit Failed (HTTP {www.responseCode}): {www.downloadHandler.text}");
                }

                byte[] clothingData = www.downloadHandler.data;

                ClothingItems.RemoveAll(c => c.Name == clothingName);
                ClothingItems.Add(new ClothingItem
                {
                    Name = clothingName,
                    GlbData = clothingData
                });

                // Save clothing to cache
                try
                {
                    string clothingCachePath = Path.Combine(Application.persistentDataPath, $"clothing_{clothingName}.glb");
                    File.WriteAllBytes(clothingCachePath, clothingData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DonkeyAvatar] Failed to cache clothing: {ex.Message}");
                }
            }
        }

        public async Task FitHairAsync(string hairName)
        {
            if (string.IsNullOrEmpty(AvatarId))
            {
                throw new Exception("Avatar must be generated before fitting hair.");
            }

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("avatar_id", AvatarId),
                new MultipartFormDataSection("hair_name", hairName)
            };

            using (UnityWebRequest www = UnityWebRequest.Post(_hairUrl, formData))
            {
                if (!string.IsNullOrEmpty(_session.StoredCookie))
                {
                    Debug.Log($"[Hair] Sending Cookie: {_session.StoredCookie}");
                    www.SetRequestHeader("Cookie", _session.StoredCookie);
                }

                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success || www.responseCode != 200)
                {
                    throw new Exception($"Hair Fit Failed (HTTP {www.responseCode}): {www.downloadHandler.text}");
                }

                HairGlbData = www.downloadHandler.data;

                // Save hair to cache
                try
                {
                    string hairCachePath = Path.Combine(Application.persistentDataPath, $"hair_{hairName}.glb");
                    File.WriteAllBytes(hairCachePath, HairGlbData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DonkeyAvatar] Failed to cache hair: {ex.Message}");
                }
            }
        }
    }
}