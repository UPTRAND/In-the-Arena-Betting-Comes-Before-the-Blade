#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class UIObjectPoolingFactory
{
    private readonly Dictionary<Type, List<UI_Poolable>> m_PooledGameObjects = new Dictionary<Type, List<UI_Poolable>>();
    private readonly Dictionary<Type, int> m_PoolCountByObject = new Dictionary<Type, int>();

    /// <summary>
    /// UIManager에 등록된 UI_Root 정보를 바탕으로 UI 풀 생성
    /// </summary>
    public void Initialize(UIManager manager, IEnumerable<GameObject> poolablePrefabs)
    {
        Clear();

        if (manager == null || poolablePrefabs == null) return;

        foreach (var prefab in poolablePrefabs)
        {
            if (prefab == null) continue;

            if (prefab.TryGetComponent<UI_Poolable>(out var component))
            {
                var parentRoot = manager.GetRootFromType(component.GetParent());
                MakePool(parentRoot, component);
            }
            else
            {
                Debug.LogError($"[UIObjectPoolingFactory] 프리팹 [{prefab.name}]에 UI_Poolable 컴포넌트가 없습니다.");
            }
        }
    }

    /// <summary>
    /// 풀 내 모든 UI 오브젝트 파괴 및 트윈 정지
    /// </summary>
    public void Clear()
    {
        foreach (var pair in m_PooledGameObjects)
        {
            List<UI_Poolable> list = pair.Value;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                UI_Poolable item = list[i];
                if (item != null && item.gameObject != null)
                {
                    item.CachedTransform.DOKill();
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
        }

        m_PooledGameObjects.Clear();
        m_PoolCountByObject.Clear();
    }

    /// <summary>
    /// C# 타입 기반 UI 스폰 (타입 안정성 제공)
    /// </summary>
    public T Spawn<T>(Vector3 pos) where T : UI_Poolable
    {
        Type typeKey = typeof(T);

        if (!m_PooledGameObjects.TryGetValue(typeKey, out var list) || list == null || list.Count == 0)
        {
            Debug.LogError($"[UIObjectPoolingFactory] 등록되지 않은 UI 풀 타입입니다: {typeKey.Name}");
            return null;
        }

        int currentIndex = m_PoolCountByObject[typeKey];
        int searchCount = 0;

        // 활성화된 UI 탐색 (가득 찬 경우 순환 라운드 로빈 방식으로 가장 오래된 UI 강제 재활용)
        while (list[currentIndex] != null && list[currentIndex].gameObject.activeSelf)
        {
            currentIndex = (currentIndex + 1) % list.Count;
            if (++searchCount >= list.Count)
            {
                Debug.LogWarning($"[UIObjectPoolingFactory] {typeKey.Name} 풀 수량이 부족하여 가장 오래된 UI 오브젝트를 재활용합니다.");
                break;
            }
        }

        UI_Poolable element = list[currentIndex];
        if (element == null) return null;

        // 이미 활성화된 상태라면 강제 Despawn 후 재사용 (DOTween 킬 및 OnDespawn 안전 실행)
        if (element.gameObject.activeSelf)
        {
            element.Despawn();
        }

        element.CachedTransform.position = pos;
        element.GameObjectSetActive(true);

        try
        {
            element.Open();
            element.OnSpawn();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{typeKey.Name}] UI Spawn/Open 실행 중 예외 발생: {ex.Message}");
            Debug.LogException(ex);
        }

        m_PoolCountByObject[typeKey] = (currentIndex + 1) % list.Count;

        return element as T;
    }

    /// <summary>
    /// UI 오브젝트 수동 반환
    /// </summary>
    public void Despawn(UI_Poolable pooled)
    {
        if (pooled == null) return;
        pooled.Despawn();
    }

    /// <summary>
    /// 개별 UI 타입별 풀 생성 및 Root 자식화
    /// </summary>
    private void MakePool(UI_Root baseParent, UI_Poolable metadata)
    {
        if (metadata == null) return;

        Type typeKey = metadata.GetType();
        if (m_PooledGameObjects.ContainsKey(typeKey))
        {
            Debug.LogWarning($"[UIObjectPoolingFactory] 이미 등록된 UI 타입입니다: {typeKey.Name}");
            return;
        }

        int size = Mathf.Max(1, metadata.poolSize);
        var list = new List<UI_Poolable>(size);
        m_PooledGameObjects.Add(typeKey, list);
        m_PoolCountByObject.Add(typeKey, 0);

        Transform parentTransform = baseParent != null ? baseParent.transform : null;

        for (int i = 0; i < size; i++)
        {
            UI_Poolable instance = UnityEngine.Object.Instantiate(metadata, Vector3.zero, Quaternion.identity, parentTransform);
            instance.gameObject.SetActive(false);
            instance.SetRoot(baseParent);
            list.Add(instance);
        }
    }
}
#endif