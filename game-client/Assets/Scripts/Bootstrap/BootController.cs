using QuizBattle.Bootstrap;
using QuizBattle.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Boot scene's only job: make sure AppRoot exists, detect endpoint, and load NameEntry directly.
public class BootController : MonoBehaviour
{
    private void Start()
    {
        _ = AppRoot.Instance;
        SessionManager.AutoDetectEndpoint();
        SceneManager.LoadScene("NameEntry");
    }
}
