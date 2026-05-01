using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VSurvivors.Managers;

public class UIManager : MonoBehaviour, IAsyncInitializable
{
        public static UIManager Instance { get; private set; }

        [Header("Popup Prefabs")]
        [SerializeField] private List<GameObject> popupPrefabs;

        private Dictionary<string, GameObject> _popupInstanceCache = new Dictionary<string, GameObject>();

        private Stack<GameObject> _activePopups = new Stack<GameObject>();

        [Header("UI Canvas")]
        private Transform popupParent;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public UniTask InitAsync()
        {
            FindAndAssingUIRoot();
            return UniTask.CompletedTask;
        }

        private void OnDestroy()
        {
        if (Instance == this)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            _popupInstanceCache.Clear();
            _activePopups.Clear();
            
            FindAndAssingUIRoot();
        }

        private void FindAndAssingUIRoot()
        {
            GameObject uiRoot = GameObject.Find("PanelUI");
            if (uiRoot == null) uiRoot = GameObject.Find("UIRoot");

            if (uiRoot != null)
            {
                popupParent = uiRoot.transform;
            }
            else
            {
                Debug.LogWarning("[UIManager] PanelUI or UIRoot not found in the loaded scene.");
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
    public void CloseAllPopups()
    {
        while (_activePopups.Count > 0)
        {
            GameObject popup = _activePopups.Pop();
            popup.SetActive(false);
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