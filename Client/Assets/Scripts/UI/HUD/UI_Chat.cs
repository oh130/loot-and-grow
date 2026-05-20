using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UI_Chat : MonoBehaviour
{
    [SerializeField] private GameObject content;
    [SerializeField] private GameObject contentPrefab;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private CanvasGroup inputUICG;

    [SerializeField] private InputField chatField;

    private List<GameObject> _currentItems = new List<GameObject>();
    private int _maxChatContent = 20;

    private float _pressTime;
    private float _windowTime = 0.2f;
    private bool _isInputUIOpen = false;
    private bool _isSubmitted = false;

    private void Awake()
    {
        ServerManager.OnChatReceived += UpdateUI;
    }

    private void OnDestroy()
    {
        ServerManager.OnChatReceived -= UpdateUI;
    }

    private void Update()
    {
        if(_isInputUIOpen)
        {
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                SetInputUI(false);
            }
        }
    }

    public void UpdateUI(ChatData data)
    {
        if(_currentItems.Count >= _maxChatContent)
        {
            Destroy(_currentItems[0]);
            _currentItems.RemoveAt(0);
        }

        UI_ChatContent item = Instantiate(contentPrefab, content.transform).GetComponent<UI_ChatContent>();
        _currentItems.Add(item.gameObject);

        item.UpdateUI(data);
        
        StartCoroutine(UpdateScrollValue());

        SoundManager.Instance.PlaySFX2D("ReceiveChat", 0.35f);
    }

    private IEnumerator UpdateScrollValue()
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    public void OnClickPressed()
    {
        _pressTime = Time.time;
    }

    public void OnClickReleased()
    {
        if(Time.time - _pressTime <= _windowTime && !_isInputUIOpen) SetInputUI(true);
    }

    private void SetInputUI(bool flag)
    {
        _isInputUIOpen = flag;
        if(flag)
        {
            inputUICG.alpha = 1f;
            inputUICG.interactable = true;
            inputUICG.blocksRaycasts = true;

            chatField.ActivateInputField();
            chatField.Select();
        }
        else
        {
            inputUICG.alpha = 0f;
            inputUICG.interactable = true;
            inputUICG.blocksRaycasts = true;

            chatField.DeactivateInputField();
            chatField.text = "";
        }
    }

    public void OnClickSend()
    {   
        if(chatField.text == "") return;

        string safeText = CutTo120Bytes(chatField.text);
        ServerManager.Instance.SendChatByUserRpc(safeText);

        _isSubmitted = true;
    }

    public void OnEndEdit()
    {
        if(_isSubmitted)
        {
            _isSubmitted = false;
            StartCoroutine(ClearInputField());
        }
        else SetInputUI(false);
    }

    private IEnumerator ClearInputField()
    {
        yield return null;
        chatField.DeactivateInputField();
        chatField.text = "";
        chatField.ActivateInputField();
        chatField.Select();
    }

    public string CutTo120Bytes(string input)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(input);

        if (utf8Bytes.Length <= 120)
        {
            return input;
        }

        int cutIndex = 120;
        while (cutIndex > 0 && (utf8Bytes[cutIndex] & 0xC0) == 0x80)
        {
            cutIndex--;
        }

        return Encoding.UTF8.GetString(utf8Bytes, 0, cutIndex);
    }
}