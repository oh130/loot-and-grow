using UnityEngine;

public class UI_Minimap : MonoBehaviour
{
    [SerializeField] private UI_CanToggle uiWorldmap;

    public void OnClick()
    {
        if(uiWorldmap.IsOpened) SoundManager.Instance.PlaySFX2D("MenuClose");
        else SoundManager.Instance.PlaySFX2D("MenuOpen");
        uiWorldmap.ToggleUI();
    }
}
