using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    public class DonkeyCompletions : MonoBehaviour
    {
        private string _apiEndpoint;
        private string _statusEndpoint;
        private float _pollInterval;
        private int _maxPollAttempts;


        private readonly DonkeySession _session;

        #region Data Structures (OpenAI Compatible)

        [Serializable]
        public class ChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        public class ChatRequest
        {
            public string model = "gpt-4o";
            public ChatMessage[] messages;
            public bool stream = false;
        }

        [Serializable]
        public class QueueResponse
        {
            public string id;
            public string status;
            public int queue_id;
            public Choice[] choices;
        }

        [Serializable]
        public class Choice
        {
            public int index;
            public ChatMessage message;
            public string finish_reason;
        }

        #endregion

        public DonkeyCompletions(string apiEndpoint, string statusEndpoint, float pollInterval, int maxPollAttempts, DonkeySession session)
        {
            _session = session;
            _apiEndpoint = apiEndpoint;
            _statusEndpoint = statusEndpoint;   
            _pollInterval = pollInterval;
            _maxPollAttempts = maxPollAttempts;
        }

        /// <summary>
        /// Sends a prompt to the queue endpoint and starts polling for completion.
        /// </summary>
        public void SendPrompt(string userPrompt, Action<string> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendRequestRoutine(userPrompt, onSuccess, onError));
        }

        private IEnumerator SendRequestRoutine(string userPrompt, Action<string> onSuccess, Action<string> onError)
        {
            // 1. Build Payload
            ChatRequest requestPayload = new ChatRequest
            {
                model = "gpt-4o",
                messages = new ChatMessage[]
                {
                    new ChatMessage { role = "user", content = userPrompt }
                }
            };

            string jsonBody = JsonUtility.ToJson(requestPayload);

            // 2. Prepare Web Request
            using (UnityWebRequest request = new UnityWebRequest(_apiEndpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(_session.StoredCookie))
                {
                    Debug.Log($"[Completions] Sending Cookie: {_session.StoredCookie}");
                    request.SetRequestHeader("Cookie", _session.StoredCookie);
                }

                yield return request.SendWebRequest();

                // 3. Handle Initial Response
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Request Error ({request.responseCode}): {request.error}\n{request.downloadHandler.text}");
                    yield break;
                }

                QueueResponse initialResponse = JsonUtility.FromJson<QueueResponse>(request.downloadHandler.text);
                
                Debug.Log($"[ChatGPTQueue] Successfully queued with Queue ID: {initialResponse.queue_id}");

                // 4. Poll for Completion (Optional, based on your implementation)
                if (initialResponse.queue_id > 0)
                {
                    StartCoroutine(PollJobStatusRoutine(initialResponse.queue_id, onSuccess, onError));
                }
                else
                {
                    // If the PHP endpoint completed synchronously, return immediately
                    string content = initialResponse.choices?[0]?.message?.content ?? "No content returned";
                    onSuccess?.Invoke(content);
                }
            }
        }

        private IEnumerator PollJobStatusRoutine(int queueId, Action<string> onSuccess, Action<string> onError)
        {
            int attempts = 0;

            while (attempts < _maxPollAttempts)
            {
                yield return new WaitForSeconds(_pollInterval);
                attempts++;

                string pollUrl = $"{_statusEndpoint}?id={queueId}";

                using (UnityWebRequest pollRequest = UnityWebRequest.Get(pollUrl))
                {
                    if (!string.IsNullOrEmpty(_session.StoredCookie))
                    {
                        Debug.Log($"[Completions] Sending Cookie: {_session.StoredCookie}");
                        pollRequest.SetRequestHeader("Cookie", _session.StoredCookie);
                    }
                    
                    yield return pollRequest.SendWebRequest();

                    if (pollRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[ChatGPTQueue] Poll attempt {attempts} failed: {pollRequest.error}");
                        continue;
                    }

                    QueueResponse pollResponse = JsonUtility.FromJson<QueueResponse>(pollRequest.downloadHandler.text);

                    // Check status return values
                    if (pollResponse.status == "completed")
                    {
                        string finalMessage = pollResponse.choices?[0]?.message?.content ?? "No content.";
                        onSuccess?.Invoke(finalMessage);
                        yield break;
                    }
                    else if (pollResponse.status == "failed")
                    {
                        onError?.Invoke("Job processing failed on the backend worker.");
                        yield break;
                    }

                    Debug.Log($"[ChatGPTQueue] Job {queueId} status: '{pollResponse.status}'. Retrying in {_pollInterval}s...");
                }
            }

            onError?.Invoke("Polling timed out waiting for the background worker to finish.");
        }
    }
}