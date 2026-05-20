using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class RegisterManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField idInput;
    [SerializeField] private TMP_InputField pwInput;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Manager Reference")]
    [SerializeField] private AuthUIManager uiManager;

    public void OnRegisterButtonClick()
    {
        if (string.IsNullOrEmpty(idInput.text) || string.IsNullOrEmpty(pwInput.text))
        {
            statusText.text = "please enter id and pw.";
            return;
        }
        StartCoroutine(RegisterRoutine());
    }

    IEnumerator RegisterRoutine()
    {
        statusText.text = "requesting server...";

        yield return StartCoroutine(DataAPI.Register(idInput.text, pwInput.text, nameInput.text,
            onSuccess: _ =>
            {
                statusText.text = "register success.";
                StartCoroutine(GoToLogin());
            },
            onError: error => statusText.text = error.Contains("400") ? "id exists already." : "server error."
        ));
    }

    IEnumerator GoToLogin()
    {
        yield return new WaitForSeconds(1.5f);
        uiManager.ShowLogin();
    }
}
