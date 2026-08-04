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
    public Button forwardKeyButton;
    public Button backwardKeyButton;
    public Button leftKeyButton;
    public Button rightKeyButton;
    public TextMeshProUGUI forwardKeyText;
    public TextMeshProUGUI backwardKeyText;
    public TextMeshProUGUI leftKeyText;
    public TextMeshProUGUI rightKeyText;

    [Header("Volume Profile")]
    public Volume volumeProfile;

    // Текущие назначения клавиш
    private KeyCode forwardKey = KeyCode.W;
    private KeyCode backwardKey = KeyCode.S;
    private KeyCode leftKey = KeyCode.A;
    private KeyCode rightKey = KeyCode.D;

    private bool isWaitingForKey = false;
    private string waitingForAction = "";
    private bool isLoading = false; // Флаг, чтобы не сохранять во время загрузки

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

        forwardKeyButton.onClick.AddListener(() => StartKeyRebind("forward"));
        backwardKeyButton.onClick.AddListener(() => StartKeyRebind("backward"));
        leftKeyButton.onClick.AddListener(() => StartKeyRebind("left"));
        rightKeyButton.onClick.AddListener(() => StartKeyRebind("right"));

        // Загружаем сохранения (во время загрузки сохранения не вызываются)
        LoadSettings();
        UpdateKeyButtonTexts();

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
                        default:
                            Debug.LogError($"Неизвестное действие: {waitingForAction}");
                            isWaitingForKey = false;
                            waitingForAction = "";
                            return;
                    }

                    isWaitingForKey = false;
                    waitingForAction = "";
                    SaveAllSettings();
                    UpdateKeyButtonTexts();
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
        isLoading = true; // Начинаем загрузку, сохранения не вызываем

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

        // Клавиши — загружаем ДО того, как закончится загрузка
        string forwardStr = PlayerPrefs.GetString("ForwardKey", KeyCode.W.ToString());
        string backwardStr = PlayerPrefs.GetString("BackwardKey", KeyCode.S.ToString());
        string leftStr = PlayerPrefs.GetString("LeftKey", KeyCode.A.ToString());
        string rightStr = PlayerPrefs.GetString("RightKey", KeyCode.D.ToString());

        try
        {
            forwardKey = (KeyCode)Enum.Parse(typeof(KeyCode), forwardStr);
            backwardKey = (KeyCode)Enum.Parse(typeof(KeyCode), backwardStr);
            leftKey = (KeyCode)Enum.Parse(typeof(KeyCode), leftStr);
            rightKey = (KeyCode)Enum.Parse(typeof(KeyCode), rightStr);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка загрузки клавиш: {e.Message}. Используются значения по умолчанию.");
            forwardKey = KeyCode.W;
            backwardKey = KeyCode.S;
            leftKey = KeyCode.A;
            rightKey = KeyCode.D;
        }

        isLoading = false; // Загрузка завершена

        Debug.Log("=== НАСТРОЙКИ ЗАГРУЖЕНЫ ===");
        Debug.Log($"Разрешение: {resolutionOptions[resIndex]}");
        Debug.Log($"Chromatic: {chromaticToggle.isOn}, Bloom: {bloomToggle.isOn}");
        Debug.Log($"Громкость: Master={masterVolumeSlider.value}, Music={musicVolumeSlider.value}, SFX={sfxVolumeSlider.value}, Ambient={ambientVolumeSlider.value}");
        Debug.Log($"Чувствительность мыши: {sensitivity}");
        Debug.Log($"Клавиши: Forward={forwardKey}, Backward={backwardKey}, Left={leftKey}, Right={rightKey}");
    }

    void SaveAllSettings()
    {
        if (isLoading) return; // Если идёт загрузка — не сохраняем

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
        Debug.Log($"Клавиши: Forward={forwardKey}, Backward={backwardKey}, Left={leftKey}, Right={rightKey}");
    }

    void SaveKeyBindings()
    {
        PlayerPrefs.SetString("ForwardKey", forwardKey.ToString());
        PlayerPrefs.SetString("BackwardKey", backwardKey.ToString());
        PlayerPrefs.SetString("LeftKey", leftKey.ToString());
        PlayerPrefs.SetString("RightKey", rightKey.ToString());
        Debug.Log($"Сохранены клавиши: Forward={forwardKey}, Backward={backwardKey}, Left={leftKey}, Right={rightKey}");
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
        VolumeProfile runtimeProfile = volumeProfile.profile;
        if (runtimeProfile == null) return;

        if (runtimeProfile.TryGet<ChromaticAberration>(out var chromatic))
            chromatic.active = chromaticToggle.isOn;

        if (runtimeProfile.TryGet<Bloom>(out var bloom))
            bloom.active = bloomToggle.isOn;
    }

    // ---- Обработчики UI (с проверкой isLoading) ----
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

    void UpdateKeyButtonTexts()
    {
        if (forwardKeyText) { forwardKeyText.text = forwardKey.ToString(); Debug.Log($"Обновлён текст Forward: {forwardKeyText.text}"); }
        if (backwardKeyText) { backwardKeyText.text = backwardKey.ToString(); Debug.Log($"Обновлён текст Backward: {backwardKeyText.text}"); }
        if (leftKeyText) { leftKeyText.text = leftKey.ToString(); Debug.Log($"Обновлён текст Left: {leftKeyText.text}"); }
        if (rightKeyText) { rightKeyText.text = rightKey.ToString(); Debug.Log($"Обновлён текст Right: {rightKeyText.text}"); }
    }

    // ---- Геттеры ----
    public KeyCode GetForwardKey() => forwardKey;
    public KeyCode GetBackwardKey() => backwardKey;
    public KeyCode GetLeftKey() => leftKey;
    public KeyCode GetRightKey() => rightKey;
    public float GetMouseSensitivity() => mouseSensitivitySlider != null ? mouseSensitivitySlider.value : 1f;

    void OnApplicationQuit()
    {
        SaveAllSettings();
        Debug.Log("Настройки сохранены при выходе");
    }
}