using System.IO;
using UnityEngine;

namespace Donkey
{

public static class DonkeySessionSave
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "session.json");

    [System.Serializable]
    private class SessionData
    {
        public string sessionId;
    }

    public static void SaveSession(string sessionId)
    {
        SessionData data = new SessionData { sessionId = sessionId };
        string json = JsonUtility.ToJson(data);
	File.WriteAllText(FilePath, json); // Fixed from File.Write.AllText
	Debug.Log("Saving session to: " + FilePath);
    }

    public static string LoadSession()
    {
        if (File.Exists(FilePath))
        {
            string json = File.ReadAllText(FilePath);
            SessionData data = JsonUtility.FromJson<SessionData>(json);
            return data?.sessionId;
        }
        return null;
    }

    public static void ClearSession()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}

}