using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Button_ExitServer : MonoBehaviour
{
    public void OnClick()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("AuthorizeScene");
    }
}
