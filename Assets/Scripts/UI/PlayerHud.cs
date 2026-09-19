using Mirror;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI survivorsText;
    [SerializeField] private TextMeshProUGUI itemAmmoText;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject ePrompt;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image[] leakIcons;

    private GameManager gameManager;
    private Player localPlayer;
    private Interactor localInteractor;

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnActiveLeaksUpdated += UpdateLeakIcons;
            UpdateLeakIcons(gameManager.GetActiveLeakCount());
        }

        bool standalone = !NetworkClient.active && !NetworkServer.active;
        localPlayer = FindObjectsByType<Player>(FindObjectsSortMode.None)
            .FirstOrDefault(player => standalone || player.isLocalPlayer);
        if (localPlayer != null)
        {
            localInteractor = localPlayer.GetComponent<Interactor>();
            localPlayer.OnHealthChanged += UpdateHealth;
            UpdateHealth(localPlayer.Health, localPlayer.MaxHealth);
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnActiveLeaksUpdated -= UpdateLeakIcons;

        if (localPlayer != null)
            localPlayer.OnHealthChanged -= UpdateHealth;
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

    private void UpdateHealth(int health, int maxHealth)
    {
        if (healthFill == null) return;

        healthFill.fillAmount = maxHealth > 0
            ? (float)health / maxHealth
            : 0f;
    }

    private void UpdateLeakIcons(int activeLeaks)
    {
        if (leakIcons == null) return;

        for (int i = 0; i < leakIcons.Length; i++)
        {
            if (leakIcons[i] != null)
                leakIcons[i].gameObject.SetActive(i < activeLeaks);
        }
    }

    private ItemObject GetSelectedItem()
    {
        if (localPlayer == null) return null;
        return localPlayer.GetSelectedIPickable() as ItemObject;
    }
}