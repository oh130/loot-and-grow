using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class UI_ChatContent : MonoBehaviour
{
    [SerializeField] private RectTransform rect;

    [SerializeField] private Text nameText;
    [SerializeField] private Text chatText;

    [SerializeField] float oneLineHeight = 80f;
    private float _addLineHeight = 40f;

    public void UpdateUI(ChatData data)
    {
        nameText.text = data.UserName.Value;
        chatText.text = data.ChatText.Value;

        int korCount = Regex.Matches(chatText.text, @"[가-힣]").Count;
        int engNumCount = Regex.Matches(chatText.text, @"[a-zA-Z0-9]").Count;
        int total = korCount * 2 + engNumCount;

        rect.sizeDelta = new Vector2(rect.sizeDelta.x, oneLineHeight + _addLineHeight * ((total-1) / 40));
    }
}
