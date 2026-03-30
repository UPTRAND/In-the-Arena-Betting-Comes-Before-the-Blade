using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Util
{
    public static T GetOrAddComponent<T>(GameObject go) where T : UnityEngine.Component
    {
        T component = go.GetComponent<T>();
		if (component == null)
            component = go.AddComponent<T>();
        return component;
	}

    public static GameObject FindChild(GameObject go, string name = null, bool recursive = false)
    {
        Transform transform = FindChild<Transform>(go, name, recursive);
        if (transform == null)
            return null;

        return transform.gameObject;
    }

    public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : UnityEngine.Object
    {
        if (go == null)
            return null;

        if (recursive == false)
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform transform = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || transform.name == name)
                {
                    T component = transform.GetComponent<T>();
                    if (component != null)
                        return component;
                }
            }
		}
        else
        {
            foreach (T component in go.GetComponentsInChildren<T>())
            {
                if (string.IsNullOrEmpty(name) || component.name == name)
                    return component;
            }
        }

        return null;
    }

    // int 리스트를 셔플시킴
    public static List<int> SuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randNum = Random.Range(0, list.Count);
            int temp = list[i];
            list[i] = list[randNum];
            list[randNum] = temp;
        }

        return list;
    }

    // int 배열을 셔플시킴
    public static int[] SuffleArray(int[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            int randNum = Random.Range(0, array.Length);
            int temp = array[i];
            array[i] = array[randNum];
            array[randNum] = temp;
        }

        return array;
    }
}
