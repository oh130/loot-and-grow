using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;
#if GOOGLE_SIGN_IN
using Google;
#endif

public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField idInput;
    [SerializeField] private TMP_InputField pwInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Manager Reference")]
    [SerializeField] private AuthUIManager uiManager;
    [SerializeField] private BotManager botManager;

    [Header("Google Sign-In")]
    [SerializeField] private string googleWebClientId = "YOUR_WEB_CLIENT_ID";

    private string _pendingGoogleToken = null;
    private string _pendingStatus = null;

    private void Update()
    {
        if (_pendingStatus != null)
        {
            statusText.text = _pendingStatus;
            _pendingStatus = null;
        }
        if (_pendingGoogleToken != null)
        {
            string token = _pendingGoogleToken;
            _pendingGoogleToken = null;
#if GOOGLE_SIGN_IN
            StartCoroutine(GoogleLoginRoutine(token));
#endif
        }
    }

    public void AutoLogin()
    {
        #if !UNITY_SERVER && !UNITY_EDITOR
        if(!botManager.enabled) StartCoroutine(TryAutoLogin());
        #endif
    }

    public void OnLoginButtonClick()
    {
        SoundManager.Instance?.PlaySFX2D("ItemSelect");
        #if !UNITY_EDITOR
        if(idInput.text.Contains("admin")) return;
        #endif
        StartCoroutine(LoginRoutine(idInput.text, pwInput.text));
    }

    public void OnToRegisterButtonClick()
    {
        SoundManager.Instance?.PlaySFX2D("ItemSelect");
        uiManager.ShowRegister();
    }

    public void OnGoogleLoginButtonClick()
    {
#if GOOGLE_SIGN_IN
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId    = googleWebClientId,
            RequestIdToken = true
        };
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                _pendingStatus = "Google 로그인 취소 또는 실패";
                return;
            }
            _pendingGoogleToken = task.Result.IdToken;
        });
#else
        statusText.text = "Google 로그인 미지원 (플러그인 미설치)";
#endif
    }

#if GOOGLE_SIGN_IN
    private IEnumerator GoogleLoginRoutine(string idToken)
    {
        statusText.text = "Google 로그인 중...";

        yield return StartCoroutine(DataAPI.GoogleLogin(idToken,
            onSuccess: res =>
            {
                UserSession.Apply(res);
                statusText.text = $"Welcome, {res.username}!";
            },
            onError: _ => statusText.text = "Google 로그인 실패"
        ));

        if (!UserSession.IsLoggedIn) yield break;

        yield return new WaitForSeconds(0.5f);
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.StartClient();
    }
#endif

    private IEnumerator TryAutoLogin()
    {
        string savedRefreshToken = UserSession.LoadRefreshTokenFromPrefs();
        if (string.IsNullOrEmpty(savedRefreshToken)) yield break;

        statusText.text = "자동 로그인 중...";

        yield return StartCoroutine(DataAPI.RefreshLogin(savedRefreshToken,
            onSuccess: res =>
            {
                UserSession.Apply(res);
                statusText.text = "자동 로그인 완료\n서버에 접속하는 중...";
            },
            onError: _ =>
            {
                UserSession.ClearPrefs();
                statusText.text = "";
            }
        ));

        if (!UserSession.IsLoggedIn) yield break;

        yield return new WaitForSeconds(0.3f);
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.StartClient();
            yield return new WaitForSeconds(5f);
            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                NetworkManager.Singleton.Shutdown();
                statusText.text = "서버 접속에 실패했습니다.";
            }
        }
    }

    IEnumerator LoginRoutine(string id, string pw)
    {
        statusText.text = "로그인 중...";

        yield return StartCoroutine(DataAPI.Login(id, pw,
            onSuccess: res =>
            {
                UserSession.Apply(res);
                statusText.text = res.is_banned ? "(경고: 밴된 계정입니다)" : $"서버에 접속하는 중...";
            },
            onError: _ => statusText.text = "로그인에 실패했습니다."
        ));

        if (!UserSession.IsLoggedIn || UserSession.IsBanned) yield break;

        yield return new WaitForSeconds(0.5f);
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.StartClient();
            yield return new WaitForSeconds(5f);
            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                NetworkManager.Singleton.Shutdown();
                statusText.text = "서버 접속에 실패했습니다.";
            }
        }
    }

    public void BotLogin(string id, string pw)
    {
        StartCoroutine(LoginRoutine(id,pw));
    }
}
