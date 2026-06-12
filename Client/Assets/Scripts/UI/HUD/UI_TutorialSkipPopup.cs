using UnityEngine;

public class UI_TutorialSkipPopup : UI_CanToggle
{
    [SerializeField] private TutorialManager tutorialManager;
    public void OnClickConfirm()
    {
        // 바로 장비 받기
        tutorialManager?.OpenStarterWeaponPanel();
        ToggleUI();
    }

    public void OnClickCancel()
    {
        // 튜토 띄우기
        tutorialManager?.OpenTutorialMenu();
        ToggleUI();
    }
}
