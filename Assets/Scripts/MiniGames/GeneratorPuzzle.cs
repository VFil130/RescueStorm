using System.Collections.Generic;
using UnityEngine;

public class GeneratorPuzzle : MonoBehaviour, IMiniGame
{
    [SerializeField] private List<InterButton> buttons;
    [SerializeField] private int sequenceLength = 3;
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material pressedMaterial;

    private List<int> targetSequence = new List<int>();
    private int currentStep = 0;
    private bool isCompleted = false;
    private Survivor linkedSurvivor;

    public string QuestId { get; private set; }
    public bool IsCompleted => isCompleted;

    void Start()
    {
        GenerateRandomSequence(sequenceLength);
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i;
            buttons[i].onPress.AddListener(() => OnButtonPressed(index));
        }
        ResetPuzzle();
    }

    private void OnButtonPressed(int buttonId)
    {
        if (isCompleted) return;

        if (buttonId == targetSequence[currentStep])
        {
            SetButtonColor(buttonId, true);
            currentStep++;

            if (currentStep >= targetSequence.Count)
            {
                CompleteGame(true);
            }
        }
        else
        {
            ResetPuzzle();
        }
    }

    private void SetButtonColor(int index, bool pressed)
    {
        var renderer = buttons[index].GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material = pressed ? pressedMaterial : defaultMaterial;
        }
    }

    private void ResetPuzzle()
    {
        currentStep = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            SetButtonColor(i, false);
        }
    }

    private void GenerateRandomSequence(int length)
    {
        targetSequence.Clear();
        List<int> available = new List<int>();
        for (int i = 0; i < buttons.Count; i++) available.Add(i);

        for (int i = 0; i < length; i++)
        {
            int index = Random.Range(0, available.Count);
            targetSequence.Add(available[index]);
            available.RemoveAt(index);
        }
    }

    public void CompleteGame(bool success)
    {
        isCompleted = true;
        linkedSurvivor?.CompleteMiniGame(success);
    }

    public void Initialize(string questId, Survivor survivor)
    {
        QuestId = questId;
        linkedSurvivor = survivor;
    }

    public void StartGame(Player player) { }
}