using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_DBUserInfo : MonoBehaviour
{
    [SerializeField] private Text infoText;
    [SerializeField] private Button[] buttons;

    private DBUserData _userData;

    private void Awake()
    {
        InitUI();
    }

    public void InitUI()
    {
        infoText.text = "";
        foreach(var button in buttons)
            button.interactable = false;
    }

    public void UpdateUI(DBUserData userData)
    {
        _userData = userData;

        infoText.text = "";
        if(_userData.Role == "admin")
            infoText.text += $"닉네임: <color=red>{_userData.UserName}</color>\n";
        else
            infoText.text += $"닉네임: {_userData.UserName}\n";
        infoText.text += $"DID: {_userData.DbUserId}\n";
        infoText.text += $"로그인 ID: {_userData.LoginID}\n";
        infoText.text += $"밴 여부: {_userData.IsBanned}\n";
        infoText.text += $"마지막 로그인: {_userData.LastLoginAt}\n";

        foreach(var button in buttons)
            button.interactable = true;
    }

    public void OnClickButton(int idx)
    {
        switch(idx)
        {
            case 0:
                ServerManager.Instance.RequestBanToggleByDBIdRpc(_userData);
                break;
            case 1:
                ServerManager.Instance.ChangeUserRoleByDBIdRpc(_userData);
                break;
        }
    }
}
