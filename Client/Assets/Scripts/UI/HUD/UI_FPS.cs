using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_FPS : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private float updateInterval = 0.5f;

    private float _accumulatedTime = 0f;
    private int _frameCount = 0;
    private float _timeLeft;

    private void Start()
    {
        _timeLeft = updateInterval;
    }

    private void Update()
    {
        _timeLeft -= Time.unscaledDeltaTime;
        _accumulatedTime += Time.unscaledDeltaTime;
        _frameCount++;

        if (_timeLeft <= 0.0f)
        {
            float fps = _frameCount / _accumulatedTime;
            fpsText.text = $"FPS: {Mathf.Ceil(fps)}";

            _timeLeft = updateInterval;
            _accumulatedTime = 0.0f;
            _frameCount = 0;
        }
    }
}