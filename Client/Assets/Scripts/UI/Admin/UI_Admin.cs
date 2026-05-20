using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_Admin : UI_CanToggle
{
    public static UI_Admin Instance { get; private set; }

    [SerializeField] private UI_UserList uiUserList;
    [SerializeField] private UI_DBUserList uiDBUserList;
    [SerializeField] private Text refreshTimeText;

    private UserData[] _userDatas;
    private DBUserData[] _dbUserDatas;
    private Coroutine _updateCoroutine;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public override void ToggleUI(bool shouldShow = false)
    {
        base.ToggleUI(shouldShow);

        if(_isOpened)
        {
            ServerManager.Instance.RequestUserListRpc();
        }
        else
        {
            if(_updateCoroutine != null)
            {
                StopCoroutine(_updateCoroutine);
                _updateCoroutine = null;
            }
        }
    }

    public void UpdateData(UserData[] userDatas, DBUserData[] dBUserDatas)
    {
        if(!_isOpened) return;

        _userDatas = userDatas;
        _dbUserDatas = dBUserDatas;

        if(_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
        _updateCoroutine = StartCoroutine(UpdateUICycle());
    }

    private IEnumerator UpdateUICycle()
    {
        UpdateUI();
        yield return new WaitForSecondsRealtime(1f);

        ServerManager.Instance.RequestUserListRpc();

        _updateCoroutine = null;
    }

    private void UpdateUI()
    {
        // UI 갱신
        uiUserList.UpdateUI(_userDatas);
        uiDBUserList.UpdateUI(_dbUserDatas);
        refreshTimeText.text = "갱신: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
