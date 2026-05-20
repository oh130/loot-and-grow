using UnityEngine;
using UnityEngine.EventSystems;

public class UI_ResizeHandle : MonoBehaviour //, IDragHandler
{
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private float minHeight = 80f;
    [SerializeField] private float maxHeight = 480f;
    private float _savedHeight;
    private bool _isMax = false;

    // public void OnDrag(PointerEventData eventData)
    // {
    //     if (targetRect == null) return;

    //     float newHeight = targetRect.sizeDelta.y + eventData.delta.y;

    //     newHeight = Mathf.Clamp(newHeight, minHeight, maxHeight);

    //     targetRect.sizeDelta = new Vector2(targetRect.sizeDelta.x, newHeight);
    // }

    public void OnClick()
    {
        if (targetRect == null) return;

        if(_isMax)
        {
            targetRect.sizeDelta = new Vector2(targetRect.sizeDelta.x, _savedHeight);
        }
        else
        {
            _savedHeight = targetRect.sizeDelta.y;
            float newHeight = maxHeight;
            targetRect.sizeDelta = new Vector2(targetRect.sizeDelta.x, newHeight);
        }
        _isMax = !_isMax;
    }
}
