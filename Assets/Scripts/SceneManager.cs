using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerService
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void LoadSceneAdditive(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }

    public void LoadSceneAdditive(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex, LoadSceneMode.Additive);
    }

    public void UnloadScene(string sceneName)
    {
        SceneManager.UnloadSceneAsync(sceneName);
    }

    public void UnloadScene(int sceneIndex)
    {
        SceneManager.UnloadSceneAsync(sceneIndex);
    }

    public void ReloadScene()
    {
        int currentScene = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentScene);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}