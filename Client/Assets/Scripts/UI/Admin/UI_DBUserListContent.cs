using UnityEngine;
using UnityEngine.UI;

public class UI_DBUserListContent : MonoBehaviour
{
    [SerializeField] private Text nameText;

    private UI_DBUserList _uiUserList;
    private DBUserData _userData;

    public void Init(UI_DBUserList uiUserList, DBUserData userData)
    {
        _uiUserList = uiUserList;
        _userData = userData;
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

