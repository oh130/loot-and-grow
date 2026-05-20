using DG.Tweening;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UI_SelectType : MonoBehaviour
{
    [SerializeField] private Text notifyText;
    [SerializeField] private CinemachineCamera cineCam;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            if(notifyText)
            {
                NetworkManager.Singleton.OnServerStarted += () => {
                    notifyText.text = "서버가 정상적으로 열렸습니다.";

                    DOVirtual.DelayedCall(2f, () => {
                        notifyText.text = "";
                    });
                };

                NetworkManager.Singleton.OnClientStarted += () => {
                    notifyText.text = "클라이언트가 정상적으로 열렸습니다.";

                    DOVirtual.DelayedCall(2f, () => {
                        notifyText.text = "";
                    });
                };
            }
        }
    }

    public void OnClick(int idx)
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if(idx == 0) StartCoroutine(ServerManager.Instance.StartServer());
            else if(idx == 1) NetworkManager.Singleton.StartClient();
        }
    }
}
