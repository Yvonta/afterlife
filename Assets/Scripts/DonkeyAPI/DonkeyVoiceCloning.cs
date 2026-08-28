using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
public class DonkeyVoiceCloning
{
    private string serverUrl;
    private DonkeySession session;

    public void Initialize(DonkeySession session, string url)
    {
        this.session = session;
        this.serverUrl = url;
    }

    /// <summary>
    /// Uploads an audio sample to the PHP endpoint to create a voice clone.
    /// </summary>
    /// <param name="audioFilePath">Full path to the local audio file (e.g., Application.persistentDataPath + "/sample.wav")</param>
    /// <param name="voiceName">The name to assign to the cloned voice</param>
    /// <param name="languageCode">Language code (e.g., "en", "es")</param>
    /// <returns>JSON string response from the server</returns>
    public async Task<string> CloneVoiceAsync(byte[] audioBytes, string mimeType, string voiceName, string languageCode)
    {
        string fileName = "voice." + "wav";

        // Construct multipart form data matching the PHP $_FILES and $_POST keys
        WWWForm form = new WWWForm();
        
        // PHP expects $_FILES['sample']
        form.AddBinaryData("sample", audioBytes, fileName, mimeType);
        
        // PHP expects $_POST['name'] and $_POST['language']
        form.AddField("name", voiceName);
        form.AddField("language", languageCode);

        using (UnityWebRequest request = UnityWebRequest.Post(serverUrl, form))
        {
            string sessionId = session != null ? session.StoredCookie : DonkeySessionSave.LoadSession();
            if (!string.IsNullOrEmpty(sessionId))
            {
                string cookieHeader = sessionId.StartsWith("SESSION=") ? sessionId : $"SESSION={sessionId}";
                request.SetRequestHeader("Cookie", cookieHeader);
            }
            
            // Wait for request completion asynchronously
            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VoiceCloneClient] Error ({request.responseCode}): {request.error}");
                Debug.LogError($"[VoiceCloneClient] Response Body: {request.downloadHandler.text}");
                return null;
            }

            Debug.Log("[VoiceCloneClient] Voice cloning request successful!");
            return request.downloadHandler.text;
        }
    }

    // Helper method to wrap UnityWebRequest's async operation into a C# Task
    private Task SendRequestAsync(UnityWebRequest request)
    {
        var tcs = new TaskCompletionSource<bool>();
        var operation = request.SendWebRequest();

        operation.completed += _ => tcs.SetResult(true);

        return tcs.Task;
    }

    // Helper to supply basic MIME types based on file extensions
    private string GetMimeType(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };
    }
}
}