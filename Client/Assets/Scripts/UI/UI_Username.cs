using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Username : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameObject hpBarParent;
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TextMeshProUGUI hpBarText;
    private Transform _targetTransform;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.15f, 0);
    private Vector3 _savedOffset;

    private void Awake()
    {
        _savedOffset = offset;
    }

    public void Init(Transform target, string name, bool hasHP = false, bool noText = false)
    {
        _targetTransform = target;
        text.text = name;
        offset = _savedOffset;
        offset.y *= _targetTransform.localScale.y;

        if(hasHP)
        {
            if(hpBarParent != null)
            {
                hpBarParent.SetActive(true);
                if(noText) hpBarText.enabled = false;
                else hpBarText.enabled = true;
            }
        }
        else
        {
            if(hpBarParent != null)
                hpBarParent?.SetActive(false);
        }
            
    }

    public void SetScaleAndOffset(float scaleMultiplier, Vector3 addOffset)
    {
        rect.localScale *= scaleMultiplier;
        offset += addOffset;
    }

    public void SetHealth(float currentHp, float maxHp)
    {
        if (hpBarFill == null) return;
        
        float ratio = maxHp > 0 ? currentHp / maxHp : 0;
        hpBarText.text = $"{currentHp:F2}/{maxHp:F2}";

        hpBarFill.DOKill();
        hpBarFill.DOFillAmount(ratio, 0.25f).SetEase(Ease.OutCubic).SetLink(gameObject, LinkBehaviour.KillOnDestroy | LinkBehaviour.KillOnDisable);
    }

    public void SetText(string name)
    {
        text.text = name;
    }

    private void LateUpdate()
    {
        if (_targetTransform == null)
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = _targetTransform.position + offset;

        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
        }
    }
}
