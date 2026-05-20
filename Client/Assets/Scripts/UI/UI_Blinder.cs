using DG.Tweening;
using UnityEngine;

public class UI_Blinder : UI_CanToggle
{
    public override void ToggleUI(bool shouldShow = false)
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
            canvasGroup.DOFade(0f, 1f).OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            });
        }
    }
}
