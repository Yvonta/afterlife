using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    public class DonkeyLLMStreaming : MonoBehaviour
    {
        public delegate void SentenceReceivedHandler(string sentence);

        [SerializeField] private string defaultApiUrl = "https://ultireal.com/appapi/v2/llm.php";
        private StringBuilder buffer = new StringBuilder();

        public void RequestStream(string prompt, SentenceReceivedHandler onSentenceReady)
        {
            RequestStream(prompt, onSentenceReady, defaultApiUrl);
        }

        public void RequestStream(string prompt, SentenceReceivedHandler onSentenceReady, string overrideUrl)
        {
            string targetUrl = !string.IsNullOrEmpty(overrideUrl) ? overrideUrl : defaultApiUrl;
            StartCoroutine(StreamRoutine(prompt, targetUrl, onSentenceReady));
        }

        private IEnumerator StreamRoutine(string prompt, string targetUrl, SentenceReceivedHandler onSentenceReady)
        {
            buffer.Clear();

            string jsonPayload = $"{{\"model\":\"gemma2:2b\",\"prompt\":\"{EscapeJson(prompt)}\",\"stream\":true}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(targetUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(payloadBytes);
                request.uploadHandler.contentType = "application/json";

                SentenceDownloadHandler streamHandler = new SentenceDownloadHandler(onSentenceReady, buffer);
                request.downloadHandler = streamHandler;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DonkeyGPTStreaming] Error sending to {targetUrl}: {request.error}");
                }
                else
                {
                    streamHandler.FlushRemaining(onSentenceReady);
                }
            }
        }

        private string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private class SentenceDownloadHandler : DownloadHandlerScript
        {
            private readonly SentenceReceivedHandler sentenceCallback;
            private readonly StringBuilder textBuffer;
            private StringBuilder rawChunkBuffer = new StringBuilder();

            private static readonly Regex ResponseRegex = new Regex(@"\""response\""\s*:\s*\""(.*?)\""", RegexOptions.Compiled);
            private static readonly Regex TokenRegex = new Regex(@"(?<=[.!?])\s+|(\r?\n){2,}|\r?\n", RegexOptions.Compiled);

            // Belangrijk: Geef een buffer-size mee aan base() voor correcte Unity streaming
            public SentenceDownloadHandler(SentenceReceivedHandler callback, StringBuilder buffer) : base(new byte[4096])
            {
                sentenceCallback = callback;
                textBuffer = buffer;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength < 1) return true;

                string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
                rawChunkBuffer.Append(chunk);

                // Verwerkt regels zodra er een \n (newline) binnenkomt van de NDJSON-stream
                string rawText = rawChunkBuffer.ToString();
                string[] lines = rawText.Split('\n');

                // Bewaar de laatste onvolledige regel in de buffer
                rawChunkBuffer.Clear();
                rawChunkBuffer.Append(lines[lines.Length - 1]);

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Extraheer "response":"..." uit elke JSON-regel
                    Match match = ResponseRegex.Match(line);
                    if (match.Success)
                    {
                        string extractedContent = UnescapeJsonString(match.Groups[1].Value);
                        textBuffer.Append(extractedContent);
                    }
                }

                ProcessSentences();
                return true;
            }

            private void ProcessSentences()
            {
                string currentText = textBuffer.ToString();
                MatchCollection matches = TokenRegex.Matches(currentText);

                if (matches.Count > 0)
                {
                    int lastCutIndex = 0;

                    foreach (Match match in matches)
                    {
                        int length = match.Index - lastCutIndex;
                        string sentence = currentText.Substring(lastCutIndex, length).Trim();

                        if (!string.IsNullOrEmpty(sentence))
                        {
                            sentenceCallback?.Invoke(sentence);
                        }

                        if (Regex.IsMatch(match.Value, @"(\r?\n){2,}"))
                        {
                            sentenceCallback?.Invoke("[PARAGRAPH_BREAK]");
                        }

                        lastCutIndex = match.Index + match.Length;
                    }

                    textBuffer.Clear();
                    textBuffer.Append(currentText.Substring(lastCutIndex));
                }
            }

            public void FlushRemaining(SentenceReceivedHandler callback)
            {
                // Verwerk de allerlaatste restjes in rawChunkBuffer
                if (rawChunkBuffer.Length > 0)
                {
                    Match match = ResponseRegex.Match(rawChunkBuffer.ToString());
                    if (match.Success)
                    {
                        textBuffer.Append(UnescapeJsonString(match.Groups[1].Value));
                    }
                }

                string remaining = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(remaining))
                {
                    callback?.Invoke(remaining);
                    textBuffer.Clear();
                }
            }

            private string UnescapeJsonString(string text)
            {
                return text.Replace("\\\"", "\"")
                           .Replace("\\\\", "\\")
                           .Replace("\\n", "\n")
                           .Replace("\\r", "\r")
                           .Replace("\\t", "\t");
            }
        }
    }
}