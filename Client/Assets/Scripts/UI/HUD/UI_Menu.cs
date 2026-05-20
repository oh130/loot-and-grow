using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UI_Menu : MonoBehaviour
{
    public static UI_Menu Instance { get; private set; }

    [Header("전체 메뉴 (모든 UI 부모)")]
    [SerializeField] private CanvasGroup menuGroup;

    [Header("패널")]
    [SerializeField] private CanvasGroup inventoryGroup;
    [SerializeField] private CanvasGroup systemGroup;
    [SerializeField] private GameObject  equipmentPanel;

    [Header("메뉴 버튼 (상점/창고 시 탭 버튼만 숨김)")]
    [SerializeField] private GameObject  inventoryTabButton;  // "인벤토리" 탭 버튼
    [SerializeField] private GameObject  systemTabButton;     // "설정" 탭 버튼

    private ShopPanel    _activeShopPanel;
    private StoragePanel _activeStoragePanel;

    public bool IsInventoryOpen => menuGroup != null && menuGroup.alpha > 0f
                                && inventoryGroup != null && inventoryGroup.alpha > 0f;

    [Header("카메라 설정")]
    public PlayerRelativeCam cam;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sensitivitySlider;
    public TMP_Dropdown graphicsDropdown;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetGroup(menuGroup, false);
        SetGroup(inventoryGroup, false);
        SetGroup(systemGroup, false);
        equipmentPanel?.SetActive(true);

        float multiplier = PlayerPrefs.GetFloat("Sensitivity", 1.0f);
        multiplier = Mathf.Clamp(multiplier, 0.1f, 1.5f);

        if (cam != null)
            cam.SetMouseSensitivityMultiplier(multiplier);

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = multiplier;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        if (graphicsDropdown != null)
        {
            graphicsDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 2);
            graphicsDropdown.onValueChanged.AddListener(OnGraphicsQualityChanged);
        }

        float bgmVol = PlayerPrefs.GetFloat("Vol_BGM", 0.5f);
        float sfxVol = PlayerPrefs.GetFloat("Vol_SFX", 0.8f);

        if (bgmSlider != null)
        {
            bgmSlider.value = bgmVol;
            SoundManager.Instance.SetBGMVolume(bgmVol);
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = sfxVol;
            SoundManager.Instance.SetBGMVolume(sfxVol);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    public void OpenMenu()
    {
        if (menuGroup == null) return;

        SoundManager.Instance.PlaySFX2D("MenuOpen");

        SetGroup(menuGroup, true);
        ShowInventory(); // 기본 탭
    }

    public void CloseMenu()
    {
        if (menuGroup == null) return;

        SoundManager.Instance.PlaySFX2D("MenuClose");

        ShopManager.Instance?.Close();
        StoragePanel.Instance?.ForceClose();
        _activeStoragePanel?.Close();
        _activeStoragePanel?.gameObject.SetActive(false);
        _activeStoragePanel = null;
        equipmentPanel?.SetActive(true);
        inventoryTabButton?.SetActive(true);
        systemTabButton?.SetActive(true);
        SetGroup(menuGroup, false);

        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.ClearSelection();
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearInventorySelection();
            if (InventoryManager.Instance.EquipmentPanel != null)
                InventoryManager.Instance.EquipmentPanel.ClearSelection();
            if (InventoryManager.Instance.ItemInfoPanel != null)
                InventoryManager.Instance.ItemInfoPanel.ShowEmpty();
        }
    }

    public void ShowInventory()
    {
        equipmentPanel?.SetActive(true);
        _activeShopPanel?.gameObject.SetActive(false);
        _activeShopPanel = null;
        _activeStoragePanel?.gameObject.SetActive(false);
        _activeStoragePanel = null;
        inventoryTabButton?.SetActive(true);
        systemTabButton?.SetActive(true);
        SetGroup(inventoryGroup, true);
        SetGroup(systemGroup, false);
        InventoryManager.Instance.Refresh();

        SoundManager.Instance.PlaySFX2D("MenuClose");
    }

    // ─── 상점 열기/닫기 ──────────────────────────────────────────────────

    // ShopPanel.Open()에서 호출. 어느 NPC 상점이든 해당 panel을 넘긴다.
    public void OpenShop(ShopPanel panel)
    {
        _activeShopPanel?.gameObject.SetActive(false);
        _activeShopPanel = panel;

        equipmentPanel?.SetActive(false);
        _activeShopPanel.gameObject.SetActive(true);
        inventoryTabButton?.SetActive(false);
        systemTabButton?.SetActive(false);
        SetGroup(menuGroup, true);
        SetGroup(inventoryGroup, true);
        SetGroup(systemGroup, false);
        InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();

        SoundManager.Instance.PlaySFX2D("MenuOpen");
    }

    public void CloseShop()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.ForceClose();
        _activeShopPanel?.Close();
        _activeShopPanel?.gameObject.SetActive(false);
        _activeShopPanel = null;
        equipmentPanel?.SetActive(true);
        inventoryTabButton?.SetActive(true);
        systemTabButton?.SetActive(true);
        SetGroup(menuGroup, false);
        InventoryManager.Instance?.ClearInventorySelection();
        InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();

        SoundManager.Instance.PlaySFX2D("MenuClose");
    }

    // ─── 창고 열기/닫기 ──────────────────────────────────────────────────

    public void OpenStorage(StoragePanel panel)
    {
        _activeShopPanel?.gameObject.SetActive(false);
        _activeStoragePanel = panel;

        equipmentPanel?.SetActive(false);
        panel.gameObject.SetActive(true);
        inventoryTabButton?.SetActive(false);
        systemTabButton?.SetActive(false);
        SetGroup(menuGroup, true);
        SetGroup(inventoryGroup, true);
        SetGroup(systemGroup, false);
        InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();

        SoundManager.Instance.PlaySFX2D("MenuOpen");
    }

    public void CloseStorage()
    {
        StoragePanel.Instance?.ForceClose();
        _activeStoragePanel?.Close();
        _activeStoragePanel?.gameObject.SetActive(false);
        _activeStoragePanel = null;
        equipmentPanel?.SetActive(true);
        inventoryTabButton?.SetActive(true);
        systemTabButton?.SetActive(true);
        SetGroup(menuGroup, false);
        InventoryManager.Instance?.ClearInventorySelection();
        InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();

        SoundManager.Instance.PlaySFX2D("MenuClose");
    }

    // ─── 시스템 탭 ───────────────────────────────────────────────────────

    public void ShowSystem()
    {
        SetGroup(inventoryGroup, false);
        SetGroup(systemGroup, true);

        float multiplier = PlayerPrefs.GetFloat("Sensitivity", 1.0f);
        if (sensitivitySlider != null)
            sensitivitySlider.value = multiplier;
        
        SoundManager.Instance.PlaySFX2D("MenuClose");
    }

    public void OnBGMVolumeChanged(float value)
    {
        SoundManager.Instance.SetBGMVolume(value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        SoundManager.Instance.SetSFXVolume(value);
    }

    public void OnGraphicsQualityChanged(int value)
    {
        GraphicsManager.Instance?.ApplyQuality(value);
    }

    public void OnSensitivityChanged(float value)
    {
        if (cam != null)
            cam.SetMouseSensitivityMultiplier(value);

        PlayerPrefs.SetFloat("Sensitivity", value);
    }

    public void QuitGame()
    {
        NetworkManager.Singleton?.Shutdown();
        Application.Quit();

#if UNITY_EDITOR
        NetworkManager.Singleton?.Shutdown();
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void SetGroup(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha          = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable   = visible;
    }
}
