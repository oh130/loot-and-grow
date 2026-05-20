using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class GatherObjectSpawnPoint : NetworkBehaviour
{
    [Serializable]
    private struct GatherObjectPrefabData
    {
        public int lootID;
        public NetworkObject gatherObjectPrefab;
    }

    [Serializable]
    private struct GatherObjectSpawnData
    {
        public int lootID;
        public float spawnRate;
    }

    [SerializeField] private List<GatherObjectSpawnData> gatherObjectSpawnData = new List<GatherObjectSpawnData>();

    [Header("데이터")]
    [SerializeField] private List<GatherObjectPrefabData> gatherObjectPrefabDatas = new List<GatherObjectPrefabData>();
    [SerializeField] private float defaultRespawnTime = 30f;
    [SerializeField] private bool isMultiPosition = false;
    [SerializeField] private Transform[] multiPositions;
    [SerializeField] private bool showSpawnChat = false;
    [SerializeField] private string spawnChatText = "";

    private NetworkObject _gatherObject = null;

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;

        StartCoroutine(RespawnRoutine());
    }

    private void SpawnGatherObject()
    {
        if(!IsServer) return;

        float totalWeight = gatherObjectSpawnData.Sum(data => data.spawnRate);
        
        float pivot = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        GatherObjectSpawnData selectedData = default;

        foreach (var data in gatherObjectSpawnData)
        {
            currentWeight += data.spawnRate;
            if (pivot <= currentWeight)
            {
                selectedData = data;
                break;
            }
        }

        GatherObjectPrefabData gatherObjectPrefabData = gatherObjectPrefabDatas.Find(data => data.lootID == selectedData.lootID);

        if(_gatherObject != null)
        {
            Debug.LogError("[GatherObjectSpawnPoint] 비정상 소환");
            return;
        }

        _gatherObject = Instantiate(
            gatherObjectPrefabData.gatherObjectPrefab,
            isMultiPosition? multiPositions[Random.Range(0,multiPositions.Length)].position : transform.position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
        );

        if(isMultiPosition && showSpawnChat)
        {
            ServerManager.Instance?.SendChatBySystem(spawnChatText);
        }
        
        _gatherObject.GetComponent<GatherObject>().OnBreak += OnBreak;
        _gatherObject.Spawn();
    }

    private void OnBreak()
    {
        if(!IsServer) return;

        _gatherObject.GetComponent<GatherObject>().OnBreak -= OnBreak;
        _gatherObject = null;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        if(!IsServer) yield break;

        yield return new WaitForSeconds(defaultRespawnTime + (isMultiPosition? Random.Range(0f, 30f) : Random.Range(-5f,5f)));
        SpawnGatherObject();
    }
}
