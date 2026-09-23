using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class SteamLobbyController : MonoBehaviour
{
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private TMP_InputField joinPasswordInput;

    private readonly List<CSteamID> foundLobbies = new List<CSteamID>();
    private readonly Dictionary<CSteamID, GameObject> lobbyItems =
        new Dictionary<CSteamID, GameObject>();

    private bool isHostingLobby;
    private NetworkManager networkManager;
    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<LobbyMatchList_t> lobbyMatchList;
    private Callback<LobbyDataUpdate_t> lobbyDataUpdate;
    private Callback<LobbyEnter_t> lobbyEntered;
    private CSteamID currentLobbyId;

    protected abstract string CreateHostAddress();
    protected abstract string CreateLobbyName();

    protected virtual void Start()
    {
        networkManager = GetComponent<NetworkManager>();
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    protected virtual void OnDestroy()
    {
        lobbyCreated?.Dispose();
        lobbyMatchList?.Dispose();
        lobbyDataUpdate?.Dispose();
        lobbyEntered?.Dispose();
    }

    public void CreateLobby()
    {
        if (!SteamManager.Initialized || networkManager == null) return;
        isHostingLobby = true;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, networkManager.maxConnections);
    }

    public void RefreshLobbyList()
    {
        if (!SteamManager.Initialized) return;

        foundLobbies.Clear();
        ClearLobbyItems();
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            "LobbyName",
            string.Empty,
            ELobbyComparison.k_ELobbyComparisonNotEqual);
        SteamMatchmaking.RequestLobbyList();
    }

    public void JoinLobby(CSteamID lobbyId)
    {
        string password = SteamMatchmaking.GetLobbyData(lobbyId, "LobbyPassword");
        if (!string.IsNullOrEmpty(password) && joinPasswordInput.text != password)
        {
            Debug.LogError("Неверный пароль!");
            return;
        }

        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public void InviteFriends()
    {
        if (currentLobbyId.IsValid())
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyId);
    }

    public void LeaveLobby()
    {
        isHostingLobby = false;

        if (currentLobbyId.IsValid())
        {
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            currentLobbyId = CSteamID.Nil;
        }

        if (networkManager == null) return;
        if (NetworkServer.active && NetworkClient.active)
            networkManager.StopHost();
        else if (NetworkClient.active)
            networkManager.StopClient();
        else if (NetworkServer.active)
            networkManager.StopServer();
    }

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult != EResult.k_EResultOK) return;

        currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(currentLobbyId, "HostAddress", CreateHostAddress());
        SteamMatchmaking.SetLobbyData(currentLobbyId, "LobbyName", CreateLobbyName());

        string password = createPasswordInput != null ? createPasswordInput.text : string.Empty;
        if (!string.IsNullOrEmpty(password))
            SteamMatchmaking.SetLobbyData(currentLobbyId, "LobbyPassword", password);

        networkManager.StartHost();
    }

    private void OnLobbyMatchList(LobbyMatchList_t result)
    {
        for (int i = 0; i < result.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            foundLobbies.Add(lobbyId);
            SteamMatchmaking.RequestLobbyData(lobbyId);
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t result)
    {
        CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);
        if (!foundLobbies.Contains(lobbyId) || lobbyItems.ContainsKey(lobbyId)) return;

        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyId, "LobbyName");
        if (!MatchesSearch(lobbyName)) return;

        GameObject item = Instantiate(lobbyItemPrefab, lobbyListContainer);
        lobbyItems[lobbyId] = item;

        TextMeshProUGUI[] labels = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (labels.Length >= 2)
        {
            labels[0].text = lobbyName;
            labels[1].text =
                $"{SteamMatchmaking.GetNumLobbyMembers(lobbyId)}/{networkManager.maxConnections}";
        }

        Button button = item.GetComponentInChildren<Button>();
        if (button != null) button.onClick.AddListener(() => JoinLobby(lobbyId));
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if (result.m_EChatRoomEnterResponse != 1)
        {
            Debug.LogError("Не удалось войти в лобби");
            return;
        }

        if (isHostingLobby) return;

        currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
        networkManager.networkAddress =
            SteamMatchmaking.GetLobbyData(currentLobbyId, "HostAddress");
        networkManager.StartClient();
    }

    private bool MatchesSearch(string lobbyName)
    {
        string query = searchField != null ? searchField.text : string.Empty;
        return string.IsNullOrEmpty(query) ||
               lobbyName.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ClearLobbyItems()
    {
        foreach (GameObject item in lobbyItems.Values)
        {
            if (item != null) Destroy(item);
        }

        lobbyItems.Clear();
    }
}
