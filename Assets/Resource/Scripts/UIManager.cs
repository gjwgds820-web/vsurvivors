using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Popup Prefabs")]
    [SerializeField] private List<GameObject> popupPrefabs;

    private Dictionary<string, GameObject> _popupCache = new Dictionary<string, GameObject>();

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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_activePopups.Count > 0)
            {
                CloseTopPopup();
            }
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
        if (_popupCache.TryGetValue(popupName, out GameObject popup))
        {
            return popup;
        }

        GameObject prefab = popupPrefabs.Find(p => p.name == popupName);
        if (prefab != null)
        {
            GameObject newPopup = Instantiate(prefab, popupParent);
            newPopup.name = popupName;
            newPopup.SetActive(false);
            _popupCache.Add(popupName, newPopup);
            return newPopup;
        }

        Debug.LogError($"Popup prefab '{popupName}' not found in the list.");
        return null;
    }
}