using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;


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

    [Header("구역 범위 지정")]
    [SerializeField] private List<AreaInfo> areaInfos = new List<AreaInfo>();

    [Header("인스펙터에서 넣지 않는 요소들")]
    public GameObject player = null;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Application.targetFrameRate = 60;
#if UNITY_SERVER
        Application.targetFrameRate = 30;
#endif

        ToggleBlinderUI();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            // SoundManager.Instance?.PlaySFX3D("test", player.transform.position + Vector3.forward * 5f);
            // SoundManager.Instance?.PlayBGM("test");
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
        uiBlinder.ToggleUI();
    }

    public void ProcessDeath(bool isStart, string killerName = "")
    {
        if(isStart)
        {
            HUD.HideUI();

            uiDeath.Init(killerName);
            uiDeath.ToggleUI(true);
        }
        else
        {
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
}
