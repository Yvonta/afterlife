using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    [Serializable]
    public class DonkeyLoginParams
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class LoginResultData
    {
        public int userid;
        public string email;
        public string sessionid;
        public string role;
        public string name;
    }

    [Serializable]
    public class LoginResult
    {
        public int code;
        public string message;
        public LoginResultData data;
    }

    [Serializable]
    public class DonkeyJsonRpcResponse
    {
        public string jsonrpc;
        public string result; // Kept as string or parsed carefully since result is an object in your server response
        public string error;
    }

    public class DonkeySession
    {
        private readonly string _serverUrl;
        private int _idCounter = 0;
        private string _storedCookie;

        public string StoredCookie => _storedCookie;

        public DonkeySession(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        public async Task<string> LoginAsync(string email, string password)
        {
            int requestId = ++_idCounter;

            string jsonBody = BuildJsonPayload("login", email, password, requestId);

            using (UnityWebRequest www = new UnityWebRequest(_serverUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(_storedCookie))
                {
                    www.SetRequestHeader("Cookie", _storedCookie);
                }

                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Login Network Error: {www.error} | Response: {www.downloadHandler.text}");
                }

                string setCookieHeader = www.GetResponseHeader("Set-Cookie");
                if (!string.IsNullOrEmpty(setCookieHeader))
                {
                    _storedCookie = setCookieHeader;
                }

                string responseString = www.downloadHandler.text;
                Debug.Log($"Raw JSON-RPC Server Response: {responseString}");

                return ParseResponse(responseString);
            }
        }

        private string BuildJsonPayload(string method, string email, string password, int id)
        {
            return $"{{\"jsonrpc\":\"2.0\",\"method\":\"{method}\",\"params\":{{\"email\":\"{email}\",\"password\":\"{password}\"}},\"id\":{id}}}";
        }

        private string ParseResponse(string jsonResponse)
        {
            // Simple check based on your server's explicit response pattern
            if (jsonResponse.Contains("\"error\"") && !jsonResponse.Contains("\"error\":null"))
            {
                // Check if error is actually a string or object
                int errorIdx = jsonResponse.IndexOf("\"error\"");
                throw new Exception($"JSON-RPC Server Error Response: {jsonResponse}");
            }

            return jsonResponse;
        }
    }
}