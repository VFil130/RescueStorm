using Mirror;
using UnityEngine;

public class NetworkService
{
    private NetworkManager networkManager;

    public NetworkService()
    {
        networkManager = NetworkManager.singleton;
    }

    public void StartHost() => networkManager.StartHost();
    public void StartClient(string address) => networkManager.StartClient();
    public bool IsHost => NetworkServer.active && NetworkClient.active;
    public bool IsClient => NetworkClient.active && !NetworkServer.active;
    public bool IsServer => NetworkServer.active && !NetworkClient.active;
}