using UnityEngine;
using UnityEngine.SceneManagement;
public class GameMenu : MonoBehaviour
{
    [SerializeField] string GameSceneName;

    public void LoadGameScene()
    {
        SceneManager.LoadScene(GameSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
