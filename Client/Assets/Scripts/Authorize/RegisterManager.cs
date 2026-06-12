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

    public void OnBackButtonClick()
    {
        SoundManager.Instance?.PlaySFX2D("ItemSelect");
        uiManager.ShowLogin();
    }

    public void OnRegisterButtonClick()
    {
        SoundManager.Instance?.PlaySFX2D("ItemSelect");
        if (string.IsNullOrEmpty(idInput.text) || string.IsNullOrEmpty(pwInput.text) || string.IsNullOrEmpty(nameInput.text))
        {
            statusText.text = "정보를 입력해 주세요.";
            return;
        }
        StartCoroutine(RegisterRoutine());
    }

    IEnumerator RegisterRoutine()
    {
        statusText.text = "처리 중...";

        yield return StartCoroutine(DataAPI.Register(idInput.text, pwInput.text, nameInput.text,
            onSuccess: _ =>
            {
                statusText.text = "계정 생성 성공!";
                StartCoroutine(GoToLogin());
            },
            onError: error => statusText.text = error.Contains("400") ? "이미 존재하는 아이디입니다." : "서버 오류 발생"
        ));
    }

    IEnumerator GoToLogin()
    {
        yield return new WaitForSeconds(1.5f);
        uiManager.ShowLogin();
    }
}
