using Mirror;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class PlayerHud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI survivorsText;
    [SerializeField] private TextMeshProUGUI itemAmmoText;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject ePrompt;

    private GameManager gameManager;
    private Player localPlayer;
    private Interactor localInteractor;

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        bool standalone = !NetworkClient.active && !NetworkServer.active;
        localPlayer = FindObjectsByType<Player>(FindObjectsSortMode.None)
            .FirstOrDefault(player => standalone || player.isLocalPlayer);
        if (localPlayer != null)
            localInteractor = localPlayer.GetComponent<Interactor>();
    }

    private void Update()
    {
        UpdateTimer();
        UpdateSurvivors();
        UpdateItemAmmo();
        UpdateEPrompt();
    }

    public void TogglePausePanel()
    {
        if (pausePanel != null)
            pausePanel.SetActive(!pausePanel.activeSelf);
    }

    private void UpdateEPrompt()
    {
        if (ePrompt == null || localInteractor == null)
        {
            if (ePrompt != null) ePrompt.SetActive(false);
            return;
        }
        ePrompt.SetActive(localInteractor.HasTarget());
    }

    private void UpdateTimer()
    {
        if (gameManager == null || timerText == null) return;

        float timeLeft = gameManager.GetTimeLeft();
        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void UpdateSurvivors()
    {
        if (gameManager == null || survivorsText == null) return;

        survivorsText.text = string.Format(
            "{0}/{1}",
            gameManager.GetSurvivorsSaved(),
            gameManager.GetTotalSurvivors());
    }

    private void UpdateItemAmmo()
    {
        if (itemAmmoText == null) return;

        ItemObject item = GetSelectedItem();
        if (item == null)
        {
            itemAmmoText.gameObject.SetActive(false);
            return;
        }

        itemAmmoText.gameObject.SetActive(true);
        itemAmmoText.text = item.charges.ToString();
    }

    private ItemObject GetSelectedItem()
    {
        if (localPlayer == null) return null;
        return localPlayer.GetSelectedIPickable() as ItemObject;
    }
}