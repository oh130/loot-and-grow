using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_BuffState : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Image durationFillImage;

    [SerializeField] private Sprite[] iconSprites;

    private float _remainTime = -1;
    private float _maxTime = -1;

    public void Init(string effectType, float duration, float value)
    {
        if(Enum.TryParse(effectType, out PlayerStatType result))
        {
            iconImage.sprite = iconSprites[(int)result];
        }

        _remainTime = _maxTime = duration;
        string prefix = value >= 0? "+" : "-";
        valueText.text = $"{prefix}{value:F2}";
        durationFillImage.fillAmount = 0f;
    }

    private void Update()
    {
        if(_remainTime > 0f)
        {
            _remainTime -= Time.unscaledDeltaTime;
            durationFillImage.fillAmount = Mathf.Min(1f - _remainTime / _maxTime, 1f);
            if(_remainTime <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
