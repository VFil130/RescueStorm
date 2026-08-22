public class LobbyTelepaty : SteamLobbyController
{
    protected override string CreateHostAddress()
    {
        return "127.0.0.1";
    }

    protected override string CreateLobbyName()
    {
        return $"Игра {UnityEngine.Random.Range(100, 999)}";
    }
}
