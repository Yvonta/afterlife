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

        // Default to standard local Ollama endpoint (change if using a custom gateway)
        [SerializeField] private string defaultApiUrl = "http://localhost:11434/api/generate";
        private StringBuilder buffer = new StringBuilder();
        private DonkeySession session;

        // Initialize with session reference
        public void Initialize(DonkeySession activeSession)
        {
            this.session = activeSession;
            Debug.Log($"[DonkeyLLMStreaming] Initialized with session instance: {(session != null ? "Valid" : "Null")}");
        }

        public void RequestStream(string prompt, SentenceReceivedHandler onSentenceReady)
        {
            RequestStream(prompt, onSentenceReady, defaultApiUrl);
        }

        public void RequestStream(string prompt, SentenceReceivedHandler onSentenceReady, string overrideUrl)
        {
            string targetUrl = !string.IsNullOrEmpty(overrideUrl) ? overrideUrl : defaultApiUrl;
            Debug.Log($"[DonkeyLLMStreaming] RequestStream triggered. URL: {targetUrl} | Prompt: \"{prompt}\"");
            StartCoroutine(StreamRoutine(prompt, targetUrl, onSentenceReady));
        }

        private IEnumerator StreamRoutine(string prompt, string targetUrl, SentenceReceivedHandler onSentenceReady)
        {
            buffer.Clear();

            // Ollama standard generation payload format
            string jsonPayload = $"{{\"model\":\"gemma2:2b\",\"prompt\":\"{EscapeJson(prompt)}\",\"stream\":true}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

            Debug.Log($"[DonkeyLLMStreaming] Sending Request to: {targetUrl}\nPayload: {jsonPayload}");

            using (UnityWebRequest request = new UnityWebRequest(targetUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(payloadBytes);
                request.uploadHandler.contentType = "application/json";

                // Attach Session Cookie if applicable
                string sessionId = session != null ? session.StoredCookie : DonkeySessionSave.LoadSession();
                if (!string.IsNullOrEmpty(sessionId))
                {
                    string cookieHeader = sessionId.StartsWith("SESSION=") ? sessionId : $"SESSION={sessionId}";
                    request.SetRequestHeader("Cookie", cookieHeader);
                    Debug.Log($"[DonkeyLLMStreaming] Sending Cookie Header: {cookieHeader}");
                }
                else
                {
                    Debug.LogWarning("[DonkeyLLMStreaming] Warning: No active session cookie found for LLM request.");
                }

                SentenceDownloadHandler streamHandler = new SentenceDownloadHandler(onSentenceReady, buffer);
                request.downloadHandler = streamHandler;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DonkeyLLMStreaming] Error sending to {targetUrl}: {request.error} | Response Code: {request.responseCode} | Raw Response: {request.downloadHandler.text}");
                }
                else
                {
                    Debug.Log($"[DonkeyLLMStreaming] Request succeeded ({request.responseCode}). Flushing remaining buffer...");
                    streamHandler.FlushRemaining(onSentenceReady);
                    Debug.Log("[DonkeyLLMStreaming] Stream completed successfully.");
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

            // Compatible with standard Ollama ("response": "...") and OpenAI-style ("content": "...") streaming lines
            private static readonly Regex ResponseRegex = new Regex(@"\""(?:response|content)\""\s*:\s*\""(.*?)\""", RegexOptions.Compiled);
            private static readonly Regex SentenceBoundaryRegex = new Regex(@"(?<=[.!?])\s+|(\r?\n){2,}|\r?\n", RegexOptions.Compiled);

            public SentenceDownloadHandler(SentenceReceivedHandler callback, StringBuilder buffer) : base(new byte[4096])
            {
                sentenceCallback = callback;
                textBuffer = buffer;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength < 1) return true;

                string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
                Debug.Log($"[SentenceDownloadHandler] Received Data Chunk ({dataLength} bytes): \"{chunk}\"");

                rawChunkBuffer.Append(chunk);

                string rawText = rawChunkBuffer.ToString();
                string[] lines = rawText.Split('\n');

                // Retain the last incomplete line fragment in the chunk buffer
                rawChunkBuffer.Clear();
                rawChunkBuffer.Append(lines[lines.Length - 1]);

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    Match match = ResponseRegex.Match(line);
                    if (match.Success)
                    {
                        string extractedContent = UnescapeJsonString(match.Groups[1].Value);
                        Debug.Log($"[SentenceDownloadHandler] Token Extracted: \"{extractedContent}\"");
                        textBuffer.Append(extractedContent);
                    }
                    else
                    {
                        Debug.LogWarning($"[SentenceDownloadHandler] Non-JSON or HTML Error Line Received: \"{line}\"");
                    }
                }

                ProcessSentences();
                return true;
            }

            private void ProcessSentences()
            {
                string currentText = textBuffer.ToString();
                MatchCollection matches = SentenceBoundaryRegex.Matches(currentText);

                if (matches.Count > 0)
                {
                    int lastCutIndex = 0;

                    foreach (Match match in matches)
                    {
                        int length = match.Index - lastCutIndex;
                        string sentence = currentText.Substring(lastCutIndex, length).Trim();

                        if (!string.IsNullOrEmpty(sentence))
                        {
                            Debug.Log($"[SentenceDownloadHandler] Dispatching Sentence: \"{sentence}\"");
                            sentenceCallback?.Invoke(sentence);
                        }

                        if (Regex.IsMatch(match.Value, @"(\r?\n){2,}"))
                        {
                            Debug.Log("[SentenceDownloadHandler] Dispatching [PARAGRAPH_BREAK]");
                            sentenceCallback?.Invoke("[PARAGRAPH_BREAK]");
                        }

                        lastCutIndex = match.Index + match.Length;
                    }

                    textBuffer.Clear();
                    textBuffer.Append(currentText.Substring(lastCutIndex));
                    Debug.Log($"[SentenceDownloadHandler] Leftover buffer after sentence boundary split: \"{textBuffer}\"");
                }
            }

            public void FlushRemaining(SentenceReceivedHandler callback)
            {
                Debug.Log($"[SentenceDownloadHandler] Flushing... Raw Leftover: \"{rawChunkBuffer}\" | Buffer Leftover: \"{textBuffer}\"");

                if (rawChunkBuffer.Length > 0)
                {
                    Match match = ResponseRegex.Match(rawChunkBuffer.ToString());
                    if (match.Success)
                    {
                        string extracted = UnescapeJsonString(match.Groups[1].Value);
                        Debug.Log($"[SentenceDownloadHandler] Flush extracted token: \"{extracted}\"");
                        textBuffer.Append(extracted);
                    }
                }

                string remaining = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(remaining))
                {
                    Debug.Log($"[SentenceDownloadHandler] Final sentence dispatched on flush: \"{remaining}\"");
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