using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    [Serializable]
    public class DonkeyJsonRpcRequest
    {
        public string jsonrpc = "2.0";
        public string method;
        public string @params;
        public object id;
    }

    [Serializable]
    public class JsonRpcResponse
    {
        public string jsonrpc;
        public string result;
        public JsonRpcError error;
        public string id;
    }

    [Serializable]
    public class JsonRpcError
    {
        public int code;
        public string message;
        public string data;
    }
}