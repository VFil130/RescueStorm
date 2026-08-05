using TMPro;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(TMP_Text))]
public class TextWithKey : MonoBehaviour
{
    public string key;
    [Inject] private LanguageManager languageManager;
    private TMP_Text textComponent;

    void Start()
    {
        textComponent = GetComponent<TMP_Text>();
        languageManager.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    private void UpdateText()
    {
        textComponent.text = languageManager.GetText(key);
    }

    void OnDestroy()
    {
        if (languageManager != null)
        {
            languageManager.OnLanguageChanged -= UpdateText;
        }
    }
}