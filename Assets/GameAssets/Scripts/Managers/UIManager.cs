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

    public bool HasActivePopup()
    {
        return _activePopups != null && _activePopups.Count > 0;
    }

    private void Update()
    {
        // 안드로이드 뒤로가기 기본 매핑은 Escape 키로 동작합니다.
        // New Input System을 사용할 경우 아래와 같이 처리 가능합니다:
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleBackButton();
        }
    }

    private void HandleBackButton()
    {
        if (HasActivePopup())
        {
            // 예외 처리 필요한 팝업(예: 사망, 설정 강제 팝업 등)이 아닐 경우 상단 팝업 닫기
            CloseTopPopup();
        }
        else
        {
            // 현재 씬에 따라 다른 팝업 띄우기
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "LobbyScene")
            {
                ShowPopup("UI_QuitConfirmPopup");
            }
            else if (sceneName == "BattleScene")
            {
                ShowPopup("UI_PausePopup");
            }
            else
            {
                Debug.Log($"[UIManager] 뒤로가기 입력 - 지원하지 않는 씬({sceneName})입니다.");
            }
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            HandleAppBackground();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            HandleAppBackground();
        }
    }

    private void HandleAppBackground()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        // 전투 씬인데 현재 팝업이 없다면, 강제로 일시정지 팝업을 띄움
        if (sceneName == "BattleScene" && !HasActivePopup())
        {
            ShowPopup("UI_PausePopup");
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