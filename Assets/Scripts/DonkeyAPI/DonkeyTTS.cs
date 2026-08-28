using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    public class DonkeyTTS : MonoBehaviour
    {
        string serverUrl = "https://ultireal.com/appapi/v2/tts.php";
        
        private string voice = "Kees";

        private AudioSource audioSource;
        private Queue<AudioClip> playQueue = new Queue<AudioClip>();
        private Queue<string> subtitleQueue = new Queue<string>();


        // Fix: Use explicit backing field instead of auto-property setter
        private bool isDownloading = false;
        public bool IsDownloading => isDownloading;

        public int GetBufferedClipCount() => playQueue.Count;

        private DonkeySession session;

        public void Initialize(DonkeySession session)
        {
            this.session = session;
        }

        public void SetVoice(string voice)
        {
            this.voice = voice;
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

            WWWForm form = new WWWForm();

            form.AddField("text", text.Replace(".", "").Replace("?", ""));
            form.AddField("format", "mp3");
            form.AddField("language_id", language);
            form.AddField("voice", voice);

            using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
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