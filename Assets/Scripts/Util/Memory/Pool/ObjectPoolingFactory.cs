#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolingFactory<T> where T : Poolable
{
    private readonly Dictionary<GameObject, List<T>> m_PooledGameObjects = new Dictionary<GameObject, List<T>>();
    private readonly Dictionary<GameObject, int> m_PoolCountByObject = new Dictionary<GameObject, int>();
    private readonly Dictionary<string, GameObject> m_PrefabsByName = new Dictionary<string, GameObject>();

    /// <summary>
    /// 풀 초기화 및 프리팹 생성
    /// </summary>
    public void Initialize(Transform parent, IEnumerable<GameObject> poolablePrefabs, string defaultKey = "")
    {
        Clear();

        if (poolablePrefabs == null) return;

        foreach (var prefab in poolablePrefabs)
        {
            if (prefab == null) continue;
            MakePool(parent, prefab);
        }
    }

    /// <summary>
    /// 모든 풀링된 게임 오브젝트 파괴 및 사전 초기화
    /// </summary>
    public void Clear()
    {
        foreach (var pair in m_PooledGameObjects)
        {
            List<T> list = pair.Value;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                T obj = list[i];
                if (obj != null && obj.gameObject != null)
                {
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
        }

        m_PooledGameObjects.Clear();
        m_PoolCountByObject.Clear();
        m_PrefabsByName.Clear();
    }

    /// <summary>
    /// 프리팹 이름(Key) 기반 스폰
    /// </summary>
    public T Spawn(string key, Vector3 pos, GameObject owner = null)
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (m_PrefabsByName.TryGetValue(key, out var prefab))
        {
            return Spawn(prefab, pos, owner);
        }

        Debug.LogWarning($"[ObjectPoolingFactory] Key를 찾을 수 없습니다: {key}");
        return null;
    }

    /// <summary>
    /// 프리팹(GameObject) 기반 스폰 (링 버퍼 순환 구조)
    /// </summary>
    public T Spawn(GameObject key, Vector3 pos, GameObject owner = null)
    {
        if (key == null) return null;

        if (!m_PooledGameObjects.TryGetValue(key, out var list) || list == null || list.Count == 0)
        {
            Debug.LogWarning($"[ObjectPoolingFactory] 등록되지 않은 프리팹 키입니다: {key.name}");
            return null;
        }

        int currentIndex = m_PoolCountByObject[key];
        int searchCount = 0;

        // 비활성화된 오브젝트 탐색 (모두 사용 중이면 순환 커서 위치의 오래된 개체 강제 재활용)
        while (list[currentIndex] != null && list[currentIndex].gameObject.activeSelf)
        {
            currentIndex = (currentIndex + 1) % list.Count;
            if (++searchCount >= list.Count)
            {
                break; // 모든 풀이 사용 중인 경우 라운드 로빈에 의해 강제 재활용 진행
            }
        }

        T obj = list[currentIndex];
        if (obj == null) return null;

        // 이미 활성화된 객체라면 강제 Despawn 후 재사용 (DOTween 킬 및 OnDespawn 안전 실행)
        if (obj.gameObject.activeSelf)
        {
            obj.Despawn();
        }

        // 트랜스폼 위치 설정 및 활성화 (ObjectPoolable의 CachedTransform 사용)
        obj.CachedTransform.position = pos;
        obj.GameObjectSetActive(true);

        try
        {
            obj.OnSpawn();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{obj.name}] OnSpawn 실행 중 예외 발생: {ex.Message}");
            Debug.LogException(ex);
        }

        // 다음 스폰을 위해 순환 인스턴스 커서 이동
        m_PoolCountByObject[key] = (currentIndex + 1) % list.Count;

        return obj;
    }

    /// <summary>
    /// 풀 오브젝트 수동 반환 래퍼
    /// </summary>
    public void Despawn(T pooled)
    {
        if (pooled == null) return;
        pooled.Despawn();
    }

    /// <summary>
    /// 개별 프리팹 풀 생성
    /// </summary>
    private void MakePool(Transform parent, GameObject prefab)
    {
        if (m_PooledGameObjects.ContainsKey(prefab))
        {
            Debug.LogWarning($"[ObjectPoolingFactory] 이미 등록된 프리팹입니다: {prefab.name}");
            return;
        }

        if (!prefab.TryGetComponent<T>(out var sampleComponent))
        {
            Debug.LogError($"[ObjectPoolingFactory] 프리팹 [{prefab.name}]에 {typeof(T).Name} 컴포넌트가 없습니다.");
            return;
        }

        if (!m_PrefabsByName.ContainsKey(prefab.name))
        {
            m_PrefabsByName.Add(prefab.name, prefab);
        }

        int size = Mathf.Max(1, sampleComponent.poolSize);
        var list = new List<T>(size);
        m_PooledGameObjects.Add(prefab, list);
        m_PoolCountByObject.Add(prefab, 0);

        for (int i = 0; i < size; i++)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
            instance.SetActive(false);

            if (instance.TryGetComponent<T>(out var poolable))
            {
                list.Add(poolable);
            }
        }
    }
}
#endif