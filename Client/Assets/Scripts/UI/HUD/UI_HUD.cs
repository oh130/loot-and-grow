using System.Collections;
using System.Runtime.InteropServices;
using DG.Tweening;
using TMPro;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public enum HUDState { Normal, NearbyNPC }

public class UI_HUD : UI_CanToggle
{
    [Header("HP Bar")]
    [SerializeField] private Image hpBarFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Action Buttons")]
    [SerializeField] private Button[] mainActionButtons;
    [SerializeField] private TextMeshProUGUI[] mainActionButtonCoverTexts;

    [Header("HP Flash Images")]
    [SerializeField] private Image hpFlashImage;
    [SerializeField] private Image hpFlashHitImage;

    [Header("Prefabs")]
    [SerializeField] private GameObject buffStatePrefab;

    [Header("Other Components")]
    [SerializeField] private GameObject uiBuffStates;
    [SerializeField] private UI_BloodEffect uiBloodEffect;
    [SerializeField] private CinemachineCamera cineCam;

    private HUDState _hudState = HUDState.Normal;
    private GameObject _interactTarget = null;

    private ClientManager _clientManager = null;
    private PlayerHealth _playerHealth = null;
    private PlayerRuntimeStat _playerRuntimeStat = null;
    private PlayerCombat _playerCombat = null;

    private Coroutine _attackButtonCoroutine = null;
    private Coroutine _skillButtonCoroutine = null;
    private Coroutine _shakeCoroutine = null;

    private void Update()
    {
        if(_hudState == HUDState.NearbyNPC)
        {
            if(_interactTarget == null || !_interactTarget.activeInHierarchy)
            {
                SetState(HUDState.Normal);
            }
        }
    }

    public void LinkPlayer(GameObject player)
    {
        _clientManager = player.GetComponent<ClientManager>();
        _clientManager.IsInitialized.OnValueChanged += OnInitializedChanged;

        _playerHealth = player.GetComponent<PlayerHealth>();
        _playerHealth.CurrentHp.OnValueChanged += OnHpChanged;

        _playerRuntimeStat = player.GetComponent<PlayerRuntimeStat>();
        _playerRuntimeStat.MaxHP.OnValueChanged += OnMaxHpChanged;
        _playerRuntimeStat.OnApplyBuff += ShowBuffIcon;

        _playerCombat = player.GetComponent<PlayerCombat>();
        _playerCombat.OnAttack += UpdateAttackButtonCooltime;
        _playerCombat.OnSkill += UpdateSkillButtonCooltime;
    }

    private void OnDestroy()
    {
        _clientManager.IsInitialized.OnValueChanged -= OnInitializedChanged;
        _playerHealth.CurrentHp.OnValueChanged -= OnHpChanged;
        _playerRuntimeStat.MaxHP.OnValueChanged -= OnMaxHpChanged;
        _playerRuntimeStat.OnApplyBuff -= ShowBuffIcon;
        _playerCombat.OnAttack -= UpdateAttackButtonCooltime;
        _playerCombat.OnSkill -= UpdateSkillButtonCooltime;
    }

