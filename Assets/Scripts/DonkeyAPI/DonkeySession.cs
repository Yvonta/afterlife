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
        public string sessionid;
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
        public LoginResult result; 
        public string error;
    }

    [System.Serializable]
    public class LoggedInResultData
    {
        public int code;
        public string message;
    }

    [System.Serializable]
    public class LoggedInResponseData
    {
        public string jsonrpc;
        public int id;
        public LoggedInResultData result;
        public string error;
    }

    public class DonkeySession
    {
	private readonly string _serverUrl;
        public static int _idCounter = 0;
        private string _storedSessionId;
	private DonkeyJsonRpcResponse _response;	

        public string StoredCookie => _storedSessionId;

        public DonkeySession(string serverUrl)
        {
            _serverUrl = serverUrl;
            LoadSessionFromDisk();
        }

        public async Task<string> LoginAsync(string email, string password)
        {
            int requestId = ++DonkeySession._idCounter;
            string jsonBody = BuildLoginPayload(email, password, requestId);

            using (UnityWebRequest www = new UnityWebRequest(_serverUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

//		if (!string.IsNullOrEmpty(_storedSessionId))
//		{
//		    www.SetRequestHeader("Cookie", $"SESSION={_storedSessionId}");
//		    Debug.Log($"Sending Cookie: SESSION={_storedSessionId}");
//		}

                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Login Network Error: {www.error} | Response: {www.downloadHandler.text}");
                }

//                string setCookieHeader = www.GetResponseHeader("Set-Cookie");
//                if (!string.IsNullOrEmpty(setCookieHeader))
//                {
//                    ExtractAndSaveSessionId(setCookieHeader);
//                }

                string responseString = www.downloadHandler.text;
                Debug.Log($"[Login] Server Response: {responseString}");
		string jsonResponse = ParseResponse(responseString);
		_response = JsonUtility.FromJson<DonkeyJsonRpcResponse>(jsonResponse);
		DonkeySessionSave.SaveSession(_response.result.data.sessionid);
		return jsonResponse;	
            }
        }

        public async Task<bool> IsLoggedInAsync()
        {
            int requestId = ++_idCounter;
            string jsonBody = BuildIsLoggedInPayload(requestId);

            using (UnityWebRequest www = new UnityWebRequest(_serverUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(_storedSessionId))
                {
                    www.SetRequestHeader("Cookie", $"SESSION={_storedSessionId}");
                    Debug.Log($"Sending Cookie: SESSION={_storedSessionId}");
                }

                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if(www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"IsLoggedIn Network Error: {www.error} | Response: {www.downloadHandler.text}");
                }

                string setCookieHeader = www.GetResponseHeader("Set-Cookie");
                if (!string.IsNullOrEmpty(setCookieHeader))
                {
                    ExtractAndSaveSessionId(setCookieHeader);
                }

                string responseString = www.downloadHandler.text;
                Debug.Log($"[IsLoggedIn] Server Response: {responseString}");

                LoggedInResponseData jsobj = JsonUtility.FromJson<LoggedInResponseData>(responseString);

                return jsobj.result.code == 0;
            }
        }

        public void ClearSession()
        {
            _storedSessionId = null;
	    DonkeySessionSave.ClearSession();
            Debug.Log("[DonkeySession] Session cleared.");
        }

        private string BuildLoginPayload(string email, string password, int id)
        {
            return $"{{\"jsonrpc\":\"2.0\",\"method\":\"login\",\"params\":{{\"email\":\"{email}\",\"password\":\"{password}\"}},\"id\":{id}}}";
        }

        private string BuildIsLoggedInPayload(int id)
        {
            return $"{{\"jsonrpc\":\"2.0\",\"method\":\"isloggedin\", \"params\": {{}},\"id\":{id}}}";
        }

        private void ExtractAndSaveSessionId(string setCookieHeader)
	{
	    if (string.IsNullOrWhiteSpace(setCookieHeader))
		return;

	    foreach (string cookie in setCookieHeader.Split(','))
	    {
		foreach (string part in cookie.Split(';'))
		{
		    string trimmed = part.Trim();

		    int equals = trimmed.IndexOf('=');
		    if (equals < 0)
		        continue;

		    string name = trimmed.Substring(0, equals);

		    if (name.Equals("SESSION", StringComparison.OrdinalIgnoreCase))
		    {
		        _storedSessionId = trimmed.Substring(equals + 1);

			DonkeySessionSave.SaveSession(_storedSessionId);

		        Debug.Log($"[DonkeySession] Session saved: {_storedSessionId}");
		        return;
		    }
		}
	    }
	}

		public async Task<string> RegisterAsync(string email, string password, string name, string gender, string age)
		{
		    int requestId = ++DonkeySession._idCounter;
		    string jsonBody = BuildRegidterPayload(email, password, name, gender, age, requestId);

		    using (UnityWebRequest www = new UnityWebRequest(_serverUrl, "POST"))
		    {
		        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
		        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
		        www.downloadHandler = new DownloadHandlerBuffer();
		        www.SetRequestHeader("Content-Type", "application/json");

		        var operation = www.SendWebRequest();
		        while (!operation.isDone) await Task.Yield();

		        if (www.result != UnityWebRequest.Result.Success)
		        {
		            throw new Exception($"Register Network Error: {www.error} | Response: {www.downloadHandler.text}");
		        }

		        string responseString = www.downloadHandler.text;
		        Debug.Log($"[Register] Server Response: {responseString}");
			string jsonResponse = ParseResponse(responseString);
			_response = JsonUtility.FromJson<DonkeyJsonRpcResponse>(jsonResponse);
			DonkeySessionSave.SaveSession(_response.result.data.sessionid);
			return jsonResponse;	
		    }
		}
		
		private string BuildRegidterPayload(string email, string password, string name, string gender, string age, int id)
		{
		    // Always include email and password for login, plus sessionid if available
		    return $"{{\"jsonrpc\":\"2.0\",\"method\":\"register\",\"params\":{{\"email\":\"{email}\",\"password\":\"{password}\",\"name\":\"{name}\",\"gender\":\"{gender}\",\"age\":\"{age}\"}},\"id\":{id}}}";
		}
 
        private void LoadSessionFromDisk()
        {
	    _storedSessionId = DonkeySessionSave.LoadSession();	
	    if (_storedSessionId != null)
            {
                Debug.Log($"[DonkeySession] Loaded Session ID from Disk: {_storedSessionId}");
            }
	    else
	    {
		Debug.Log("New session!");
	    }
        }

        private static string ParseResponse(string jsonResponse)
        {
            if (jsonResponse.Contains("\"error\"") && !jsonResponse.Contains("\"error\":null"))
            {
                throw new Exception($"JSON-RPC Server Error Response: {jsonResponse}");
            }
            return jsonResponse;
        }
    }
}