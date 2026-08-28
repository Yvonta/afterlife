using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    public class DonkeySTT
    {
        private readonly string apiUrl;
        private readonly string defaultLanguage;
        private readonly string defaultModel;
        private readonly int timeout;
        private readonly MonoBehaviour coroutineRunner;
        private readonly DonkeySession session;

        public DonkeySTT(
            DonkeySession session,
            string apiUrl, 
            string defaultLanguage, 
            string defaultModel, 
            int timeout = 280,
            MonoBehaviour coroutineRunner = null)
        {
            this.apiUrl = apiUrl;
            this.defaultLanguage = defaultLanguage;
            this.defaultModel = defaultModel;
            this.timeout = timeout;
            this.coroutineRunner = coroutineRunner;
            this.session = session;
        }

        public void SendAudioForTranscription(
            byte[] wavData, 
            string language = null, 
            Action<string> onSuccess = null, 
            Action<string> onError = null)
        {
            if (wavData == null || wavData.Length == 0)
            {
                onError?.Invoke("WAV audio data is null or empty.");
                return;
            }

            string targetLanguage = language ?? defaultLanguage;
            IEnumerator process = PostAudioCoroutine(wavData, targetLanguage, onSuccess, onError);
            StartTask(process);
        }

        public void SendAudioClipForTranscription(
            AudioClip clip, 
            string language = null, 
            Action<string> onSuccess = null, 
            Action<string> onError = null)
        {
            if (clip == null)
            {
                onError?.Invoke("Provided AudioClip is null.");
                return;
            }

            try
            {
                byte[] wavBytes = EncodeToWav(clip);
                SendAudioForTranscription(wavBytes, language, onSuccess, onError);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to encode AudioClip: {ex.Message}");
            }
        }

        private void StartTask(IEnumerator coroutine)
        {
            if (coroutineRunner != null)
            {
                coroutineRunner.StartCoroutine(coroutine);
            }
            else
            {
                var runnerGo = new GameObject("[DonkeyTalk_Runner]");
                UnityEngine.Object.DontDestroyOnLoad(runnerGo);
                var runner = runnerGo.AddComponent<CoroutineHost>();
                runner.StartCoroutine(runner.RunAndSelfDestruct(coroutine));
            }
        }


private IEnumerator PostAudioCoroutine(
    byte[] audioData, 
    string language, 
    Action<string> onSuccess, 
    Action<string> onError)
{
    WWWForm form = new WWWForm();
    form.AddBinaryData("file", audioData, "audio.wav", "audio/wav");
    form.AddField("model", defaultModel);
    form.AddField("response_format", "json");

    if (!string.IsNullOrEmpty(language))
    {
        form.AddField("language", language);
    }

    using (UnityWebRequest request = UnityWebRequest.Post(apiUrl, form))
    {
        request.timeout = timeout;

        // Ensure session cookie is properly formatted and attached
        if (session != null && !string.IsNullOrEmpty(session.StoredCookie))
        {
            string cookieHeader = session.StoredCookie.StartsWith("SESSION=") 
                ? session.StoredCookie 
                : $"SESSION={session.StoredCookie}";

            request.SetRequestHeader("Cookie", cookieHeader);
            Debug.Log($"[DonkeySTT] Sending Header -> Cookie: {cookieHeader}");
        }
        else
        {
            Debug.LogWarning("[DonkeySTT] Request sent without session cookie! (Session was null or empty)");
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string errMessage = $"HTTP Error [{request.responseCode}]: {request.error}\n{request.downloadHandler.text}";
            Debug.LogError($"[DonkeyTalk] {errMessage}");
            onError?.Invoke(errMessage);
        }
        else
        {
            string jsonResponse = request.downloadHandler.text;
            try
            {
                WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(jsonResponse);

                if (response.error != null && !string.IsNullOrEmpty(response.error.message))
                {
                    onError?.Invoke(response.error.message);
                }
                else if (!string.IsNullOrEmpty(response.text))
                {
                    onSuccess?.Invoke(response.text);
                }
                else if (!string.IsNullOrEmpty(response.transcription))
                {
                    onSuccess?.Invoke(response.transcription);
                }
                else
                {
                    onError?.Invoke("Received empty transcription from API.");
                }
            }
            catch (Exception ex)
            {
                string parseErr = $"Failed to parse JSON response: {ex.Message}\nRaw: {jsonResponse}";
                Debug.LogError($"[DonkeyTalk] {parseErr}");
                onError?.Invoke(parseErr);
            }
        }
    }
}
/*
        private IEnumerator PostAudioCoroutine(
            byte[] audioData, 
            string language, 
            Action<string> onSuccess, 
            Action<string> onError)
        {
            // Build multipart/form-data payload for Whisper compatibility
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", audioData, "audio.wav", "audio/wav");
            form.AddField("model", defaultModel);
            form.AddField("response_format", "json");

            if (!string.IsNullOrEmpty(language))
            {
                form.AddField("language", language);
            }

            using (UnityWebRequest request = UnityWebRequest.Post(apiUrl, form))
            {
                request.timeout = timeout;

                if (session != null && !string.IsNullOrEmpty(session.StoredCookie))
                {
                    request.SetRequestHeader("Cookie", session.StoredCookie);
                }

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errMessage = $"HTTP Error [{request.responseCode}]: {request.error}\n{request.downloadHandler.text}";
                    Debug.LogError($"[DonkeyTalk] {errMessage}");
                    onError?.Invoke(errMessage);
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;
                    try
                    {
                        WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(jsonResponse);

                        if (response.error != null && !string.IsNullOrEmpty(response.error.message))
                        {
                            onError?.Invoke(response.error.message);
                        }
                        else if (!string.IsNullOrEmpty(response.text))
                        {
                            onSuccess?.Invoke(response.text);
                        }
                        // Fallback check for proxy backends returning legacy {"transcription": "..."}
                        else if (!string.IsNullOrEmpty(response.transcription))
                        {
                            onSuccess?.Invoke(response.transcription);
                        }
                        else
                        {
                            onError?.Invoke("Received empty transcription from API.");
                        }
                    }
                    catch (Exception ex)
                    {
                        string parseErr = $"Failed to parse JSON response: {ex.Message}\nRaw: {jsonResponse}";
                        Debug.LogError($"[DonkeyTalk] {parseErr}");
                        onError?.Invoke(parseErr);
                    }
                }
            }
        }
*/
        #region WAV Encoder
        public static byte[] EncodeToWav(AudioClip clip)
        {
            ushort channels = (ushort)clip.channels;
            int frequency = clip.frequency;
            ushort bitsPerSample = 16;
            int sampleCount = clip.samples * channels;

            float[] samples = new float[sampleCount];
            clip.GetData(samples, 0);

            using (MemoryStream stream = new MemoryStream(44 + sampleCount * 2))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write(channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * bitsPerSample / 8);
                writer.Write((ushort)(channels * bitsPerSample / 8));
                writer.Write(bitsPerSample);

                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(sampleCount * 2);

                for (int i = 0; i < samples.Length; i++)
                {
                    short pcmSample = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                    writer.Write(pcmSample);
                }

                return stream.ToArray();
            }
        }
        #endregion

        [Serializable]
        private class WhisperResponse
        {
            public string text;          // Standard Whisper response field
            public string transcription; // Fallback field
            public WhisperError error;
        }

        [Serializable]
        private class WhisperError
        {
            public string message;
            public string type;
            public string code;
        }

        private class CoroutineHost : MonoBehaviour
        {
            public IEnumerator RunAndSelfDestruct(IEnumerator target)
            {
                yield return StartCoroutine(target);
                Destroy(gameObject);
            }
        }
    }
}