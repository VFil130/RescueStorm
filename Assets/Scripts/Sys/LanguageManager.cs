using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Zenject;

public class LanguageManager
{
    public event System.Action OnLanguageChanged;

    private Dictionary<string, string> translations = new Dictionary<string, string>();
    private string currentLanguage = "English";

    public LanguageManager()
    {
        LoadLanguage(currentLanguage);
    }
    public void LoadLanguage(string language)
    {
        currentLanguage = language;
        translations.Clear();

        TextAsset xmlFile = Resources.Load<TextAsset>($"Languages/{language}");
        if (xmlFile == null)
        {
            Debug.LogError($"Language file not found: Languages/{language}");
            return;
        }

        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlFile.text);

        XmlNodeList nodes = xmlDoc.SelectNodes("//text");
        foreach (XmlNode node in nodes)
        {
            string key = node.Attributes["key"]?.Value;
            string value = node.InnerText;
            if (!string.IsNullOrEmpty(key))
            {
                translations[key] = value;
            }
        }

        OnLanguageChanged?.Invoke();
    }

    public string GetText(string key)
    {
        if (translations.TryGetValue(key, out string value))
        {
            return value;
        }
        Debug.LogWarning($"Translation not found for key: {key}");
        return $"[{key}]";
    }

    public string GetCurrentLanguage() => currentLanguage;
}
