using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class BotManager : MonoBehaviour
{
    [SerializeField] private LoginManager loginManager;

    public void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void Start()
    {
        StartCoroutine(BotRoutine());
    }

    private IEnumerator BotRoutine()
    {
        Debug.Log("[BotManager] Waiting for login...");
        yield return new WaitForSecondsRealtime(Random.Range(0f, 5f));
        string id = "b" + GetBotNumberFromArgs().ToString();
        string pw = "1";
        Debug.Log($"[BotManager] Login start / ID : {id}");
        loginManager.BotLogin(id, pw);
    }

    private int GetBotNumberFromArgs()
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--bot-id" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int result))
                {
                    return result;
                }
            }
        }

        return Random.Range(100, 999); 
    }
}
