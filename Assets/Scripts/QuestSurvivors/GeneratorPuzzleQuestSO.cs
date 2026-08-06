using UnityEngine;

[CreateAssetMenu(fileName = "GeneratorPuzzleQuest", menuName = "Quest/GeneratorPuzzleQuest")]
public class GeneratorPuzzleQuestSO : MinigameQuestSO
{
    [SerializeField] private GameObject puzzlePrefab;
    private GeneratorPuzzle puzzleInstance;

    public override void Initialize(Survivor survivor)
    {
        base.Initialize(survivor);
        owner = survivor;

        if (puzzlePrefab != null)
        {
            GameObject puzzleObj = Instantiate(puzzlePrefab);
            puzzleInstance = puzzleObj.GetComponent<GeneratorPuzzle>();
            puzzleInstance.Initialize(questId, survivor);
            SetMiniGame(puzzleInstance);
        }
    }
}