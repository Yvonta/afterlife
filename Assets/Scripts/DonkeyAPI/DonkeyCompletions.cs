using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    // Removed : MonoBehaviour inheritance
    public class DonkeyCompletions
    {
        private readonly string _apiEndpoint;
        private readonly string _statusEndpoint;
        private readonly float _pollInterval;
        private readonly int _maxPollAttempts;
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
            public string model = "gemma2:2b";
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
        /// Sends a prompt to the queue endpoint and polls for completion asynchronously.
        /// </summary>
        public async void SendPrompt(string userPrompt, Action<string> onSuccess, Action<string> onError)
        {
            await SendRequestAsync(userPrompt, onSuccess, onError);
        }

        private async Task SendRequestAsync(string userPrompt, Action<string> onSuccess, Action<string> onError)
        {
            // 1. Build Payload
            ChatRequest requestPayload = new ChatRequest
            {
                model = "gemma2:2b",
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

                if (_session != null && !string.IsNullOrEmpty(_session.StoredCookie))
                {
                    Debug.Log($"[Completions] Sending Cookie: {_session.StoredCookie}");
                    request.SetRequestHeader("Cookie", _session.StoredCookie);
                }

                // Send request asynchronously without Coroutines
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                // 3. Handle Initial Response
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Request Error ({request.responseCode}): {request.error}\n{request.downloadHandler?.text}");
                    return;
                }

                QueueResponse initialResponse = JsonUtility.FromJson<QueueResponse>(request.downloadHandler.text);
                
                Debug.Log($"[ChatGPTQueue] Successfully queued with Queue ID: {initialResponse.queue_id}");

                // 4. Poll for Completion
                if (initialResponse != null && initialResponse.queue_id > 0)
                {
                    await PollJobStatusAsync(initialResponse.queue_id, onSuccess, onError);
                }
                else
                {
                    string content = initialResponse?.choices?[0]?.message?.content ?? "No content returned";
                    onSuccess?.Invoke(content);
                }
            }
        }

        private async Task PollJobStatusAsync(int queueId, Action<string> onSuccess, Action<string> onError)
        {
            int attempts = 0;

            while (attempts < _maxPollAttempts)
            {
                await Task.Delay((int)(_pollInterval * 1000));
                attempts++;

                string pollUrl = $"{_statusEndpoint}?id={queueId}";

                using (UnityWebRequest pollRequest = UnityWebRequest.Get(pollUrl))
                {
                    if (_session != null && !string.IsNullOrEmpty(_session.StoredCookie))
                    {
                        pollRequest.SetRequestHeader("Cookie", _session.StoredCookie);
                    }
                    
                    var operation = pollRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (pollRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[ChatGPTQueue] Poll attempt {attempts} failed: {pollRequest.error}");
                        continue;
                    }

                    QueueResponse pollResponse = JsonUtility.FromJson<QueueResponse>(pollRequest.downloadHandler.text);

                    if (pollResponse == null)
                    {
                        continue;
                    }

                    // Check status return values
                    if (pollResponse.status == "completed")
                    {
                        string finalMessage = pollResponse.choices?[0]?.message?.content ?? "No content.";
                        onSuccess?.Invoke(finalMessage);
                        return;
                    }
                    else if (pollResponse.status == "failed")
                    {
                        onError?.Invoke("Job processing failed on the backend worker.");
                        return;
                    }

                    Debug.Log($"[ChatGPTQueue] Job {queueId} status: '{pollResponse.status}'. Retrying in {_pollInterval}s...");
                }
            }

            onError?.Invoke("Polling timed out waiting for the background worker to finish.");
        }
    }
}