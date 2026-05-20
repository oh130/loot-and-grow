using UnityEngine;
using UnityEngine.UI;

public class UI_BloodEffect : MonoBehaviour
{
    [SerializeField] private RawImage overlayImage;
    private float _threshold = 0.5f;
    
    private Material _bloodMat;
    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");

    private void Start()
    {
        _bloodMat = new Material(overlayImage.material);
        overlayImage.material = _bloodMat;
    }

    public void UpdateEffect(float curHp, float maxHp)
    {
        float healthPercent = curHp / maxHp;
        if (healthPercent <= _threshold)
        {
            float intensity = (1f - (healthPercent / _threshold)) * 1.8f;
            _bloodMat.SetFloat(IntensityID, intensity);
            
            if (!overlayImage.enabled) overlayImage.enabled = true;
        }
        else
        {
            _bloodMat.SetFloat(IntensityID, 0f);
            if (overlayImage.enabled) overlayImage.enabled = false;
        }
    }
}