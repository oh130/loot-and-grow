using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GraphicsManager : MonoBehaviour
{
    public static GraphicsManager Instance { get; private set; }

    [Header("Far Clip Plane")]
    [SerializeField] private float farClip = 600f;

    private float _originalRenderScale;
    private float _originalShadowDistance;
    private int   _originalShadowCascades;
    private int   _originalMsaa;
    private int   _originalMipmapLimit;
    private float _originalLodBias;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
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
        if (Instance != this) return;
        var urp = GetURP();
        if (urp != null)
        {
            urp.renderScale        = _originalRenderScale;
            urp.shadowDistance     = _originalShadowDistance;
            urp.shadowCascadeCount = Mathf.Max(1, _originalShadowCascades);
            urp.msaaSampleCount    = _originalMsaa;
        }
        QualitySettings.globalTextureMipmapLimit = _originalMipmapLimit;
        QualitySettings.lodBias                  = _originalLodBias;
    }

    private void Start()
    {
        ApplyQuality(PlayerPrefs.GetInt("GraphicsQuality", 1));
        PlayerPrefs.DeleteKey("FpsCap");
        ApplyFpsCap(60);
    }

    public void ApplyFpsCap(int fps)
    {
        PlayerPrefs.SetInt("FpsCap", fps);
        Application.targetFrameRate = fps;
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
            case 0: // 낮음
                urp.renderScale        = 0.45f;
                urp.shadowDistance     = 0f;
                urp.shadowCascadeCount = 1;
                urp.msaaSampleCount    = 1;
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.lodBias                  = 0.4f;
                break;

            case 1: // 보통
                urp.renderScale        = 0.65f;
                urp.shadowDistance     = 20f;
                urp.shadowCascadeCount = 1;
                urp.msaaSampleCount    = 1;
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.lodBias                  = 0.7f;
                break;

            case 2: // 높음
                urp.renderScale        = 0.85f;
                urp.shadowDistance     = 70f;
                urp.shadowCascadeCount = 2;
                urp.msaaSampleCount    = 2;
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.lodBias                  = 1.2f;
                break;

            default: // 3 = 울트라
                urp.renderScale        = 1.0f;
                urp.shadowDistance     = 150f;
                urp.shadowCascadeCount = 4;
                urp.msaaSampleCount    = 4;
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.lodBias                  = 1.5f;
                break;
        }

        SetFarClip(farClip);
    }

    private void SetFarClip(float distance)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.farClipPlane = distance;
            cam.layerCullDistances = new float[32]; // 레이어별 컬링 거리 전부 초기화 (0 = farClipPlane 따름)
        }

        // Cinemachine이 매 프레임 Camera.main.farClipPlane을 Lens값으로 덮어쓰므로 같이 변경
        if (GameManager.Instance?.cineCam != null)
        {
            var lens = GameManager.Instance.cineCam.Lens;
            lens.FarClipPlane = distance;
            GameManager.Instance.cineCam.Lens = lens;
        }
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
        ApplyQuality(PlayerPrefs.GetInt("GraphicsQuality", 1));
    }
}
