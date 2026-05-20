using UnityEngine;
using UnityEngine.Rendering;

public class UI_CanToggle : MonoBehaviour
{
    [SerializeField] protected CanvasGroup canvasGroup;

    public bool IsOpened => _isOpened;
    protected bool _isOpened = false;

    public virtual void ShowUI()
    {
        ToggleUI(true);
    }

    public virtual void HideUI()
    {
        _isOpened = false;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public virtual void ToggleUI(bool shouldShow = false)
    {
        _isOpened = !_isOpened;
        if(shouldShow) _isOpened = true;

        if (_isOpened)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
