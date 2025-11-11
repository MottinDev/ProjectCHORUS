using UnityEngine;

[CreateAssetMenu(fileName = "ServerConfig", menuName = "Server/Server Config", order = 1)]
public class ServerConfig : ScriptableObject
{
    public string ServiceAccountId;
    public string ServiceAccountSecret;
}