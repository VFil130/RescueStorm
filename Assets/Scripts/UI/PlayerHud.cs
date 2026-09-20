using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHud : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Survivors")]
    [SerializeField] private TextMeshProUGUI survivorsText;

    [Header("Item")]
    [SerializeField] private TextMeshProUGUI itemAmmoText;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;

    [Header("Interact")]
    [SerializeField] private GameObject ePrompt;

    [Header("Health")]
    [SerializeField] private Image healthFill;

    [Header("Leaks")]
    [SerializeField] private Image[] leakIcons;

    [Header("Inventory Slots")]
    [SerializeField] private Image[] slotBackgrounds;
    [SerializeField] private Image[] slotIcons;
    [SerializeField] private Color slotSelectedColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color slotNormalColor = new Color(1f, 1f, 1f, 0.2f);

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

        localPlayer = FindFirstObjectByType<Player>();
        if (localPlayer != null)
        {
            localInteractor = localPlayer.GetComponent<Interactor>();
            localPlayer.OnHealthChanged += UpdateHealth;
            localPlayer.OnSelectedSlotChangedEvent += UpdateSelectedSlot;
            localPlayer.OnInventoryChanged += UpdateSlotIcons;
            UpdateSelectedSlot(localPlayer.SelectedSlot);
            UpdateSlotIcons(localPlayer.InventoryItems);
            UpdateHealth(localPlayer.Health, localPlayer.MaxHealth);
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnActiveLeaksUpdated -= UpdateLeakIcons;

        if (localPlayer != null)
        {
            localPlayer.OnHealthChanged -= UpdateHealth;
            localPlayer.OnSelectedSlotChangedEvent -= UpdateSelectedSlot;
            localPlayer.OnInventoryChanged -= UpdateSlotIcons;
        }
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

    private void UpdateSelectedSlot(int slotIndex)
    {
        if (slotBackgrounds == null) return;

        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] != null)
                slotBackgrounds[i].color = i == slotIndex
                    ? slotSelectedColor
                    : slotNormalColor;
        }
    }

    private void UpdateSlotIcons(InventorySlot[] slots)
    {
        if (slotIcons == null) return;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] == null) continue;

            ItemObject item = slots != null && i < slots.Length
                ? slots[i].itemObject as ItemObject
                : null;

            if (item != null && item.icon != null)
            {
                slotIcons[i].gameObject.SetActive(true);
                slotIcons[i].sprite = item.icon;
            }
            else
            {
                slotIcons[i].gameObject.SetActive(false);
            }
        }
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

    private void UpdateEPrompt()
    {
        if (ePrompt == null) return;

        ePrompt.SetActive(localInteractor != null && localInteractor.HasTarget());
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
