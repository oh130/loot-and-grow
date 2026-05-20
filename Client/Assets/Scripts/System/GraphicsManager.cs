using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GraphicsManager : MonoBehaviour
{
    public static GraphicsManager Instance { get; private set; }

    [Header("Far Clip Plane (품질별)")]
    [SerializeField] private float farClipHigh = 150f;
    [SerializeField] private float farClipMid  = 100f;
    [SerializeField] private float farClipLow  =  60f;

    private float _originalRenderScale;
    private float _originalShadowDistance;
    private int   _originalShadowCascades;
    private int   _originalMsaa;
    private int   _originalMipmapLimit;
    private float _originalLodBias;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        var urp = GetURP();
        if (urp != null)
        {
            _originalRenderScale   = urp.renderScale;
            _originalShadowDistance = urp.shadowDistance;
            _originalShadowCascades = urp.shadowCascadeCount;
            _originalMsaa          = urp.msaaSampleCount;
        }
        _originalMipmapLimit = QualitySettings.globalTextureMipmapLimit;
        _originalLodBias     = QualitySettings.lodBias;
    }

    private void OnDestroy()
    {
        var urp = GetURP();
        if (urp != null)
        {
            urp.renderScale      = _originalRenderScale;
            urp.shadowDistance   = _originalShadowDistance;
            urp.shadowCascadeCount = _originalShadowCascades;
            urp.msaaSampleCount  = _originalMsaa;
        }
        QualitySettings.globalTextureMipmapLimit = _originalMipmapLimit;
        QualitySettings.lodBias                  = _originalLodBias;
    }

    private void Start()
    {
        ApplyQuality(PlayerPrefs.GetInt("GraphicsQuality", 2));
    }

    public void ApplyQuality(int level)
    {
        PlayerPrefs.SetInt("GraphicsQuality", level);

        var urp = GetURP();
        if (urp == null)
        {
            Debug.LogWarning("[GraphicsManager] URP Asset을 찾을 수 없습니다.");
            return;
        }

        switch (level)
        {
            case 0: // 하 — 확실히 흐릿하고 그림자 없음
                urp.renderScale        = 0.45f;
                urp.shadowDistance     = 0f;
                urp.shadowCascadeCount = 1;
                urp.msaaSampleCount    = 1;
                QualitySettings.globalTextureMipmapLimit = 2;
                QualitySettings.lodBias                  = 0.3f;
                SetFarClip(farClipLow);
                break;

            case 1: // 중
                urp.renderScale        = 0.75f;
                urp.shadowDistance     = 40f;
                urp.shadowCascadeCount = 2;
                urp.msaaSampleCount    = 2;
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.lodBias                  = 0.7f;
                SetFarClip(farClipMid);
                break;

            default: // 2 = 상 — 선명하고 그림자 풍부
                urp.renderScale        = 1.0f;
                urp.shadowDistance     = 150f;
                urp.shadowCascadeCount = 4;
                urp.msaaSampleCount    = 4;
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.lodBias                  = 1.5f;
                SetFarClip(farClipHigh);
                break;
        }
    }

    private void SetFarClip(float distance)
    {
        Camera cam = Camera.main;
        if (cam != null) cam.farClipPlane = distance;
    }

    private static UniversalRenderPipelineAsset GetURP()
    {
        var pipeline = QualitySettings.renderPipeline
                    ?? GraphicsSettings.defaultRenderPipeline
                    ?? GraphicsSettings.currentRenderPipeline;
        return pipeline as UniversalRenderPipelineAsset;
    }

    public void RefreshCamera()
    {
        ApplyQuality(PlayerPrefs.GetInt("GraphicsQuality", 2));
    }
}
