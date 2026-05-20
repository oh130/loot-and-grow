using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UI_Intro : MonoBehaviour
{
    Vector3 startPos;
    Vector3 targetPos;

    public TMP_Text tapText;

    bool canTouch = false;
    bool isBlinking = false;

    void Start()
    {
        Time.timeScale = 1f; // 멈춤 방지

        startPos = new Vector3(25.7f, 151f, -160f);
        targetPos = new Vector3(25.7f, 151f, -120f);

        transform.position = startPos;
        transform.rotation = Quaternion.Euler(50.5f, -5.8f, -0.001f);

        // 처음엔 투명
        Color c = tapText.color;
        c.a = 0f;
        tapText.color = c;

        StartCoroutine(Move());
        StartCoroutine(ShowTapText());
    }

    void Update()
    {
        // 터치/클릭 시 씬 이동
        if (canTouch && Input.GetMouseButtonDown(0))
        {
            SceneManager.LoadScene("AuthorizeScene");
        }

        // 깜빡임 (부드럽게)
        if (isBlinking)
        {
            Color c = tapText.color;

            // sin으로 자연스럽게 밝아졌다 어두워짐
            c.a = 0.5f + Mathf.Sin(Time.time * 3f) * 0.5f;

            tapText.color = c;
        }
    }

    System.Collections.IEnumerator Move()
    {
        float t = 0;

        while (t < 1f)
        {
            t += Time.deltaTime * 0.05f; // 속도 조절
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
    }

    System.Collections.IEnumerator ShowTapText()
    {
        yield return new WaitForSeconds(2f);

        float t = 0;

        // 페이드 인
        while (t < 1f)
        {
            t += Time.deltaTime;
            Color c = tapText.color;
            c.a = Mathf.SmoothStep(0f, 1f, t);
            tapText.color = c;

            yield return null;
        }

        // 이후 깜빡임 시작
        isBlinking = true;
        canTouch = true;
    }
}