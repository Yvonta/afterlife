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
        private readonly string defaultMimeType;
        private readonly int timeout;
        private readonly MonoBehaviour coroutineRunner;
        private readonly DonkeySession session;

        /// <summary>
        /// Initializes a new instance of the DonkeyTalk API client.
        /// </summary>
        public DonkeySTT(
            DonkeySession session,
            string apiUrl = "https://yourdomain.com/appapi/stt.php", 
            string defaultLanguage = "en", 
            string defaultMimeType = "audio/wav", 
            int timeout = 280,
            MonoBehaviour coroutineRunner = null)
        {
            this.apiUrl = apiUrl;
            this.defaultLanguage = defaultLanguage;
            this.defaultMimeType = defaultMimeType;
            this.timeout = timeout;
            this.coroutineRunner = coroutineRunner;
            this.session = session;
        }

        /// <summary>
        /// Sends raw WAV audio bytes to the Speech-To-Text endpoint.
        /// </summary>
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
            IEnumerator process = PostAudioCoroutine(wavData, defaultMimeType, targetLanguage, onSuccess, onError);

            StartTask(process);
        }

        /// <summary>
        /// Encodes a Unity AudioClip to WAV and sends it for transcription.
        /// </summary>
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
            string mimeType, 
            string language, 
            Action<string> onSuccess, 
            Action<string> onError)
        {
            string requestUrl = $"{apiUrl}?language={UnityWebRequest.EscapeURL(language)}";

            using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(audioData);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", mimeType);
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
                        STTResponse response = JsonUtility.FromJson<STTResponse>(jsonResponse);

                        if (!string.IsNullOrEmpty(response.error))
                        {
                            onError?.Invoke(response.error);
                        }
                        else
                        {
                            onSuccess?.Invoke(response.transcription);
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

        #region WAV Encoder
        /// <summary>
        /// Converts a Unity AudioClip to a 16-bit PCM WAV byte array.
        /// </summary>
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
                // RIFF Header
                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

                // Subchunk 1 (fmt )
                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size (16 for PCM)
                writer.Write((ushort)1); // AudioFormat (1 = PCM)
                writer.Write(channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * bitsPerSample / 8); // ByteRate
                writer.Write((ushort)(channels * bitsPerSample / 8));  // BlockAlign
                writer.Write(bitsPerSample);

                // Subchunk 2 (data)
                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(sampleCount * 2);

                // Samples convert float -> Int16 PCM
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
        private class STTResponse
        {
            public string transcription;
            public string error;
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