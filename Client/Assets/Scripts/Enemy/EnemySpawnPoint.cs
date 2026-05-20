using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemySpawnPoint : NetworkBehaviour
{
    [Serializable]
    private struct EnemyPrefabData
    {
        public int enemyID;
        public NetworkObject enemyPrefabs;
    }

    [Serializable]
    private struct EnemySpawnData
    {
        public int enemyID;
        public float spawnRate;
    }

    [SerializeField] private List<EnemySpawnData> enemySpawnData = new List<EnemySpawnData>();

    [Header("데이터")]
    [SerializeField] private List<EnemyPrefabData> enemyPrefabDatas = new List<EnemyPrefabData>();
    [SerializeField] private bool isBossSpawnPoint = false;
    [SerializeField] private float defaultRespawnTime = 20f;

    private NetworkObject _enemy = null;

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;

        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if(!IsServer) return;

        float totalWeight = enemySpawnData.Sum(data => data.spawnRate);
        
        float pivot = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        EnemySpawnData selectedData = default;

        foreach (var data in enemySpawnData)
        {
            currentWeight += data.spawnRate;
            if (pivot <= currentWeight)
            {
                selectedData = data;
                break;
            }
        }

        EnemyPrefabData enemyPrefabData = enemyPrefabDatas.Find(data => data.enemyID == selectedData.enemyID);

        if(_enemy != null)
        {
            Debug.LogError("[EnemySpawnPoint] 비정상 소환");
            return;
        }

        _enemy = Instantiate(
            enemyPrefabData.enemyPrefabs,
            transform.position,
            isBossSpawnPoint? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
        );
        
        _enemy.GetComponent<Enemy>().OnDeath += OnEnemyDeath;
        _enemy.Spawn();
    }

    private void OnEnemyDeath()
    {
        if(!IsServer) return;

        _enemy.GetComponent<Enemy>().OnDeath -= OnEnemyDeath;
        _enemy = null;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        if(!IsServer) yield break;

        yield return new WaitForSeconds(isBossSpawnPoint? 120f : defaultRespawnTime + Random.Range(-5f,5f));
        SpawnEnemy();
    }
}
