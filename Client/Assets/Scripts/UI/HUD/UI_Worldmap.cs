using System.Collections;
using UnityEngine;

public class UI_Worldmap : UI_CanToggle
{
    [SerializeField] private Camera worldmapCamera;
    [SerializeField] private float renderInterval = 0.1f;

    private void Awake()
    {
        worldmapCamera.enabled = false;
    }

    public override void ToggleUI(bool shouldShow = false)
    {
        _isOpened = !_isOpened;
        if(shouldShow) _isOpened = true;

        if (_isOpened)
        {
            StartCoroutine(RenderIntervalRoutine());
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            StopAllCoroutines();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private IEnumerator RenderIntervalRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(renderInterval);

            if (worldmapCamera != null)
            {
                worldmapCamera.Render();
            }
        }
    }
}
