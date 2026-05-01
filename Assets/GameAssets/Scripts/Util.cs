using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Transform = UnityEngine.Transform;

public static class Util
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
        if (go == null)
            return null;

        if (recursive == false)
        {
            // 직접 자식만 검색
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform child = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || child.name == name)
                {
                    return child.gameObject;
                }
            }
        }
        else
        {
            // 재귀적으로 검색하되, 자기 자신은 제외
            return FindChildRecursive(go.transform, name);
        }

        return null;
    }

    private static GameObject FindChildRecursive(Transform parent, string name)
    {
        // 자식들을 검색
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.IsNullOrEmpty(name) || child.name == name)
            {
                return child.gameObject;
            }
            
            // 재귀적으로 자식의 자식 검색
            GameObject found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        
        return null;
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

    public static Transform FindChildByName(Transform transform, string childName)
    {
        foreach (Transform child in transform)
        {
            if (child.name == childName)
            {
                return child;
            }
        }
        return null;
    }

    public static Vector2 RandomPointInAnnulus(Vector2 origin, float minRadius = 6, float maxRadius = 12)
    {
        float randomDist = UnityEngine.Random.Range(minRadius, maxRadius);

        Vector2 randomDir = new Vector2(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)).normalized;
        //Debug.Log(randomDir);
        var point = origin + randomDir * randomDist;
        return point;
    }

    public static Color HexToColor(string color)
    {
        Color parsedColor;
        ColorUtility.TryParseHtmlString("#" + color, out parsedColor);

        return parsedColor;
    }

    //string값 으로 Enum값 찾기
    public static T ParseEnum<T>(string value)
    {
        return (T)Enum.Parse(typeof(T), value, true);
    }

    public static Vector3 ScreenToWorldCood(Vector3 input)
    {
        int width = Screen.width;
        int height = Screen.height;

        return new Vector3(input.x - width / 2, input.y - height / 2, input.z);
    }

    public static Vector3 WorldToScreenCood(Vector3 input)
    {
        int width = Screen.width;
        int height = Screen.height;

        return new Vector3(input.x + width / 2, input.y + height / 2, input.z);
    }

    public static Color DamagedColor()
    {
        return new Color((float)190 / 255, (float)38 / 255, (float)51 / 255);
    }

    public static Color DefenceColor()
    {
        return new Color((float)0, (float)140 / 255, (float)255 / 255);
    }
}
