using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public struct PoolElement
{
    public string name;
    public GameObject prefab;
}

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [SerializeField] private List<PoolElement> poolElements = new List<PoolElement>();

    private Dictionary<string, GameObject> _prefabDict = new Dictionary<string, GameObject>();
    private Dictionary<string, List<GameObject>> _poolDict = new Dictionary<string, List<GameObject>>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        foreach (var element in poolElements)
        {
            if (string.IsNullOrEmpty(element.name) || element.prefab == null) continue;
            
            _prefabDict[element.name] = element.prefab;
            _poolDict[element.name] = new List<GameObject>();
        }
    }

    public GameObject Get(string poolName, Transform parent = null)
    {
        if (!_prefabDict.ContainsKey(poolName))
        {
            Debug.LogError($"[PoolManager] {poolName} 이름으로 등록된 프리팹이 없습니다!");
            return null;
        }

        List<GameObject> list = _poolDict[poolName];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && !list[i].activeSelf)
            {
                if (parent != null) list[i].transform.SetParent(parent);
                list[i].SetActive(true);
                return list[i];
            }
        }

        GameObject newObj = Instantiate(_prefabDict[poolName], parent != null ? parent : transform);
        newObj.name = poolName;
        list.Add(newObj);
        return newObj;
    }
}