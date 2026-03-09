using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Popup Prefabs")]
    [SerializeField] private List<GameObject> popupPrefabs;

    private Dictionary<string, GameObject> _popupInstanceCache = new Dictionary<string, GameObject>();

    private Stack<GameObject> _activePopups = new Stack<GameObject>();

    [Header("UI Canvas")]
    [SerializeField] private Transform popupParent;

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

    public void ShowPopup(string popupName)
    {
        GameObject popup = GetOrCreatePopup(popupName);
        if (popup != null)
        {
            popup.SetActive(true);
            popup.transform.SetAsLastSibling();
            _activePopups.Push(popup);
        }
    }

    public void CloseTopPopup()
    {
        if (_activePopups.Count > 0)
        {
            GameObject topPopup = _activePopups.Pop();
            topPopup.SetActive(false);
        }
    }

    private GameObject GetOrCreatePopup(string popupName)
    {
        if (_popupInstanceCache.TryGetValue(popupName, out GameObject popup))
        {
            return popup;
        }

        string prefabPath = $"UI/Popups/{popupName}";
        GameObject prefab = ResourceManager.Instance.Instantiate(prefabPath, popupParent);
        if (prefab != null)
        {
            prefab.SetActive(false);
            _popupInstanceCache.Add(popupName, prefab);
            return prefab;
        }

        Debug.LogError($"Popup prefab '{popupName}' not found in the list.");
        return null;
    }
}