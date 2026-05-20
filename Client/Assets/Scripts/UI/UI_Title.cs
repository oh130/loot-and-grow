using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI_Title : MonoBehaviour
{
    [SerializeField] private Text reasonText;

    private void Awake()
    {
        if (PlayerPrefs.HasKey("DisconnectReason"))
        {
            Debug.Log($"[UI_Title] DisconnectReason 읽어옴");
            string reason = PlayerPrefs.GetString("DisconnectReason");
            reasonText.text = reason;
            PlayerPrefs.DeleteKey("DisconnectReason");
            PlayerPrefs.Save();
            DOVirtual.DelayedCall(5f, () => {
                reasonText.text = "";
            });
        }
        else reasonText.text = "";
    }
}
