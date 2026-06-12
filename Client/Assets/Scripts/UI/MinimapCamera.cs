using UnityEngine;
using System.Collections;

public class MinimapCamera : MonoBehaviour
{
    private Camera minimapCamera;
    [SerializeField] private float renderInterval = 0.1f;

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();
        minimapCamera.enabled = false;
    }

    private void OnEnable()
    {
        #if UNITY_SERVER
        return;
        #endif
        StartCoroutine(RenderIntervalRoutine());
    }

    private void OnDisable()
    {
        #if UNITY_SERVER
        return;
        #endif
        StopAllCoroutines();
    }

    private IEnumerator RenderIntervalRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(renderInterval);

            if (minimapCamera != null)
            {
                minimapCamera.Render();
            }
        }
    }
}