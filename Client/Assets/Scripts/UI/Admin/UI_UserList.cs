using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class UI_UserList : MonoBehaviour
{
    [SerializeField] private Text totalText;
    [SerializeField] private GameObject content;
    [SerializeField] private GameObject contentPrefab;
    [SerializeField] private UI_UserInfo uiUserInfo;

    private UserData[] _userDatas;
    private List<GameObject> _currentItems = new List<GameObject>();
    private UserData _curUserData;

    public void UpdateUI(UserData[] userDatas)
    {
        _userDatas = userDatas;

        foreach (var item in _currentItems) Destroy(item);
        _currentItems.Clear();

        foreach (UserData data in _userDatas)
        {
            UI_UserListContent item = Instantiate(contentPrefab, content.transform).GetComponent<UI_UserListContent>();
            _currentItems.Add(item.gameObject);

            item.SetText(data.UserName.ToString());
            item.Init(this, data);

            if(data.UserName == _curUserData.UserName) UpdateInfo(data);
        }
        
        totalText.text = $"{_userDatas.Length}명";
    }

    public void UpdateInfo(UserData userData)
    {
        _curUserData = userData;
        uiUserInfo.UpdateUI(userData);
    }
}
