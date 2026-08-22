using Steamworks;

public class LobbyManager : SteamLobbyController
{
    protected override string CreateHostAddress()
    {
        return SteamUser.GetSteamID().ToString();
    }

    protected override string CreateLobbyName()
    {
        return $"{SteamFriends.GetPersonaName()}_{UnityEngine.Random.Range(10000, 99999)}";
    }
}
