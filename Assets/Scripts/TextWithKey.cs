using TMPro;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(TMP_Text))]
public class TextWithKey : MonoBehaviour
{
    public string key;

    private TMP_Text textComponent;
    private LanguageManager languageManager;

    void Start()
    {
        textComponent = GetComponent<TMP_Text>();
        languageManager = ProjectContext.Instance.Container.Resolve<LanguageManager>();

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