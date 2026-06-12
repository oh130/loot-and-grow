using Unity.Netcode;
using UnityEngine;

public class NetworkDestroyer : NetworkBehaviour
{
	[SerializeField] private float lifetime = 2f;

	public override void OnNetworkSpawn()
	{
		// 오직 서버(호스트)에서만 타이머를 돌려 디스폰합니다.
		if (IsServer)
		{
			Invoke(nameof(DoDespawn), lifetime);
		}
	}

	private void DoDespawn()
	{
		if (NetworkObject != null && NetworkObject.IsSpawned)
		{
			NetworkObject.Despawn(); // 자동으로 파괴 및 클라이언트 동기화
		}
	}
}