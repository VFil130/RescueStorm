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

    public string QuestId { get; private set; }
    public bool IsCompleted => isCompleted;

    void Start()
    {
        if (!isServer) return;

        ResetPuzzle();

        selectButton.onPress.AddListener(SelectNextRing);
        leftButton.onPress.AddListener(RotateLeft);
        rightButton.onPress.AddListener(RotateRight);
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
        rings[ringIndex].currentStep += direction;
        rings[ringIndex].currentStep %= 8;
        if (rings[ringIndex].currentStep < 0) rings[ringIndex].currentStep += 8;

        ApplyRotation(ringIndex, rings[ringIndex].currentStep);
        RpcRotateRing(ringIndex, rings[ringIndex].currentStep);

        CheckCompletion();
    }

    private void ApplyRotation(int ringIndex, int step)
    {
        if (ringIndex < rings.Count && rings[ringIndex].transform != null)
        {
            rings[ringIndex].transform.localRotation = Quaternion.Euler(0, 0, step * rotationStep);
        }
    }

    [ClientRpc]
    private void RpcRotateRing(int ringIndex, int step)
    {
        ApplyRotation(ringIndex, step);
    }

    private void CheckCompletion()
    {
        for (int i = 0; i < ringCount; i++)
        {
            if (rings[i].currentStep != 0) return;
        }
        CompleteGame(true);
    }

    private void ResetPuzzle()
    {
        for (int i = 0; i < rings.Count; i++)
        {
            rings[i].currentStep = Random.Range(0, 8);
            ApplyRotation(i, rings[i].currentStep);
        }
        selectedRing = 0;
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