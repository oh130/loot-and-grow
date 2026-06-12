using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UI_Intro : MonoBehaviour
{
    Vector3 startPos;
    Vector3 targetPos;

    public TMP_Text tapText;
    [SerializeField] SoundManager soundManager;
    public TMP_Text versionText;

    bool canTouch = false;
    bool isBlinking = false;

    void Start()
    {
        Time.timeScale = 1f; // ���� ����
        soundManager.PlayBGM("Title");

        startPos = new Vector3(25.7f, 151f, -160f);
        targetPos = new Vector3(25.7f, 151f, -120f);

        transform.position = startPos;
        transform.rotation = Quaternion.Euler(50.5f, -5.8f, -0.001f);

        // ó���� ����
        Color c = tapText.color;
        c.a = 0f;
        tapText.color = c;

        versionText.text = "Version " + Application.version;

        StartCoroutine(Move());
        StartCoroutine(ShowTapText());
    }

    void Update()
    {
        // ��ġ/Ŭ�� �� �� �̵�
        if (canTouch && Input.GetMouseButtonDown(0))
        {
            soundManager.PlaySFX2D("ItemSelect");
            SceneManager.LoadScene("AuthorizeScene");
        }

        // ������ (�ε巴��)
        if (isBlinking)
        {
            Color c = tapText.color;

            // sin���� �ڿ������� ������� ��ο���
            c.a = 0.5f + Mathf.Sin(Time.time * 3f) * 0.5f;

            tapText.color = c;
        }
    }

    System.Collections.IEnumerator Move()
    {
        float t = 0;

        while (t < 1f)
        {
            t += Time.deltaTime * 0.05f; // �ӵ� ����
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
    }

    System.Collections.IEnumerator ShowTapText()
    {
        yield return new WaitForSeconds(2f);

        float t = 0;

        // ���̵� ��
        while (t < 1f)
        {
            t += Time.deltaTime;
            Color c = tapText.color;
            c.a = Mathf.SmoothStep(0f, 1f, t);
            tapText.color = c;

            yield return null;
        }

        // ���� ������ ����
        isBlinking = true;
        canTouch = true;
    }
}