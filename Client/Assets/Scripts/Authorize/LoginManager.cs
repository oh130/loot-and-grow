using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField idInput;
    [SerializeField] private TMP_InputField pwInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Manager Reference")]
    [SerializeField] private AuthUIManager uiManager;

    public void OnLoginButtonClick()
    {
        StartCoroutine(LoginRoutine(idInput.text, pwInput.text));
    }

    public void OnToRegisterButtonClick()
    {
        uiManager.ShowRegister();
    }

    IEnumerator LoginRoutine(string id, string pw)
    {
        statusText.text = "Login...";

        yield return StartCoroutine(DataAPI.Login(id, pw,
            onSuccess: res =>
            {
                UserSession.Apply(res);
                statusText.text = res.is_banned ? "(경고: 밴된 계정입니다)" : $"Welcome, {res.username}!";
            },
            onError: _ => statusText.text = "wrong id or password."
        ));

        if (!UserSession.IsLoggedIn || UserSession.IsBanned) yield break;

        yield return new WaitForSeconds(0.5f);
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            // 씬 이동 자체를 서버가 관리하기에, 클라이언트로써 시작만 하면 됨
            NetworkManager.Singleton.StartClient();
        }
    }
}
