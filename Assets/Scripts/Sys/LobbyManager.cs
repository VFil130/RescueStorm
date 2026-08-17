using Mirror;
using Steamworks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    private NetworkManager networkManager;
    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<LobbyMatchList_t> lobbyMatchList;
    private Callback<LobbyDataUpdate_t> lobbyDataUpdate;
    private Callback<LobbyEnter_t> lobbyEntered;

    private CSteamID currentLobbyID;
    private List<CSteamID> foundLobbies = new List<CSteamID>();
    private Dictionary<CSteamID, GameObject> lobbyUIItems = new Dictionary<CSteamID, GameObject>();

    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private TMP_InputField joinPasswordInput;
    void Start()
    {
        networkManager = GetComponent<NetworkManager>();
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    #region Создание лобби
    public void CreateLobby()
    {
        if (!SteamManager.Initialized) return;

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, networkManager.maxConnections);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "HostAddress", SteamUser.GetSteamID().ToString());
        string playerName = SteamFriends.GetPersonaName();
        string lobbyName = playerName + "_" + Random.Range(10000, 99999).ToString();
        SteamMatchmaking.SetLobbyData(currentLobbyID, "LobbyName", lobbyName);
        if (!string.IsNullOrEmpty(createPasswordInput.text))
        {
            SteamMatchmaking.SetLobbyData(currentLobbyID, "LobbyPassword", createPasswordInput.text);
        }
        networkManager.StartHost();
    }
    #endregion

    #region Получение списка лобби
    public void RefreshLobbyList()
    {
        if (!SteamManager.Initialized) return;

        foundLobbies.Clear();
        ClearLobbyUI();

        SteamMatchmaking.AddRequestLobbyListStringFilter("LobbyName", "", ELobbyComparison.k_ELobbyComparisonNotEqual);
        SteamMatchmaking.RequestLobbyList();
    }

    private void ClearLobbyUI()
    {
        foreach (var item in lobbyUIItems.Values)
        {
            Destroy(item);
        }
        lobbyUIItems.Clear();
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            foundLobbies.Add(lobbyID);
            SteamMatchmaking.RequestLobbyData(lobbyID);
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        if (!foundLobbies.Contains(lobbyID)) return;
        if (lobbyUIItems.ContainsKey(lobbyID)) return;

        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "LobbyName");
        if (!IsLobbyMatchesSearch(lobbyName)) return;
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
        int maxMembers = networkManager.maxConnections;

        GameObject item = Instantiate(lobbyItemPrefab, lobbyListContainer);
        lobbyUIItems[lobbyID] = item;

        TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 2)
        {
            texts[0].text = lobbyName;
            texts[1].text = $"{memberCount}/{maxMembers}";
        }

        Button button = item.GetComponentInChildren<Button>();
        if (button != null)
        {
            CSteamID capturedID = lobbyID;
            button.onClick.AddListener(() => JoinLobby(capturedID));
        }
    }
    private bool IsLobbyMatchesSearch(string lobbyName)
    {
        if (string.IsNullOrEmpty(searchField.text)) return true;
        return lobbyName.ToLower().Contains(searchField.text.ToLower());
    }
    #endregion

    #region Присоединение к лобби
    public void JoinLobby(CSteamID lobbyID)
    {
        string lobbyPassword = SteamMatchmaking.GetLobbyData(lobbyID, "LobbyPassword");

        if (string.IsNullOrEmpty(lobbyPassword))
        {
            SteamMatchmaking.JoinLobby(lobbyID);
        }
        else
        {
            string enteredPassword = joinPasswordInput.text;
            if (enteredPassword == lobbyPassword)
            {
                SteamMatchmaking.JoinLobby(lobbyID);
            }
            else
            {
                Debug.LogError("Неверный пароль!");
            }
        }
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (callback.m_EChatRoomEnterResponse != 1)
        {
            Debug.LogError("Не удалось войти в лобби");
            return;
        }

        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyID, "HostAddress");

        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
    }
    #endregion

    #region Приглашения
    public void InviteFriends()
    {
        if (currentLobbyID.IsValid())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyID);
        }
    }
    #endregion

    #region Выход
    public void LeaveLobby()
    {
        if (currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }

        if (NetworkServer.active || NetworkClient.active)
        {
            networkManager.StopHost();
        }
    }
    #endregion
}