using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    public class DonkeyTTS : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "https://ultireal.com/appapi/v2/tts.php";
        [SerializeField] private string voiceFilePath = "/Users/dirkjan/dirkjan.mp3";

        private AudioSource audioSource;
        private Queue<AudioClip> playQueue = new Queue<AudioClip>();
        private Queue<string> subtitleQueue = new Queue<string>();


        // Fix: Use explicit backing field instead of auto-property setter
        private bool isDownloading = false;
        public bool IsDownloading => isDownloading;

        public int GetBufferedClipCount() => playQueue.Count;

        private DonkeySession session;

        private string subtitleText = "";

        public void Initialize(DonkeySession session)
        {
            this.session = session;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        public void EnqueueSentence(string sentence, string language)
        {
            if (this == null || !gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(sentence)) return;
            StartCoroutine(FetchSentenceAudio(sentence.Trim(), language));
        }

        public void EnqueueParagraph(string paragraph, string language)
        {
            EnqueueSentence(paragraph, language);
        }

        private IEnumerator FetchSentenceAudio(string text, string language)
        {
            isDownloading = true;

            byte[] fileBytes = System.Array.Empty<byte>();
            if (File.Exists(voiceFilePath))
            {
                fileBytes = File.ReadAllBytes(voiceFilePath);
            }
            else
            {
                Debug.LogWarning($"[DonkeyTTS] Voice file not found at: {voiceFilePath}");
            }

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("text", text.Replace(".", "").Replace("?", "")),
                new MultipartFormDataSection("format", "mp3"),
                new MultipartFormDataSection("language_id", language),           
                new MultipartFormFileSection("voice_file", fileBytes, "voice.mp3", "audio/mpeg")
            };

            using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
            {
                DownloadHandlerAudioClip dh = new DownloadHandlerAudioClip(string.Empty, AudioType.MPEG);
                dh.streamAudio = false;
                www.downloadHandler = dh;

                if(session != null && !string.IsNullOrEmpty(session.StoredCookie))
                {
                    www.SetRequestHeader("Cookie", session.StoredCookie);
                }

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success && www.downloadHandler.data != null && www.downloadHandler.data.Length > 0)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

                    if (clip != null && clip.samples > 0)
                    {
                        playQueue.Enqueue(clip);
                        subtitleQueue.Enqueue(text);
                    }
                    else
                    {
                        Debug.LogError($"[DonkeyTTS] Received invalid audio clip for: '{text}'");
                    }
                }
                else
                {
                    Debug.LogError($"[DonkeyTTS] Request failed: {www.error}");
                }
            }

            isDownloading = false;
        }

        public float PlayNextClip()
        {
            if (playQueue.Count == 0) return 0f;

            AudioClip clip = playQueue.Dequeue();
            string text = subtitleQueue.Dequeue();
            audioSource.clip = clip;
            audioSource.Play();
            SubtitleManager.Instance.DisplaySubtitle(text);

            return clip.length;
        }

        public void ClearAll()
        {
            StopAllCoroutines();
            if (audioSource != null) audioSource.Stop();
            playQueue.Clear();
            subtitleQueue.Clear();
            isDownloading = false;
        }
    }
}