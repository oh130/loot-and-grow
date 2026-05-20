using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_UserInfo : MonoBehaviour
{
    [SerializeField] private Text infoText;
    [SerializeField] private Button[] buttons;
    [SerializeField] private InputField reasonInputField;

    private UserData _userData;

    private void Awake()
    {
        InitUI();
    }

    public void InitUI()
    {
        infoText.text = "";
        foreach(var button in buttons)
            button.interactable = false;
        reasonInputField.text = "";
    }

    public void UpdateUI(UserData userData)
    {
        _userData = userData;

        infoText.text = "";
        if(_userData.IsAdmin)
            infoText.text += $"닉네임: <color=red>{_userData.UserName}</color>\n";
        else
            infoText.text += $"닉네임: {_userData.UserName}\n";
        infoText.text += $"CID/DID: {_userData.ClientId}/{_userData.DbUserId}\n";
        infoText.text += $"IP 주소: {_userData.IPAddress}\n";
        infoText.text += $"Ping(ms): {_userData.Ping}\n";
        infoText.text += $"접속 경과 시간: {_userData.PlayTime}\n";
        infoText.text += $"현재 위치: X {_userData.Position.x:f1} / Y {_userData.Position.y:f1} / Z {_userData.Position.z:f1}\n";
        infoText.text += $"위치한 구역: {_userData.CurrentArea}\n";

        foreach(var button in buttons)
            button.interactable = true;
    }

    public void OnClickButton(int idx)
    {
        switch(idx)
        {
            case 0: // Kick
                ServerManager.Instance?.RequestKickUserRpc(_userData.ClientId, reasonInputField.text);
                InitUI();
                break;
            case 1: // Ban
                ServerManager.Instance?.RequestBanUserRpc(_userData.ClientId, reasonInputField.text);
                break;
            case 2: // Pull
                ServerManager.Instance?.RequestTeleportRpc(false, _userData.ClientId);
                break;
            case 3: // TP To
                ServerManager.Instance?.RequestTeleportRpc(true, _userData.ClientId);
                break;
            case 4: // 어드민 권한 변경
                ServerManager.Instance?.ChangeUserRoleRpc(_userData.ClientId);
                break;
            case 5: // Kill
                ServerManager.Instance?.RequestKillUserRpc(_userData.ClientId);
                break;
        }
    }
}
