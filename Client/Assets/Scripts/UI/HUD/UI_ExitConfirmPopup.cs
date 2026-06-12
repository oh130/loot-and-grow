using Unity.Netcode;
using UnityEngine;

public class UI_ExitConfirmPopup : UI_CanToggle
{
    [SerializeField] private bool isInGame = true;

    private void Update()
    {
        if(!isInGame && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleUI();
        }
    }

    public void OnClickConfirm()
    {
        if(isInGame)
        {
            NetworkManager.Singleton?.Shutdown();
        }
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
