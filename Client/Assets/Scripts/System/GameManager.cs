using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Camera")]
    public CinemachineCamera cineCam;
    [SerializeField] CinemachineCamera minimapCineCam;

    [Header("World Space UI Canvas")]
    public GameObject worldUI;

    [Header("UI")]
    public UI_HUD HUD;
    [SerializeField] private UI_Admin uiAdmin;
    [SerializeField] private UI_Blinder uiBlinder;
    [SerializeField] private UI_Death uiDeath;
    [SerializeField] private UI_ExitConfirmPopup uiExit;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private TMP_Text pvpText;

    [Header("구역 범위 지정")]
    [SerializeField] private List<AreaInfo> areaInfos = new List<AreaInfo>();

    [Header("인스펙터에서 넣지 않는 요소들")]
    public GameObject player = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // DDOL에 남아있던 스테일 GameManager 컴포넌트만 교체 (gameObject는 유지)
            Destroy(Instance);
        }
        Instance = this;
#if UNITY_SERVER
        Application.targetFrameRate = 30;
#endif

        float aspectRatio = (float)Screen.height / Screen.width;

        if (aspectRatio >= 0.8f)
        {
            canvasScaler.matchWidthOrHeight = 0.65f; 
        }

        SoundManager.Instance?.StopBGM();
        SoundManager.Instance?.PlayBGM("InGame");

        GraphicsManager.Instance?.RefreshCamera();
        ToggleBlinderUI();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            uiExit?.ToggleUI();
        }
    }

    public void RegisterPlayerObject(GameObject gameObject)
    {
        player = gameObject;
        minimapCineCam.Target.TrackingTarget = player.transform;
        HUD.LinkPlayer(player);
    }

    public void ToggleAdminUI()
    {
        uiAdmin.ToggleUI();
    }

    public void ToggleBlinderUI()
    {
        uiBlinder?.ToggleUI();
    }

    public void ProcessDeath(bool isStart, string killerName = "")
    {
        if(isStart)
        {
            SoundManager.Instance?.StopBGM();
            SoundManager.Instance?.PlayBGM("Die");

            HUD.HideUI();

            uiDeath.Init(killerName);
            uiDeath.ToggleUI(true);
        }
        else
        {
            SoundManager.Instance?.StopBGM();
            SoundManager.Instance?.PlayBGM("InGame");

            HUD.ShowUI();
            uiDeath.ToggleUI();
        }
        InventoryManager.Instance?.Refresh();
    }

    public void GetAreaAtPosition(Vector3 pos, out AreaType areaType)
    {
        foreach (var area in areaInfos)
        {
            if(area.areaCollider.bounds.Contains(pos))
            {
                areaType = area.areaType;
                return;
            }
        }
        areaType = AreaType.PVP;
    }

    public void OnAreaChanged(AreaType newArea)
    {
        if(newArea == AreaType.PVP) pvpText.text = "<color=red>전투 가능 구역</color>";
        else if(newArea == AreaType.PVE || newArea == AreaType.Gather) pvpText.text = "<color=orange>사냥 가능 구역</color>";
        else pvpText.text = "<color=green>안전 구역</color>";
    }
}
