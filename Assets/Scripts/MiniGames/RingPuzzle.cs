using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class RingPuzzle : NetworkBehaviour, IMiniGame
{
    [SerializeField] private List<Ring> rings;
    [SerializeField] private int ringCount = 3;
    [SerializeField] private float rotationStep = 45f;
    [SerializeField] private InterButton selectButton;
    [SerializeField] private InterButton leftButton;
    [SerializeField] private InterButton rightButton;

    private bool isCompleted = false;
    private Survivor linkedSurvivor;
    private int selectedRing = 0;
    private readonly SyncList<int> ringSteps = new SyncList<int>();

    public string QuestId { get; private set; }
    public bool IsCompleted => isCompleted;

    public override void OnStartServer()
    {
        base.OnStartServer();

        ResetPuzzle();
        selectButton.onPress.AddListener(SelectNextRing);
        leftButton.onPress.AddListener(RotateLeft);
        rightButton.onPress.AddListener(RotateRight);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ringSteps.Callback += OnRingStepsChanged;
        ApplyAllRingSteps();
    }

    public override void OnStopClient()
    {
        ringSteps.Callback -= OnRingStepsChanged;
        base.OnStopClient();
    }

    private void SelectNextRing()
    {
        if (isCompleted) return;
        selectedRing = (selectedRing + 1) % ringCount;
    }

    private void RotateLeft()
    {
        if (isCompleted) return;
        RotateRing(selectedRing, false);
    }

    private void RotateRight()
    {
        if (isCompleted) return;
        RotateRing(selectedRing, true);
    }

    private void RotateRing(int ringIndex, bool clockwise)
    {
        if (isCompleted) return;
        if (ringIndex < 0 || ringIndex >= rings.Count) return;

        int direction = clockwise ? 1 : -1;
        int step = (ringSteps[ringIndex] + direction) % 8;
        if (step < 0) step += 8;

        ringSteps[ringIndex] = step;
        ApplyRotation(ringIndex, step);

        CheckCompletion();
    }

    private void ApplyRotation(int ringIndex, int step)
    {
        if (ringIndex < rings.Count && rings[ringIndex].transform != null)
        {
            rings[ringIndex].currentStep = step;
            rings[ringIndex].transform.localRotation = Quaternion.Euler(0, 0, step * rotationStep);
        }
    }

    private void OnRingStepsChanged(
        SyncList<int>.Operation _,
        int index,
        int __,
        int newStep)
    {
        if (index >= 0) ApplyRotation(index, newStep);
    }

    private void CheckCompletion()
    {
        for (int i = 0; i < ringCount; i++)
        {
            if (i >= ringSteps.Count || ringSteps[i] != 0) return;
        }
        CompleteGame(true);
    }

    private void ResetPuzzle()
    {
        ringSteps.Clear();
        for (int i = 0; i < rings.Count; i++)
        {
            int step = Random.Range(0, 8);
            ringSteps.Add(step);
            ApplyRotation(i, step);
        }
        selectedRing = 0;
    }

    private void ApplyAllRingSteps()
    {
        int count = Mathf.Min(rings.Count, ringSteps.Count);
        for (int i = 0; i < count; i++) ApplyRotation(i, ringSteps[i]);
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

    [System.Serializable]
    public class Ring
    {
        public Transform transform;
        public int currentStep = 0;
    }
}
