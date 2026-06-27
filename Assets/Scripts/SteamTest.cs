using UnityEngine;
using Steamworks;

public class SteamTest : MonoBehaviour
{
    void Start()
    {
        if (SteamAPI.Init())
        {
            Debug.Log("Steam API инициализирована успешно!");
        }
        else
        {
            Debug.LogError("Steam API НЕ инициализирована! Проверьте, что Steam запущен и есть файл steam_appid.txt.");
        }
    }

    void OnApplicationQuit()
    {
        SteamAPI.Shutdown();
    }
}