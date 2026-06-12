using System.Collections;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject tutorialMenuPanel;     // TutorialButton
    public GameObject tutorialBackground;    // Background
    public GameObject tutorialRoot;          // Tutorial

    [SerializeField] private UI_TutorialSkipPopup uiSkip;

    [Header("신규 유저 무기 선택")]
    [SerializeField] private GameObject starterWeaponPanel;

    private GameObject currentTutorial;
    private int currentIndex;
    private bool isTutorialActive;
    private string TutorialShownKey => "TutorialShown_" + UserSession.DbUserId;

    void Start()
    {
        if (tutorialMenuPanel != null)
            tutorialMenuPanel.SetActive(false);

        if (tutorialBackground != null)
            tutorialBackground.SetActive(false);

        if (starterWeaponPanel != null)
            starterWeaponPanel.SetActive(false);

        HideAllTutorials();
    }

    // 튜토리얼 NPC 상호작용 시 호출
    public void InteractFromNPC()
    {
        if (PlayerPrefs.GetInt(TutorialShownKey, 0) == 0)
        {
            PlayerPrefs.SetInt(TutorialShownKey, 1);
            OpenTutorialMenu();
        }
        else
        {
            uiSkip?.ToggleUI();
        }
    }

    public void OpenStarterWeaponPanel()
    {
        if (starterWeaponPanel != null)
            starterWeaponPanel.SetActive(true);
    }

    // TutorialTest 버튼
    public void OpenTutorialMenu()
    {
        if (tutorialBackground != null)
            tutorialBackground.SetActive(true);

        if (tutorialMenuPanel != null)
            tutorialMenuPanel.SetActive(true);

        HideAllTutorials();

        currentTutorial = null;
        currentIndex = 0;
        isTutorialActive = false;
    }

    // X 버튼
    public void CloseTutorialMenu()
    {
        isTutorialActive = false;

        HideAllTutorials();

        if (tutorialMenuPanel != null)
            tutorialMenuPanel.SetActive(false);

        if (tutorialBackground != null)
            tutorialBackground.SetActive(false);

        currentTutorial = null;
        currentIndex = 0;

        OpenStarterWeaponPanel();
    }

    // 왼쪽 메뉴 버튼들
    public void StartTutorial(GameObject tutorial)
    {
        if (tutorial == null)
            return;

        HideAllTutorials();

        currentTutorial = tutorial;
        currentIndex = 0;
        isTutorialActive = true;

        currentTutorial.SetActive(true);

        ShowOnly(0);
    }

    void ShowOnly(int index)
    {
        if (currentTutorial == null)
            return;

        for (int i = 0; i < currentTutorial.transform.childCount; i++)
        {
            currentTutorial.transform.GetChild(i)
                .gameObject.SetActive(i == index);
        }
    }

    // 오른쪽 내용영역 투명 버튼에서 호출
    public void NextPanel()
    {
        if (!isTutorialActive)
            return;

        if (currentTutorial == null)
            return;

        currentTutorial.transform.GetChild(currentIndex)
            .gameObject.SetActive(false);

        currentIndex++;

        if (currentIndex >= currentTutorial.transform.childCount)
        {
            EndTutorial();
            return;
        }

        currentTutorial.transform.GetChild(currentIndex)
            .gameObject.SetActive(true);
    }

    void EndTutorial()
    {
        isTutorialActive = false;

        if (currentTutorial != null)
        {
            for (int i = 0; i < currentTutorial.transform.childCount; i++)
            {
                currentTutorial.transform.GetChild(i)
                    .gameObject.SetActive(false);
            }

            currentTutorial.SetActive(false);
        }

        currentTutorial = null;
        currentIndex = 0;

        // if (tutorialBackground != null)
        //     tutorialBackground.SetActive(false);
        // if (tutorialMenuPanel != null)
        //     tutorialMenuPanel.SetActive(false);
        // if (starterWeaponPanel != null)
        //     starterWeaponPanel.SetActive(true);
    }

    void HideAllTutorials()
    {
        if (tutorialRoot == null)
            return;

        for (int i = 0; i < tutorialRoot.transform.childCount; i++)
        {
            GameObject child = tutorialRoot.transform.GetChild(i).gameObject;

            // 메뉴 패널과 배경은 끄지 않음
            if (child == tutorialMenuPanel)
                continue;

            if (child == tutorialBackground)
                continue;

            child.SetActive(false);
        }
    }
}