using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI_Title : MonoBehaviour
{
    [SerializeField] private Text reasonText;
    [SerializeField] private LoginManager loginManager;

    private void Awake()
    {
        bool isKicked = false;
        if (PlayerPrefs.HasKey("DisconnectReason"))
        {
            isKicked = true;
            Debug.Log($"[UI_Title] DisconnectReason 읽어옴");
            string reason = PlayerPrefs.GetString("DisconnectReason");
            reasonText.text = reason;
            PlayerPrefs.DeleteKey("DisconnectReason");
            PlayerPrefs.Save();
            DOVirtual.DelayedCall(10f, () => {
                reasonText.text = "";
            });
        }
        else reasonText.text = "";

        if(PlayerPrefs.HasKey("DoUpdate"))
        {
            int flag = PlayerPrefs.GetInt("DoUpdate");
            if(flag == 1)
            {
                string packageId = Application.identifier; 
                Application.OpenURL($"market://details?id={packageId}");
            }

            PlayerPrefs.DeleteKey("DoUpdate");
            PlayerPrefs.Save();
        }

        if(!isKicked) loginManager?.AutoLogin();
    }
}
