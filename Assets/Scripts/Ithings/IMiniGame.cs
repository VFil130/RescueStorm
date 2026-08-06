using UnityEngine;

public interface IMiniGame
{
    string QuestId { get; }
    bool IsCompleted { get; }
    void StartGame(Player player);
    void CompleteGame(bool success);
    void Initialize(string questId, Survivor survivor);
}