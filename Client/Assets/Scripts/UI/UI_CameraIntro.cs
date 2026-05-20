using System.Collections;
using UnityEngine;

public class UI_CameraIntro : MonoBehaviour
{
    [Header("시작 위치 (타이틀 카메라 위치)")]
    public Transform startPoint;

    [Header("플레이어 카메라 (목표 위치)")]
    public Transform playerCamera;

    [Header("이동 시간")]
    public float duration = 3f;

    [Header("플레이어 컨트롤 (선택)")]
    public MonoBehaviour playerController;

    private bool isPlaying = false;

    void Start()
    {
        transform.position = startPoint.position;
        transform.rotation = startPoint.rotation;

        if (playerController != null)
            playerController.enabled = false;

        StartIntro();
    }

    public void StartIntro()
    {
        if (!isPlaying)
            StartCoroutine(CameraMove());
    }

    IEnumerator CameraMove()
    {
        isPlaying = true;

        float time = 0f;

        Vector3 startPos = startPoint.position;
        Quaternion startRot = startPoint.rotation;

        while (time < duration)
        {
            float t = time / duration;

            t = Mathf.SmoothStep(0, 1, t);

            Vector3 endPos = playerCamera.position;
            Quaternion endRot = playerCamera.rotation;

            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);

            time += Time.deltaTime;
            yield return null;
        }

        transform.position = playerCamera.position;
        transform.rotation = playerCamera.rotation;

        if (playerController != null)
            playerController.enabled = true;
    }
}