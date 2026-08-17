using Mirror;
using UnityEngine;

public class DrinkMachineMiniGame : NetworkBehaviour, IMiniGame
{
    [SerializeField] private Survivor targetSurvivor;
    [SerializeField] private InterButton[] colorButtons;
    [SerializeField] private Color[] baseColors;
    [SerializeField] private GameObject cupPrefab;
    [SerializeField] private Transform cupSpawnPos;
    [SerializeField] private DrinkPlaceablePoint placeablePoint;
    private Color correctColor;
    private Color selectedColor1;
    private Color selectedColor2;
    private Color selectedColor3;
    private int selectionCount = 0;
    private bool isCompleted = false;

    public string QuestId { get; private set; }
    public bool IsCompleted => isCompleted;

    void Start()
    {
        if (!isServer) return;

        GenerateRandomCombination();
        placeablePoint.SetCorrectColor(correctColor);
        if (targetSurvivor != null)
        {
            targetSurvivor.SetIndicatorColor(correctColor);
        }

        for (int i = 0; i < colorButtons.Length; i++)
        {
            int index = i;
            colorButtons[i].onPress.AddListener(() => SelectColor(baseColors[index]));
        }
    }

    private void GenerateRandomCombination()
    {
        Color c1 = baseColors[Random.Range(0, baseColors.Length)];
        Color c2 = baseColors[Random.Range(0, baseColors.Length)];
        Color c3 = baseColors[Random.Range(0, baseColors.Length)];

        correctColor = MixColors(c1, c2, c3);
    }

    private Color MixColors(Color a, Color b, Color c)
    {
        return new Color(
            Mathf.Clamp01((a.r + b.r + c.r) / 3f),
            Mathf.Clamp01((a.g + b.g + c.g) / 3f),
            Mathf.Clamp01((a.b + b.b + c.b) / 3f)
        );
    }

    private bool ColorsMatch(Color a, Color b)
    {
        float threshold = 0.1f;
        return Mathf.Abs(a.r - b.r) < threshold &&
               Mathf.Abs(a.g - b.g) < threshold &&
               Mathf.Abs(a.b - b.b) < threshold;
    }

    private void SelectColor(Color color)
    {
        if (isCompleted) return;
        if (selectionCount >= 3) return;

        if (selectionCount == 0)
        {
            selectedColor1 = color;
            selectionCount = 1;
        }
        else if (selectionCount == 1)
        {
            selectedColor2 = color;
            selectionCount = 2;
        }
        else if (selectionCount == 2)
        {
            selectedColor3 = color;
            selectionCount = 3;
            CheckCombination();
        }
    }

    private void CheckCombination()
    {
        Color mixedColor = MixColors(selectedColor1, selectedColor2, selectedColor3);
        SpawnCup(mixedColor);
        selectionCount = 0;
    }

    private void SpawnCup(Color color)
    {
        if (cupPrefab == null) return;

        GameObject cup = Instantiate(cupPrefab, cupSpawnPos.position, Quaternion.identity);
        CupItem cupItem = cup.GetComponent<CupItem>();
        if (cupItem != null)
        {
            cupItem.SetColor(color);
        }
        var renderers = cup.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.material.color = color;
        }
        NetworkServer.Spawn(cup);
    }

    public void CompleteGame(bool success)
    {
        isCompleted = true;
        Debug.Log("POBEDA");
        if (targetSurvivor != null)
        {
            targetSurvivor.CompleteMiniGame(success);
        }
    }
    public bool CheckColor(Color color)
    {
        return ColorsMatch(color, correctColor);
    }
    public void Initialize(string questId, Survivor survivor)
    {
        QuestId = questId;
        targetSurvivor = survivor;
    }

    public void StartGame(Player player) { }
}
