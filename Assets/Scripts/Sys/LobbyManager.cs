using Steamworks;
using UnityEngine;
using Zenject;

public class LobbyManager : MonoBehaviour
{
    [Inject] private NetworkService networkService;

    private CSteamID currentLobbyID;

    void OnEnable()
    {
        if (!SteamManager.Initialized) return;

        Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
    }

    public void CreateLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam not initialized!");
            return;
        }

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 6);
    }

    public void JoinLobby(CSteamID lobbyID)
    {
        if (!SteamManager.Initialized) return;
        SteamMatchmaking.JoinLobby(lobbyID);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to create lobby!");
            return;
        }

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "HostAddress", SteamUser.GetSteamID().ToString());

        networkService.StartHost();
        Debug.Log("Lobby created and host started!");
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyID, "HostAddress");

        if (string.IsNullOrEmpty(hostAddress))
        {
            Debug.LogError("No host address found in lobby!");
            return;
        }

        networkService.StartClient(hostAddress);
        Debug.Log("Joined lobby and connected to host!");
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        JoinLobby(callback.m_steamIDLobby);
    }
}