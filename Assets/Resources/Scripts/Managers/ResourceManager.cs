using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
    private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public GameObject LoadPrefab(string path)
    {
        if (_prefabCache.TryGetValue(path, out var prefab))
        {
            return prefab;
        }

        prefab = Resources.Load<GameObject>(path);
        if (prefab != null)
        {
            _prefabCache[path] = prefab;
        }

        return prefab;
    }

    public Sprite LoadSprite(string path)
    {
        if (_spriteCache.TryGetValue(path, out var sprite))
        {
            return sprite;
        }

        sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            _spriteCache[path] = sprite;
        }

        return sprite;
    }

    public GameObject Instantiate(string path, Transform parent = null)
    {
        GameObject prefab = LoadPrefab(path);
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, parent);
            instance.name = prefab.name;
            return instance;
        }
        return null;
    }
}