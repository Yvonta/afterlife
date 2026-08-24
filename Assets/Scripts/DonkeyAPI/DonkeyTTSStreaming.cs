using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Donkey
{
    [RequireComponent(typeof(DonkeyTTS))]
    public class DonkeyTTSStreaming : MonoBehaviour
    {
        public const string PARAGRAPH_BREAK_TOKEN = "[PARAGRAPH_BREAK]";

        [SerializeField] private float pauseBetweenSentences = 0.2f;
        [SerializeField] private float pauseBetweenParagraphs = 0.6f;

        private DonkeyTTS donkeyTTS;

        private struct SentenceItem
        {
            public string Text;
            public string Language;
        }

        private Queue<SentenceItem> sentenceQueue = new Queue<SentenceItem>();
        private bool isProcessingQueue = false;
        private TextLanguageDetector languageDetector;

        private void Start()
        {
            languageDetector = new TextLanguageDetector();
        }

        private void Awake()
        {
            donkeyTTS = GetComponent<DonkeyTTS>();
            if (donkeyTTS == null)
            {
                donkeyTTS = gameObject.AddComponent<DonkeyTTS>();
            }
        }

        public void Initialize(DonkeySession session, float pauseBetweenSentences = 0.2f, float pauseBetweenParagraphs = 0.6f)
        {
            this.pauseBetweenSentences = pauseBetweenSentences;
            this.pauseBetweenParagraphs = pauseBetweenParagraphs;
            donkeyTTS.Initialize(session);
        }

        public void AddSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence)) return;

            string detectedLanguage = languageDetector.DetectLanguage(sentence);  

            if (sentence == PARAGRAPH_BREAK_TOKEN)
            {
                sentenceQueue.Enqueue(new SentenceItem { Text = PARAGRAPH_BREAK_TOKEN, Language = string.Empty });
            }
            else
            {
                string clean = EmojiRemover.RemoveEmojis(sentence);
                clean = StringHelper.RemoveNonVisibleChars(clean);
                clean = StringHelper.FilterAsterisks(clean);
                clean = StringHelper.FilterNewline(clean);

                if (string.IsNullOrWhiteSpace(clean)) return;

                clean = clean.Trim();
                sentenceQueue.Enqueue(new SentenceItem { Text = clean, Language = detectedLanguage });
            }

            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessSentenceQueue());
            }
        }

        private IEnumerator ProcessSentenceQueue()
        {
            isProcessingQueue = true;

            // Start een achtergrond-coroutine die continu zinnen klaarzet in de buffer (max 2)
            Coroutine fetchCoroutine = StartCoroutine(BufferSentencesRoutine());

            while (sentenceQueue.Count > 0 || donkeyTTS.GetBufferedClipCount() > 0 || donkeyTTS.IsDownloading)
            {
                // Wacht tot er minimaal 1 audio clip klaar is om af te spelen
                yield return new WaitUntil(() => donkeyTTS.GetBufferedClipCount() > 0 || (!donkeyTTS.IsDownloading && sentenceQueue.Count == 0));

                if (donkeyTTS.GetBufferedClipCount() > 0)
                {
                    // Speel de voorste clip af
                    float clipLength = donkeyTTS.PlayNextClip();

                    // Wacht tot de clip klaar is met afspelen
                    yield return new WaitForSeconds(clipLength);

                    if (pauseBetweenSentences > 0f)
                    {
                        yield return new WaitForSeconds(pauseBetweenSentences);
                    }
                }
            }

            if (fetchCoroutine != null) StopCoroutine(fetchCoroutine);
            isProcessingQueue = false;
        }

        // Deze coroutine vult de buffer achter elkaar aan tot maximaal 2 zinnen
        private IEnumerator BufferSentencesRoutine()
        {
            const int MAX_BUFFER_SIZE = 3;

            while (true)
            {
                // Vult alleen aan als de buffer minder dan 2 zinnen heeft, 
                // er zinnen klaarstaan EN er niet al een download bezig is
                if (donkeyTTS.GetBufferedClipCount() < MAX_BUFFER_SIZE && sentenceQueue.Count > 0 && !donkeyTTS.IsDownloading)
                {
                    SentenceItem nextItem = sentenceQueue.Dequeue();

                    if (nextItem.Text == PARAGRAPH_BREAK_TOKEN)
                    {
                        if (pauseBetweenParagraphs > 0f)
                        {
                            yield return new WaitForSeconds(pauseBetweenParagraphs);
                        }
                        continue;
                    }

                    // Start de download voor deze zin
                    donkeyTTS.EnqueueParagraph(nextItem.Text, nextItem.Language);

                    // Wacht verplicht tot deze specifieke download VOLLEDIG is afgerond
                    // Dit zorgt ervoor dat downloads nooit tegelijk/parallel lopen
                    yield return new WaitUntil(() => !donkeyTTS.IsDownloading);
                }

                yield return null;
            }
        }

        public void StopStream()
        {
            StopAllCoroutines();
            sentenceQueue.Clear();
            if (donkeyTTS != null) donkeyTTS.ClearAll();
            isProcessingQueue = false;
        }
    }
}