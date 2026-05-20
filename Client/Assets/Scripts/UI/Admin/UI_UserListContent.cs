using UnityEngine;
using UnityEngine.UI;

public class UI_UserListContent : MonoBehaviour
{
    [SerializeField] private Text nameText;

    private UI_UserList _uiUserList;
    private UserData _userData;

    public void Init(UI_UserList uiUserList, UserData userData)
    {
        _uiUserList = uiUserList;
        _userData= userData;
    }

    public void SetText(string text)
    {
        nameText.text = text;
    }

    public void OnClick()
    {
        _uiUserList.UpdateInfo(_userData);
    }
}
