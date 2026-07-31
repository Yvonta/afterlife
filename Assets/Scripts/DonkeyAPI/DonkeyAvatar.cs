using System;
using System.Collections.Generic;
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
                ClothingItems.Add(new ClothingItem { Name = clothingName, GlbData = clothingData });
            }
        }

        public async Task FitHairAsync(string hairName)
        {
            if (string.IsNullOrEmpty(AvatarId))
            {
                throw new Exception("Avatar must be generated before fitting hair.");
            }

            // clothing.php validates against clothes_name, so pass the hair name into clothes_name
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("avatar_id", AvatarId),
                new MultipartFormDataSection("hair_name", hairName)
            };

            using (UnityWebRequest www = UnityWebRequest.Post(_hairUrl, formData))
            {
                if (!string.IsNullOrEmpty(_session.StoredCookie))
                {
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
            }
        }
    }
}