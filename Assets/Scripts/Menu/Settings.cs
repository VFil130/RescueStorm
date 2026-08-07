using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

public class SettingsManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle chromaticToggle;
    public Toggle bloomToggle;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider ambientVolumeSlider;
    public Slider mouseSensitivitySlider;

    // WASD
    public Button forwardKeyButton;
    public Button backwardKeyButton;
    public Button leftKeyButton;
    public Button rightKeyButton;
    public TextMeshProUGUI forwardKeyText;
    public TextMeshProUGUI backwardKeyText;
    public TextMeshProUGUI leftKeyText;
    public TextMeshProUGUI rightKeyText;

    // Дополнительные клавиши
    public Button eKeyButton;
    public Button spaceKeyButton;
    public Button leftCtrlKeyButton;
    public Button leftShiftKeyButton;
    public Button key1Button;
    public Button key2Button;
    public Button key3Button;
    public Button key4Button;
    public Button key5Button;

    public TextMeshProUGUI eKeyText;
    public TextMeshProUGUI spaceKeyText;
    public TextMeshProUGUI leftCtrlKeyText;
    public TextMeshProUGUI leftShiftKeyText;
    public TextMeshProUGUI key1Text;
    public TextMeshProUGUI key2Text;
    public TextMeshProUGUI key3Text;
    public TextMeshProUGUI key4Text;
    public TextMeshProUGUI key5Text;

    [Header("Volume Profile")]
    public VolumeProfile volumeProfile;

    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject graphicsPanel;
    public GameObject audioPanel;
    public GameObject controlsPanel;
    public GameObject exitConfirmationPanel;

    // Новые панели для мультиплеера
    public GameObject multiplayerPanel;
    public GameObject joinPanel;
    public GameObject createPanel;

    [Header("Multiplayer UI")]
    public TMP_InputField joinInputField;
    public TMP_InputField createInputField;

    // Текущие назначения клавиш
    private KeyCode forwardKey = KeyCode.W;
    private KeyCode backwardKey = KeyCode.S;
    private KeyCode leftKey = KeyCode.A;
    private KeyCode rightKey = KeyCode.D;
    private KeyCode eKey = KeyCode.E;
    private KeyCode spaceKey = KeyCode.Space;
    private KeyCode leftCtrlKey = KeyCode.LeftControl;
    private KeyCode leftShiftKey = KeyCode.LeftShift;
    private KeyCode key1 = KeyCode.Alpha1;
    private KeyCode key2 = KeyCode.Alpha2;
    private KeyCode key3 = KeyCode.Alpha3;
    private KeyCode key4 = KeyCode.Alpha4;
    private KeyCode key5 = KeyCode.Alpha5;

    private bool isWaitingForKey = false;
    private string waitingForAction = "";
    private bool isLoading = false;

    private Resolution[] resolutions;
    private List<string> resolutionOptions = new List<string>();

    void Start()
    {
        // Проверка привязок
        if (resolutionDropdown == null) Debug.LogError("resolutionDropdown не привязан!");
        if (chromaticToggle == null) Debug.LogError("chromaticToggle не привязан!");
        if (bloomToggle == null) Debug.LogError("bloomToggle не привязан!");
        if (volumeProfile == null) Debug.LogWarning("volumeProfile не привязан! Пост-эффекты не будут работать.");
        if (mouseSensitivitySlider == null) Debug.LogWarning("mouseSensitivitySlider не привязан!");

        // Заполняем разрешения
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        int currentResIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + "x" + resolutions[i].height;
            resolutionOptions.Add(option);
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
                currentResIndex = i;
        }
        resolutionDropdown.AddOptions(resolutionOptions);
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();

        // Подписки на события UI
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        chromaticToggle.onValueChanged.AddListener(OnChromaticToggleChanged);
        bloomToggle.onValueChanged.AddListener(OnBloomToggleChanged);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        ambientVolumeSlider.onValueChanged.AddListener(OnAmbientVolumeChanged);
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);

        // Подписки на кнопки WASD
        forwardKeyButton.onClick.AddListener(() => StartKeyRebind("forward"));
        backwardKeyButton.onClick.AddListener(() => StartKeyRebind("backward"));
        leftKeyButton.onClick.AddListener(() => StartKeyRebind("left"));
        rightKeyButton.onClick.AddListener(() => StartKeyRebind("right"));

        // Подписки на дополнительные кнопки
        eKeyButton.onClick.AddListener(() => StartKeyRebind("e"));
        spaceKeyButton.onClick.AddListener(() => StartKeyRebind("space"));
        leftCtrlKeyButton.onClick.AddListener(() => StartKeyRebind("leftctrl"));
        leftShiftKeyButton.onClick.AddListener(() => StartKeyRebind("leftshift"));
        key1Button.onClick.AddListener(() => StartKeyRebind("key1"));
        key2Button.onClick.AddListener(() => StartKeyRebind("key2"));
        key3Button.onClick.AddListener(() => StartKeyRebind("key3"));
        key4Button.onClick.AddListener(() => StartKeyRebind("key4"));
        key5Button.onClick.AddListener(() => StartKeyRebind("key5"));

        // Загружаем сохранения
        LoadSettings();
        UpdateAllKeyTexts();

        // По умолчанию показываем главное меню
        CloseAllPanels();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        Canvas.ForceUpdateCanvases();
        Debug.Log("SettingsManager инициализирован");
    }

    void Update()
    {
        if (isWaitingForKey)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            foreach (var key in keyboard.allKeys)
            {
                if (key.wasPressedThisFrame)
                {
                    if (key.keyCode == Key.Escape)
                    {
                        Debug.Log("Нажата Escape — отмена переназначения");
                        isWaitingForKey = false;
                        waitingForAction = "";
                        return;
                    }

                    KeyCode newKey = ConvertInputSystemKeyToKeyCode(key.keyCode);
                    Debug.Log($"Обнаружена клавиша: {key.keyCode} -> KeyCode: {newKey} для действия {waitingForAction}");

                    switch (waitingForAction)
                    {
                        case "forward": forwardKey = newKey; break;
                        case "backward": backwardKey = newKey; break;
                        case "left": leftKey = newKey; break;
                        case "right": rightKey = newKey; break;
                        case "e": eKey = newKey; break;
                        case "space": spaceKey = newKey; break;
                        case "leftctrl": leftCtrlKey = newKey; break;
                        case "leftshift": leftShiftKey = newKey; break;
                        case "key1": key1 = newKey; break;
                        case "key2": key2 = newKey; break;
                        case "key3": key3 = newKey; break;
                        case "key4": key4 = newKey; break;
                        case "key5": key5 = newKey; break;
                        default:
                            Debug.LogError($"Неизвестное действие: {waitingForAction}");
                            isWaitingForKey = false;
                            waitingForAction = "";
                            return;
                    }

                    isWaitingForKey = false;
                    waitingForAction = "";
                    SaveAllSettings();
                    UpdateAllKeyTexts();
                    Canvas.ForceUpdateCanvases();
                    Debug.Log($"Переназначение завершено. Новая клавиша: {newKey}");
                    break;
                }
            }
        }
    }

    private KeyCode ConvertInputSystemKeyToKeyCode(Key key)
    {
        switch (key)
        {
            case Key.A: return KeyCode.A;
            case Key.B: return KeyCode.B;
            case Key.C: return KeyCode.C;
            case Key.D: return KeyCode.D;
            case Key.E: return KeyCode.E;
            case Key.F: return KeyCode.F;
            case Key.G: return KeyCode.G;
            case Key.H: return KeyCode.H;
            case Key.I: return KeyCode.I;
            case Key.J: return KeyCode.J;
            case Key.K: return KeyCode.K;
            case Key.L: return KeyCode.L;
            case Key.M: return KeyCode.M;
            case Key.N: return KeyCode.N;
            case Key.O: return KeyCode.O;
            case Key.P: return KeyCode.P;
            case Key.Q: return KeyCode.Q;
            case Key.R: return KeyCode.R;
            case Key.S: return KeyCode.S;
            case Key.T: return KeyCode.T;
            case Key.U: return KeyCode.U;
            case Key.V: return KeyCode.V;
            case Key.W: return KeyCode.W;
            case Key.X: return KeyCode.X;
            case Key.Y: return KeyCode.Y;
            case Key.Z: return KeyCode.Z;
            case Key.Digit0: return KeyCode.Alpha0;
            case Key.Digit1: return KeyCode.Alpha1;
            case Key.Digit2: return KeyCode.Alpha2;
            case Key.Digit3: return KeyCode.Alpha3;
            case Key.Digit4: return KeyCode.Alpha4;
            case Key.Digit5: return KeyCode.Alpha5;
            case Key.Digit6: return KeyCode.Alpha6;
            case Key.Digit7: return KeyCode.Alpha7;
            case Key.Digit8: return KeyCode.Alpha8;
            case Key.Digit9: return KeyCode.Alpha9;
            case Key.Space: return KeyCode.Space;
            case Key.Enter: return KeyCode.Return;
            case Key.Tab: return KeyCode.Tab;
            case Key.Backspace: return KeyCode.Backspace;
            case Key.LeftShift: return KeyCode.LeftShift;
            case Key.RightShift: return KeyCode.RightShift;
            case Key.LeftCtrl: return KeyCode.LeftControl;
            case Key.RightCtrl: return KeyCode.RightControl;
            case Key.LeftAlt: return KeyCode.LeftAlt;
            case Key.RightAlt: return KeyCode.RightAlt;
            case Key.Escape: return KeyCode.Escape;
            case Key.UpArrow: return KeyCode.UpArrow;
            case Key.DownArrow: return KeyCode.DownArrow;
            case Key.LeftArrow: return KeyCode.LeftArrow;
            case Key.RightArrow: return KeyCode.RightArrow;
            default:
                Debug.LogWarning($"Неизвестная клавиша: {key}, возвращаем None");
                return KeyCode.None;
        }
    }

    void LoadSettings()
    {
        isLoading = true;

        // Разрешение
        int resIndex = PlayerPrefs.GetInt("ResolutionIndex", resolutionDropdown.value);
        resolutionDropdown.value = resIndex;
        ApplyResolution(resIndex);

        // Пост-эффекты
        chromaticToggle.isOn = PlayerPrefs.GetInt("ChromaticAberrationEnabled", 1) == 1;
        bloomToggle.isOn = PlayerPrefs.GetInt("BloomEnabled", 1) == 1;
        ApplyPostProcessing();

        // Аудио
        masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        ambientVolumeSlider.value = PlayerPrefs.GetFloat("AmbientVolume", 0.8f);

        // Чувствительность мыши
        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = sensitivity;

        // Клавиши
        forwardKey = LoadKey("ForwardKey", KeyCode.W);
        backwardKey = LoadKey("BackwardKey", KeyCode.S);
        leftKey = LoadKey("LeftKey", KeyCode.A);
        rightKey = LoadKey("RightKey", KeyCode.D);
        eKey = LoadKey("EKey", KeyCode.E);
        spaceKey = LoadKey("SpaceKey", KeyCode.Space);
        leftCtrlKey = LoadKey("LeftCtrlKey", KeyCode.LeftControl);
        leftShiftKey = LoadKey("LeftShiftKey", KeyCode.LeftShift);
        key1 = LoadKey("Key1", KeyCode.Alpha1);
        key2 = LoadKey("Key2", KeyCode.Alpha2);
        key3 = LoadKey("Key3", KeyCode.Alpha3);
        key4 = LoadKey("Key4", KeyCode.Alpha4);
        key5 = LoadKey("Key5", KeyCode.Alpha5);

        isLoading = false;

        Debug.Log("=== НАСТРОЙКИ ЗАГРУЖЕНЫ ===");
        Debug.Log($"Разрешение: {resolutionOptions[resIndex]}");
        Debug.Log($"Chromatic: {chromaticToggle.isOn}, Bloom: {bloomToggle.isOn}");
        Debug.Log($"Громкость: Master={masterVolumeSlider.value}, Music={musicVolumeSlider.value}, SFX={sfxVolumeSlider.value}, Ambient={ambientVolumeSlider.value}");
        Debug.Log($"Чувствительность мыши: {sensitivity}");
        Debug.Log($"Клавиши: Forward={forwardKey}, Backward={backwardKey}, Left={leftKey}, Right={rightKey}, E={eKey}, Space={spaceKey}, LCTRL={leftCtrlKey}, LSHIFT={leftShiftKey}, 1={key1}, 2={key2}, 3={key3}, 4={key4}, 5={key5}");
    }

    private KeyCode LoadKey(string key, KeyCode defaultKey)
    {
        string str = PlayerPrefs.GetString(key, defaultKey.ToString());
        try
        {
            return (KeyCode)Enum.Parse(typeof(KeyCode), str);
        }
        catch
        {
            Debug.LogWarning($"Не удалось загрузить {key}, используется значение по умолчанию {defaultKey}");
            return defaultKey;
        }
    }

    void SaveAllSettings()
    {
        if (isLoading) return;

        PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        PlayerPrefs.SetInt("ChromaticAberrationEnabled", chromaticToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("BloomEnabled", bloomToggle.isOn ? 1 : 0);
        PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);
        PlayerPrefs.SetFloat("AmbientVolume", ambientVolumeSlider.value);
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivitySlider != null ? mouseSensitivitySlider.value : 1f);
        SaveKeyBindings();
        PlayerPrefs.Save();

        Debug.Log("=== НАСТРОЙКИ СОХРАНЕНЫ ===");
        Debug.Log($"Разрешение: {resolutionOptions[resolutionDropdown.value]}");
        Debug.Log($"Chromatic: {chromaticToggle.isOn}, Bloom: {bloomToggle.isOn}");
        Debug.Log($"Громкость: Master={masterVolumeSlider.value}, Music={musicVolumeSlider.value}, SFX={sfxVolumeSlider.value}, Ambient={ambientVolumeSlider.value}");
        Debug.Log($"Чувствительность мыши: {mouseSensitivitySlider.value}");
        Debug.Log($"Клавиши: Forward={forwardKey}, Backward={backwardKey}, Left={leftKey}, Right={rightKey}, E={eKey}, Space={spaceKey}, LCTRL={leftCtrlKey}, LSHIFT={leftShiftKey}, 1={key1}, 2={key2}, 3={key3}, 4={key4}, 5={key5}");
    }

    void SaveKeyBindings()
    {
        SaveKey("ForwardKey", forwardKey);
        SaveKey("BackwardKey", backwardKey);
        SaveKey("LeftKey", leftKey);
        SaveKey("RightKey", rightKey);
        SaveKey("EKey", eKey);
        SaveKey("SpaceKey", spaceKey);
        SaveKey("LeftCtrlKey", leftCtrlKey);
        SaveKey("LeftShiftKey", leftShiftKey);
        SaveKey("Key1", key1);
        SaveKey("Key2", key2);
        SaveKey("Key3", key3);
        SaveKey("Key4", key4);
        SaveKey("Key5", key5);
        Debug.Log("Сохранены все клавиши");
    }

    private void SaveKey(string key, KeyCode value)
    {
        PlayerPrefs.SetString(key, value.ToString());
    }

    void ApplyResolution(int index)
    {
        if (resolutions == null || resolutions.Length == 0) return;
        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }

    void ApplyPostProcessing()
    {
        if (volumeProfile == null) return;

        if (volumeProfile.TryGet<ChromaticAberration>(out var chromatic))
            chromatic.active = chromaticToggle.isOn;

        if (volumeProfile.TryGet<Bloom>(out var bloom))
            bloom.active = bloomToggle.isOn;
    }

    // ---- Обработчики UI ----
    void OnResolutionChanged(int index) { if (!isLoading) { ApplyResolution(index); SaveAllSettings(); } }
    void OnChromaticToggleChanged(bool isOn) { if (!isLoading) { ApplyPostProcessing(); SaveAllSettings(); } }
    void OnBloomToggleChanged(bool isOn) { if (!isLoading) { ApplyPostProcessing(); SaveAllSettings(); } }
    void OnMasterVolumeChanged(float val) { if (!isLoading) SaveAllSettings(); }
    void OnMusicVolumeChanged(float val) { if (!isLoading) SaveAllSettings(); }
    void OnSFXVolumeChanged(float val) { if (!isLoading) SaveAllSettings(); }
    void OnAmbientVolumeChanged(float val) { if (!isLoading) SaveAllSettings(); }
    void OnMouseSensitivityChanged(float val) { if (!isLoading) SaveAllSettings(); }

    // ---- Переназначение клавиш ----
    void StartKeyRebind(string action)
    {
        Debug.Log($"Запуск переназначения для {action}");
        isWaitingForKey = true;
        waitingForAction = action;
    }

    // ---- Отображение понятных имён клавиш ----
    private string GetKeyDisplayName(KeyCode key)
    {
        if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha5)
            return (key - KeyCode.Alpha1 + 1).ToString();

        switch (key)
        {
            case KeyCode.LeftControl: return "L CTRL";
            case KeyCode.RightControl: return "R CTRL";
            case KeyCode.LeftShift: return "L SHIFT";
            case KeyCode.RightShift: return "R SHIFT";
            case KeyCode.LeftAlt: return "L ALT";
            case KeyCode.RightAlt: return "R ALT";
            case KeyCode.Space: return "Space";
            case KeyCode.Escape: return "Esc";
            case KeyCode.Return: return "Enter";
            case KeyCode.Tab: return "Tab";
            case KeyCode.Backspace: return "Backspace";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            default: return key.ToString();
        }
    }

    void UpdateAllKeyTexts()
    {
        if (forwardKeyText) forwardKeyText.text = GetKeyDisplayName(forwardKey);
        if (backwardKeyText) backwardKeyText.text = GetKeyDisplayName(backwardKey);
        if (leftKeyText) leftKeyText.text = GetKeyDisplayName(leftKey);
        if (rightKeyText) rightKeyText.text = GetKeyDisplayName(rightKey);
        if (eKeyText) eKeyText.text = GetKeyDisplayName(eKey);
        if (spaceKeyText) spaceKeyText.text = GetKeyDisplayName(spaceKey);
        if (leftCtrlKeyText) leftCtrlKeyText.text = GetKeyDisplayName(leftCtrlKey);
        if (leftShiftKeyText) leftShiftKeyText.text = GetKeyDisplayName(leftShiftKey);
        if (key1Text) key1Text.text = GetKeyDisplayName(key1);
        if (key2Text) key2Text.text = GetKeyDisplayName(key2);
        if (key3Text) key3Text.text = GetKeyDisplayName(key3);
        if (key4Text) key4Text.text = GetKeyDisplayName(key4);
        if (key5Text) key5Text.text = GetKeyDisplayName(key5);
        Debug.Log("Все тексты клавиш обновлены");
    }

    // ---- Сброс до исходных ----
    public void ResetToDefaults()
    {
        forwardKey = KeyCode.W;
        backwardKey = KeyCode.S;
        leftKey = KeyCode.A;
        rightKey = KeyCode.D;
        eKey = KeyCode.E;
        spaceKey = KeyCode.Space;
        leftCtrlKey = KeyCode.LeftControl;
        leftShiftKey = KeyCode.LeftShift;
        key1 = KeyCode.Alpha1;
        key2 = KeyCode.Alpha2;
        key3 = KeyCode.Alpha3;
        key4 = KeyCode.Alpha4;
        key5 = KeyCode.Alpha5;

        UpdateAllKeyTexts();
        SaveAllSettings();
        Debug.Log("Все клавиши сброшены до значений по умолчанию");
    }

    // ---- Геттеры ----
    public KeyCode GetForwardKey() => forwardKey;
    public KeyCode GetBackwardKey() => backwardKey;
    public KeyCode GetLeftKey() => leftKey;
    public KeyCode GetRightKey() => rightKey;
    public KeyCode GetEKey() => eKey;
    public KeyCode GetSpaceKey() => spaceKey;
    public KeyCode GetLeftCtrlKey() => leftCtrlKey;
    public KeyCode GetLeftShiftKey() => leftShiftKey;
    public KeyCode GetKey1() => key1;
    public KeyCode GetKey2() => key2;
    public KeyCode GetKey3() => key3;
    public KeyCode GetKey4() => key4;
    public KeyCode GetKey5() => key5;
    public float GetMouseSensitivity() => mouseSensitivitySlider != null ? mouseSensitivitySlider.value : 1f;

    // ---- Управление панелями ----

    private void CloseAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (graphicsPanel != null) graphicsPanel.SetActive(false);
        if (audioPanel != null) audioPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);
        if (createPanel != null) createPanel.SetActive(false);
    }

    public void OpenMainMenu()
    {
        CloseAllPanels();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        Debug.Log("Открыто главное меню");
    }

    public void OpenSettings()
    {
        CloseAllPanels();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        Debug.Log("Открыты настройки");
    }

    public void OpenGraphics()
    {
        CloseAllPanels();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (graphicsPanel != null) graphicsPanel.SetActive(true);
        Debug.Log("Открыта графика");
    }

    public void OpenAudio()
    {
        CloseAllPanels();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (audioPanel != null) audioPanel.SetActive(true);
        Debug.Log("Открыто аудио");
    }

    public void OpenControls()
    {
        CloseAllPanels();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (controlsPanel != null) controlsPanel.SetActive(true);
        Debug.Log("Открыто управление");
    }

    public void OpenExitConfirmation()
    {
        CloseAllPanels();
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(true);
        Debug.Log("Открыто подтверждение выхода");
    }

    public void BackToMainMenu()
    {
        OpenMainMenu();
    }

    public void CloseExitConfirmation()
    {
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
        OpenMainMenu();
        Debug.Log("Выход отменён");
    }

    public void ExitGame()
    {
        Debug.Log("Выход из игры");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- Мультиплеер ----
    public void OpenMultiplayer()
    {
        CloseAllPanels();
        if (multiplayerPanel != null) multiplayerPanel.SetActive(true);
        Debug.Log("Открыта панель мультиплеера");
    }

    public void OpenJoinPanel()
    {
        CloseAllPanels();
        // Показываем панель Join, но можно оставить фон (multiplayerPanel) или нет - решать вам
        // Можно показать только joinPanel, но для удобства можно также показать multiplayerPanel как фон.
        // Я покажу только joinPanel, а возврат через BackFromJoin вернёт в multiplayerPanel.
        if (multiplayerPanel != null) multiplayerPanel.SetActive(true); // опционально
        if (joinPanel != null) joinPanel.SetActive(true);
        Debug.Log("Открыта панель Join");
    }

    public void OpenCreatePanel()
    {
        CloseAllPanels();
        if (multiplayerPanel != null) multiplayerPanel.SetActive(true);
        if (createPanel != null) createPanel.SetActive(true);
        Debug.Log("Открыта панель Create");
    }

    public void BackFromMultiplayer()
    {
        OpenMainMenu();
    }

    public void BackFromJoin()
    {
        // Закрываем joinPanel и показываем multiplayerPanel
        if (joinPanel != null) joinPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(true);
        Debug.Log("Возврат в мультиплеер из Join");
    }

    public void BackFromCreate()
    {
        if (createPanel != null) createPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(true);
        Debug.Log("Возврат в мультиплеер из Create");
    }

    // ---- Заглушки для кнопок мультиплеера ----
    public void OnJoinButton()
    {
        string ip = joinInputField != null ? joinInputField.text : "";
        Debug.Log($"Присоединение к серверу с IP: {ip} (заглушка)");
        // Здесь будет логика подключения
    }

    public void OnCreateButton()
    {
        string roomName = createInputField != null ? createInputField.text : "";
        Debug.Log($"Создание комнаты с именем: {roomName} (заглушка)");
        // Здесь будет логика создания
    }

    // ---- Заглушки для главного меню ----
    public void OnSinglePlayer()
    {
        Debug.Log("Запуск одиночной игры (заглушка)");
        // Здесь будет загрузка одиночной игры
    }

    // Метод OnMultiPlayer теперь вызывает OpenMultiplayer
    public void OnMultiPlayer()
    {
        OpenMultiplayer();
    }

    void OnApplicationQuit()
    {
        SaveAllSettings();
        Debug.Log("Настройки сохранены при выходе");
    }
}