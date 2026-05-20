using UnityEngine;
using TMPro;
using DG.Tweening;

public class UI_DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.0f, 0);

    public void Show(float damage, Vector3 worldPos, bool isCritical)
    {
        damageText.text = damage >= 0 ? "+" : "";
        damageText.text += $"{damage:F2}";
        if(damage >= 0) damageText.color = Color.green;
        else damageText.color = isCritical? Color.red : Color.white;
        damageText.alpha = 1f;
        
        Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
        transform.position = worldPos + offset + randomOffset;

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        
        seq.Append(transform.DOMoveY(transform.position.y + 0.4f, 0.6f).SetEase(Ease.OutBack));
        
        seq.Join(damageText.DOFade(0, 0.4f).SetDelay(0.3f));
        
        seq.OnComplete(() => {
            gameObject.SetActive(false);
        });
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
        }
    }
}