    private void OnInitializedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            OnHpChanged(-1, _playerHealth.CurrentHp.Value);
        }
    }

    private void OnHpChanged(float oldHp, float newHp)
    {
        if(!_clientManager.IsInitialized.Value) return;

        if(oldHp > newHp)
        {
            if(_hudState == HUDState.NearbyNPC)
            {
                _interactTarget = null;
                SetState(HUDState.Normal);
            }

            CameraShake(1.5f + 0.5f * (1 - newHp / _playerRuntimeStat.MaxHP.Value), 4, 0.2f);
            FlashHpBarHit();
            SoundManager.Instance.PlaySFX2D("Hit");
        }
        else
        {
            if(newHp - oldHp >= 6)
                SoundManager.Instance.PlaySFX2D("Heal", 0.7f);
            else
                SoundManager.Instance.PlaySFX2D("Heal", 0.5f);
            FlashHpBar();
        }
        uiBloodEffect.UpdateEffect(newHp, _playerRuntimeStat.MaxHP.Value);

        hpText.text = $"{newHp:F2}/{_playerRuntimeStat.MaxHP.Value:F2}";

        float targetFill = newHp / _playerRuntimeStat.MaxHP.Value;
        hpBarFillImage.DOKill();
        hpBarFillImage.DOFillAmount(targetFill, 0.25f).SetEase(Ease.OutCubic).SetLink(gameObject, LinkBehaviour.KillOnDestroy | LinkBehaviour.KillOnDisable);
    }

    private void OnMaxHpChanged(float oldHp, float newHp)
    {
        if(!_clientManager.IsInitialized.Value) return;

        hpText.text = $"{_playerHealth.CurrentHp.Value}/{newHp}";
        hpBarFillImage.fillAmount = _playerHealth.CurrentHp.Value / newHp;
    }

    private void FlashHpBar()
    {
        if(hpFlashImage == null) return;

        hpFlashImage.DOKill();
        Color color = hpFlashImage.color;
        hpFlashImage.color = new Color(color.r, color.g, color.b, 0.6f);
        hpFlashImage.DOFade(0f, 0.25f).SetLink(gameObject, LinkBehaviour.KillOnDestroy | LinkBehaviour.KillOnDisable);
    }

    private void FlashHpBarHit()
    {
        if(hpFlashHitImage == null) return;

        hpFlashHitImage.DOKill();
        Color color = hpFlashHitImage.color;
        hpFlashHitImage.color = new Color(color.r, color.g, color.b, 1f);
        hpFlashHitImage.DOFade(0f, 0.25f).SetLink(gameObject, LinkBehaviour.KillOnDestroy | LinkBehaviour.KillOnDisable);
    }


    // 버튼 관련 ---------------------------------------------
    public void SetState(HUDState state, GameObject interactTargetNpc = null)
    {
        _hudState = state;
        if(state == HUDState.Normal)
        {
            _interactTarget = null;

            mainActionButtons[0].gameObject.SetActive(true);
            mainActionButtons[1].gameObject.SetActive(false);
        }
        else if(state == HUDState.NearbyNPC)
        {
            _interactTarget = interactTargetNpc;

            mainActionButtons[0].gameObject.SetActive(false);
            mainActionButtons[1].gameObject.SetActive(true);
        }
    }

    public void OnClickInteractButton()
    {
        if(_interactTarget == null) return;

        if(_interactTarget.TryGetComponent<IInteractable>(out IInteractable component))
        {
            component.Interact();
        }
    }

    private void UpdateAttackButtonCooltime(float cooltime)
    {
        if(_attackButtonCoroutine != null)
        {
            StopCoroutine(_attackButtonCoroutine);
            _attackButtonCoroutine = null;
        }
        _attackButtonCoroutine = StartCoroutine(ButtonCooltime(0, cooltime));
    }

    private void UpdateSkillButtonCooltime(float cooltime)
    {
        if(_skillButtonCoroutine != null)
        {
            StopCoroutine(_skillButtonCoroutine);
            _skillButtonCoroutine = null;
        }
        _skillButtonCoroutine = StartCoroutine(ButtonCooltime(2, cooltime));
    }

    private IEnumerator ButtonCooltime(int buttonIdx, float cooltime)
    {
        mainActionButtons[buttonIdx].interactable = false;
        mainActionButtonCoverTexts[buttonIdx].text = "";

        float startTime = Time.time;
        float endTime = startTime + cooltime;

        while (Time.time < endTime)
        {
            float remaining = endTime - Time.time;
            mainActionButtonCoverTexts[buttonIdx].text = $"{remaining:F1}";
            yield return null;
        }

        mainActionButtons[buttonIdx].interactable = true;
        mainActionButtonCoverTexts[buttonIdx].text = "";

        if(buttonIdx == 0) _attackButtonCoroutine = null;
        else if(buttonIdx == 2) _skillButtonCoroutine = null;
    }
    // ---------------------------------------------------------------------

    private void ShowBuffIcon(string effectType, float duration, float value)
    {
        UI_BuffState uiBuffState = Instantiate(buffStatePrefab).GetComponent<UI_BuffState>();
        uiBuffState.transform.parent = uiBuffStates.transform;
        uiBuffState.Init(effectType, duration, value);

        SoundManager.Instance.PlaySFX2D("Buff", 0.75f);
    }

    public void CameraShake(float amplitude, float frequency, float duration)
    {
        if(_shakeCoroutine != null){
            StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = null;
        }
        _shakeCoroutine = StartCoroutine(ShakeCoroutine(amplitude, frequency, duration));
    }

    private IEnumerator ShakeCoroutine(float amplitude, float frequency, float duration)
    {
        CinemachineBasicMultiChannelPerlin noise = cineCam.GetComponent<CinemachineBasicMultiChannelPerlin>();
        
        noise.AmplitudeGain = amplitude;
        noise.FrequencyGain = frequency;
        yield return new WaitForSecondsRealtime(duration);

        noise.AmplitudeGain = 0;
        noise.FrequencyGain = 0;

        _shakeCoroutine = null;
    }
}
