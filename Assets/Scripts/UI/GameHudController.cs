using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameHudController : MonoBehaviour
{
    private const float AlertDuration = 3f;

    private GameManager gameManager;
    private LeakRepairController repairController;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI leakCountText;
    private TextMeshProUGUI alertText;
    private Slider repairSlider;
    private Coroutine alertCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GameHudController>() != null) return;
        if (FindFirstObjectByType<GameManager>() == null) return;

        new GameObject("__GameHUD").AddComponent<GameHudController>();
    }

    private void Awake()
    {
        BuildUi();
        BindGameManager();
    }

    private void Update()
    {
        if (gameManager == null) BindGameManager();
        if (repairController == null) FindRepairController();

        float timeLeft = gameManager != null ? gameManager.GetTimeLeft() : 0f;
        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        int leakCount = gameManager != null
            ? gameManager.GetActiveLeakCount()
            : 0;
        leakCountText.text = $"Утечки: {leakCount}";

        LeakHazard target = repairController != null
            ? repairController.CurrentTarget
            : null;
        bool showRepair = target != null && !target.IsRepaired;
        repairSlider.gameObject.SetActive(showRepair);
        if (showRepair) repairSlider.value = target.RepairProgress;
    }

    private void BindGameManager()
    {
        GameManager found = FindFirstObjectByType<GameManager>();
        if (found == gameManager) return;

        if (gameManager != null)
            gameManager.OnLeakSpawned -= ShowLeakAlert;

        gameManager = found;
        if (gameManager != null)
            gameManager.OnLeakSpawned += ShowLeakAlert;
    }

    private void FindRepairController()
    {
        repairController = FindObjectsByType<LeakRepairController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(controller =>
                controller.isLocalPlayer ||
                (!Mirror.NetworkClient.active && !Mirror.NetworkServer.active));
    }

    private void ShowLeakAlert()
    {
        if (alertCoroutine != null) StopCoroutine(alertCoroutine);
        alertCoroutine = StartCoroutine(ShowAlertForDuration());
    }

    private IEnumerator ShowAlertForDuration()
    {
        alertText.gameObject.SetActive(true);
        alertText.text = "ОБНАРУЖЕНА УТЕЧКА";
        yield return new WaitForSecondsRealtime(AlertDuration);
        alertText.gameObject.SetActive(false);
        alertCoroutine = null;
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        timerText = CreateText(
            "Timer",
            canvas.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -35f),
            new Vector2(320f, 60f),
            42,
            TextAlignmentOptions.Center);

        leakCountText = CreateText(
            "LeakCount",
            canvas.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -85f),
            new Vector2(320f, 45f),
            28,
            TextAlignmentOptions.Center);

        alertText = CreateText(
            "LeakAlert",
            canvas.transform,
            new Vector2(0.5f, 0.78f),
            Vector2.zero,
            new Vector2(700f, 80f),
            44,
            TextAlignmentOptions.Center);
        alertText.color = new Color(1f, 0.25f, 0.1f);
        alertText.gameObject.SetActive(false);

        repairSlider = CreateRepairSlider(canvas.transform);
        repairSlider.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Slider CreateRepairSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject(
            "RepairProgress",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)sliderObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.35f);
        rect.anchorMax = new Vector2(0.5f, 0.35f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(420f, 28f);

        Image background = sliderObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        fillObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        Image fill = fillObject.GetComponent<Image>();
        fill.color = new Color(0.15f, 0.9f, 0.35f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
        slider.fillRect = fillRect;
        return slider;
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnLeakSpawned -= ShowLeakAlert;
    }
}
