using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SoundData
{
    public string name;
    public AudioClip clip;
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfx2DSource;

    [Header("Sound List")]
    [SerializeField] private List<SoundData> bgmList = new List<SoundData>();
    [SerializeField] private List<SoundData> sfxList = new List<SoundData>();

    private Dictionary<string, AudioClip> _bgmDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> _sfxDict = new Dictionary<string, AudioClip>();

    private float _masterVolume;
    private float _bgmVolume;
    private float _sfxVolume;

    private const string MASTER_KEY = "Vol_Master";
    private const string BGM_KEY = "Vol_BGM";
    private const string SFX_KEY = "Vol_SFX";

    private const float DEFAULT_MASTER_VOL = 1.0f;
    private const float DEFAULT_BGM_VOL = 0.5f;
    private const float DEFAULT_SFX_VOL = 0.8f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitDictionary(bgmList, _bgmDict);
        InitDictionary(sfxList, _sfxDict);

        LoadVolumeSettings();
    }

    private void InitDictionary(List<SoundData> list, Dictionary<string, AudioClip> dict)
    {
        foreach (var data in list)
        {
            if (data.clip != null && !dict.ContainsKey(data.name))
                dict.Add(data.name, data.clip);
        }
    }

    private void LoadVolumeSettings()
    {
        _masterVolume = PlayerPrefs.GetFloat(MASTER_KEY, DEFAULT_MASTER_VOL);
        _bgmVolume = PlayerPrefs.GetFloat(BGM_KEY, DEFAULT_BGM_VOL);
        _sfxVolume = PlayerPrefs.GetFloat(SFX_KEY, DEFAULT_SFX_VOL);

        ApplyVolume();
    }

    private void ApplyVolume()
    {
        bgmSource.volume = _bgmVolume * _masterVolume;
        sfx2DSource.volume = _sfxVolume * _masterVolume;
    }

    public void SetMasterVolume(float vol)
    {
        _masterVolume = Mathf.Clamp01(vol);
        PlayerPrefs.SetFloat(MASTER_KEY, _masterVolume);
        ApplyVolume();
    }

    public void SetBGMVolume(float vol)
    {
        _bgmVolume = Mathf.Clamp01(vol);
        PlayerPrefs.SetFloat(BGM_KEY, _bgmVolume);
        ApplyVolume();
    }

    public void SetSFXVolume(float vol)
    {
        _sfxVolume = Mathf.Clamp01(vol);
        PlayerPrefs.SetFloat(SFX_KEY, _sfxVolume);
        ApplyVolume();
    }

    public void SaveSettings() => PlayerPrefs.Save();

    public void PlayBGM(string fileName)
    {
        if (_bgmDict.TryGetValue(fileName, out AudioClip clip))
        {
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    public void StopBGM() => bgmSource.Stop();

    public void PlaySFX2D(string fileName, float factor = 1f)
    {
        if (_sfxDict.TryGetValue(fileName, out AudioClip clip))
        {
            sfx2DSource.PlayOneShot(clip, factor * _sfxVolume * _masterVolume);
        }
    }

    public void PlaySFX3D(string fileName, Vector3 position, float factor = 1f)
    {
        if (_sfxDict.TryGetValue(fileName, out AudioClip clip))
        {
            GameObject go = PoolManager.Instance?.Get("AudioSource_3D");
            if (go == null) return;

            go.transform.position = position;
            AudioSource source = go.GetComponent<AudioSource>();

            source.clip = clip;
            source.spatialBlend = 1f;
            source.volume = factor * _sfxVolume * _masterVolume;
            source.Play();
        }
    }
}