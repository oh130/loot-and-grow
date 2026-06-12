using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class AIPlayerSpawnPoint : NetworkBehaviour
{
    [Serializable]
    private struct EquipData
    {
        public int dataID;
        public int[] equipIDs;
        public int[] weaponIDs;
    }

    [Serializable]
    private struct AIPlayerSpawnData
    {
        public int dataID;
        public float spawnRate;
    }

    [SerializeField] private List<AIPlayerSpawnData> aiPlayerSpawnData = new List<AIPlayerSpawnData>();

    [Header("데이터")]
    [SerializeField] private NetworkObject aiPlayerPrefab;
    [SerializeField] private List<EquipData> equipDatas = new List<EquipData>();
    [SerializeField] private float defaultRespawnTime = 20f;

    private NetworkObject _aiPlayer = null;

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;

        Spawn();
    }

    private void Spawn()
    {
        if(!IsServer) return;

        float totalWeight = aiPlayerSpawnData.Sum(data => data.spawnRate);
        
        float pivot = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        AIPlayerSpawnData selectedData = default;

        foreach (var data in aiPlayerSpawnData)
        {
            currentWeight += data.spawnRate;
            if (pivot <= currentWeight)
            {
                selectedData = data;
                break;
            }
        }

        EquipData equipData = equipDatas.Find(data => data.dataID == selectedData.dataID);

        if(_aiPlayer != null)
        {
            Debug.LogError("[AIPlayerSpawnPoint] 비정상 소환");
            return;
        }

        _aiPlayer = Instantiate(
            aiPlayerPrefab,
            transform.position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
        );

        
        List<int> equips = new List<int>(equipData.equipIDs);
        int targetCount = (int)Math.Ceiling(equips.Count * Random.Range(0.25f,1f));
        List<int> selectedItems = equips.OrderBy(item => Guid.NewGuid()).Take(targetCount).ToList();
        int weaponIdx = Random.Range(0, equipData.weaponIDs.Length+1);
        if(weaponIdx != equipData.weaponIDs.Length)
            selectedItems.Add(equipData.weaponIDs[weaponIdx]);

        _aiPlayer.GetComponent<AIPlayer>().equippedItemIDs = selectedItems.ToArray();
        
        _aiPlayer.GetComponent<AIPlayer>().OnDeath += OnDeath;
        _aiPlayer.Spawn();
    }

    private void OnDeath()
    {
        if(!IsServer) return;

        _aiPlayer.GetComponent<AIPlayer>().OnDeath -= OnDeath;
        _aiPlayer = null;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        if(!IsServer) yield break;

        yield return new WaitForSeconds(defaultRespawnTime + Random.Range(-5f,5f));
        Spawn();
    }
}
