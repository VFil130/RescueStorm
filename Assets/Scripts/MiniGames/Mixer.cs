using Mirror;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class Mixer : NetworkBehaviour, IInteractable
{
    [SerializeField] private int[] correctSequence;
    [SerializeField] private GameObject successBottlePrefab;
    [SerializeField] private GameObject failBottlePrefab;
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 3f;
    private bool[] currentSequence = new bool[3];
    private int currentIndex = 0;
    private bool isCompleted = false;
    private float timer = 2f;
    private bool isRunning = false;

    void Update()
    {
        if (!isServer) return;
        if (isRunning)
        {
            timer += Time.deltaTime;
        }
    }

    public void Interact(GameObject gm)
    {
        if (!isServer) return;
        if (isCompleted) return;

        Player player = gm.GetComponent<Player>();
        IPickupable item = player?.GetSelectedIPickable();
        if (item == null) return;

        if (item is BottleItem bottle)
        {
            if (bottle.bottleID == 0)
            {
                TryFinishMix(player);
                return;
            }
            

            player.DestroySelectedItem();
            if (correctSequence[currentIndex] == bottle.bottleID && IsBetween(timer, minDelay, maxDelay))
            {
                currentSequence[currentIndex] = true;
            }
            else
            {
                currentSequence[currentIndex] = false;
            }
            currentIndex++;
            timer = 0f;
            isRunning = true;
        }
    }

    private void TryFinishMix(Player player)
    {
        if (currentIndex < 3) return;

        bool allTrue = true;
        foreach (bool b in currentSequence)
        {
            if (!b) { allTrue = false; break; }
        }

        GameObject result = allTrue ? successBottlePrefab : failBottlePrefab;
        player.ReplaceSelectedItem(result);
        isCompleted = true;
    }
    public static bool IsBetween(float value, float a, float b)
    {
        return value >= Mathf.Min(a, b) && value <= Mathf.Max(a, b);
    }
}