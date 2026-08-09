using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VSurvivors.Managers;

public class UIManager : MonoBehaviour, IAsyncInitializable
{
    public static UIManager Instance { get; private set; }

    [Header("Popup Prefabs")]
    [SerializeField] private List<GameObject> popupPrefabs;

    [Header("Toast Settings")]
    [SerializeField] private Color toastFadeColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float toastFadeAlpha = 0.75f;
    [SerializeField] private float toastScreenFadeOutDuration = 0.25f;
    [SerializeField] private float toastTextFadeInDuration = 0.15f;
    [SerializeField] private float toastTextVisibleDuration = 0.85f;
    [SerializeField] private float toastTextFadeOutDuration = 0.15f;
    [SerializeField] private float toastScreenFadeInDuration = 0.25f;

    [Header("Floating Toast Settings")]
    [SerializeField] private float floatingToastFadeInDuration = 0.12f;
    [SerializeField] private float floatingToastVisibleDuration = 0.6f;
    [SerializeField] private float floatingToastFadeOutDuration = 0.3f;
    [SerializeField] private float floatingToastMoveUpDistance = 80f;

    private readonly Dictionary<string, GameObject> _popupInstanceCache = new Dictionary<string, GameObject>();
    private readonly Stack<GameObject> _activePopups = new Stack<GameObject>();

    [Header("UI Canvas")]
    private Transform popupParent;

    private GameObject _toastBlockerRoot;
    private CanvasGroup _toastBlockerCanvasGroup;

    private GameObject _activeToastInstance;
    private CanvasGroup _activeToastCanvasGroup;
    private TMP_Text _activeToastText;

    private CancellationTokenSource _toastCts;
    private bool _isToastShowing;
    private bool _toastSkipRequested;

    private GameObject _activeFloatingToastInstance;
    private CanvasGroup _activeFloatingToastCanvasGroup;
    private TMP_Text _activeFloatingToastText;
    private RectTransform _activeFloatingToastRect;
    private CancellationTokenSource _floatingToastCts;
    private bool _isFloatingToastShowing;

    public bool IsToastShowing => _isToastShowing;
    public bool IsFloatingToastShowing => _isFloatingToastShowing;

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

        CancelAndCleanupToast();
        CancelAndCleanupFloatingToast();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _popupInstanceCache.Clear();
        _activePopups.Clear();

        CancelAndCleanupToast();
        CancelAndCleanupFloatingToast();
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
        return _activePopups.Count > 0;
    }

    public async UniTask<bool> ShowToastAsync(string toastAddress, string message, Color? textColor = null)
    {
        if (_isToastShowing)
        {
            return false;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[UIManager] ResourceManager.Instance is null. Cannot show toast.");
            return false;
        }

        if (popupParent == null)
        {
            FindAndAssingUIRoot();
            if (popupParent == null)
            {
                Debug.LogError("[UIManager] popupParent is null. Cannot show toast.");
                return false;
            }
        }

        EnsureToastBlocker();
        if (_toastBlockerRoot == null || _toastBlockerCanvasGroup == null)
        {
            Debug.LogError("[UIManager] Failed to initialize toast blocker.");
            return false;
        }

        _toastCts = new CancellationTokenSource();
        _isToastShowing = true;
        _toastSkipRequested = false;

        try
        {
            _toastBlockerRoot.transform.SetParent(popupParent, false);
            _toastBlockerRoot.SetActive(true);
            _toastBlockerRoot.transform.SetAsLastSibling();
            _toastBlockerCanvasGroup.alpha = 0f;
            _toastBlockerCanvasGroup.blocksRaycasts = true;
            _toastBlockerCanvasGroup.interactable = true;

            _activeToastInstance = await ResourceManager.Instance.InstantiateAddressableAsync(toastAddress, _toastBlockerRoot.transform);
            if (_activeToastInstance == null)
            {
                Debug.LogError($"[UIManager] Toast prefab load failed. Address: {toastAddress}");
                return false;
            }

            _activeToastInstance.transform.SetAsLastSibling();
            _activeToastCanvasGroup = _activeToastInstance.GetComponent<CanvasGroup>();
            if (_activeToastCanvasGroup == null)
            {
                _activeToastCanvasGroup = _activeToastInstance.AddComponent<CanvasGroup>();
            }

            _activeToastCanvasGroup.alpha = 0f;
            _activeToastCanvasGroup.blocksRaycasts = false;
            _activeToastCanvasGroup.interactable = false;

            _activeToastText = _activeToastInstance.GetComponentInChildren<TMP_Text>(true);
            if (_activeToastText != null)
            {
                _activeToastText.text = message;
                if (textColor.HasValue)
                {
                    _activeToastText.color = textColor.Value;
                }
            }
            else
            {
                Debug.LogWarning("[UIManager] Toast prefab has no TMP_Text. Message assignment skipped.");
            }

            await AwaitTween(
                _toastBlockerCanvasGroup.DOFade(toastFadeAlpha, toastScreenFadeOutDuration).SetUpdate(true),
                _toastCts.Token);

            await AwaitTween(
                _activeToastCanvasGroup.DOFade(1f, toastTextFadeInDuration).SetUpdate(true),
                _toastCts.Token);

            await WaitForToastDismissOrTimeoutAsync(_toastCts.Token);

            await AwaitTween(
                _activeToastCanvasGroup.DOFade(0f, toastTextFadeOutDuration).SetUpdate(true),
                _toastCts.Token);

            await AwaitTween(
                _toastBlockerCanvasGroup.DOFade(0f, toastScreenFadeInDuration).SetUpdate(true),
                _toastCts.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            CleanupToastVisuals();

            _toastCts?.Dispose();
            _toastCts = null;

            _isToastShowing = false;
            _toastSkipRequested = false;
        }
    }

    public async UniTask<bool> ShowFloatingToastAsync(string toastAddress, string message, Color? textColor = null)
    {
        if (_isFloatingToastShowing)
        {
            return false;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[UIManager] ResourceManager.Instance is null. Cannot show floating toast.");
            return false;
        }

        if (popupParent == null)
        {
            FindAndAssingUIRoot();
            if (popupParent == null)
            {
                Debug.LogError("[UIManager] popupParent is null. Cannot show floating toast.");
                return false;
            }
        }

        _floatingToastCts = new CancellationTokenSource();
        _isFloatingToastShowing = true;

        try
        {
            _activeFloatingToastInstance = await ResourceManager.Instance.InstantiateAddressableAsync(toastAddress, popupParent);
            if (_activeFloatingToastInstance == null)
            {
                Debug.LogError($"[UIManager] Floating toast prefab load failed. Address: {toastAddress}");
                return false;
            }

            _activeFloatingToastInstance.transform.SetAsLastSibling();

            _activeFloatingToastCanvasGroup = _activeFloatingToastInstance.GetComponent<CanvasGroup>();
            if (_activeFloatingToastCanvasGroup == null)
            {
                _activeFloatingToastCanvasGroup = _activeFloatingToastInstance.AddComponent<CanvasGroup>();
            }

            _activeFloatingToastCanvasGroup.alpha = 0f;
            _activeFloatingToastCanvasGroup.blocksRaycasts = false;
            _activeFloatingToastCanvasGroup.interactable = false;

            _activeFloatingToastText = _activeFloatingToastInstance.GetComponentInChildren<TMP_Text>(true);
            if (_activeFloatingToastText != null)
            {
                _activeFloatingToastText.text = message;
                if (textColor.HasValue)
                {
                    _activeFloatingToastText.color = textColor.Value;
                }
            }

            _activeFloatingToastRect = _activeFloatingToastInstance.GetComponent<RectTransform>();
            float startY = 0f;
            if (_activeFloatingToastRect != null)
            {
                startY = _activeFloatingToastRect.anchoredPosition.y;
            }

            await AwaitTween(
                _activeFloatingToastCanvasGroup.DOFade(1f, floatingToastFadeInDuration).SetUpdate(true),
                _floatingToastCts.Token);

            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, floatingToastVisibleDuration)), true, PlayerLoopTiming.Update, _floatingToastCts.Token);

            Sequence fadeOutSequence = DOTween.Sequence().SetUpdate(true);
            fadeOutSequence.Join(_activeFloatingToastCanvasGroup.DOFade(0f, floatingToastFadeOutDuration));
            if (_activeFloatingToastRect != null)
            {
                fadeOutSequence.Join(_activeFloatingToastRect.DOAnchorPosY(startY + floatingToastMoveUpDistance, floatingToastFadeOutDuration).SetEase(Ease.OutCubic));
            }

            await AwaitTween(fadeOutSequence, _floatingToastCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            CleanupFloatingToastVisuals();

            _floatingToastCts?.Dispose();
            _floatingToastCts = null;

            _isFloatingToastShowing = false;
        }
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
        if (_isToastShowing)
        {
            RequestToastDismiss();
            return;
        }

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
        if (_isToastShowing)
        {
            RequestToastDismiss();
            return;
        }

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

    private void EnsureToastBlocker()
    {
        if (_toastBlockerRoot != null && _toastBlockerCanvasGroup != null)
        {
            return;
        }

        _toastBlockerRoot = new GameObject("ToastBlocker", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Button));
        RectTransform rect = _toastBlockerRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image blockerImage = _toastBlockerRoot.GetComponent<Image>();
        blockerImage.color = new Color(toastFadeColor.r, toastFadeColor.g, toastFadeColor.b, 1f);
        blockerImage.raycastTarget = true;

        _toastBlockerCanvasGroup = _toastBlockerRoot.GetComponent<CanvasGroup>();
        _toastBlockerCanvasGroup.alpha = 0f;
        _toastBlockerCanvasGroup.blocksRaycasts = false;
        _toastBlockerCanvasGroup.interactable = false;

        Button blockerButton = _toastBlockerRoot.GetComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(RequestToastDismiss);

        _toastBlockerRoot.SetActive(false);
    }

    private async UniTask WaitForToastDismissOrTimeoutAsync(CancellationToken token)
    {
        float duration = Mathf.Max(0f, toastTextVisibleDuration);
        float elapsed = 0f;

        while (!_toastSkipRequested && elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private void RequestToastDismiss()
    {
        if (!_isToastShowing)
        {
            return;
        }

        _toastSkipRequested = true;
    }

    private void CancelAndCleanupToast()
    {
        if (_toastCts != null && !_toastCts.IsCancellationRequested)
        {
            _toastCts.Cancel();
        }

        CleanupToastVisuals();

        _toastCts?.Dispose();
        _toastCts = null;

        _isToastShowing = false;
        _toastSkipRequested = false;
    }

    private void CancelAndCleanupFloatingToast()
    {
        if (_floatingToastCts != null && !_floatingToastCts.IsCancellationRequested)
        {
            _floatingToastCts.Cancel();
        }

        CleanupFloatingToastVisuals();

        _floatingToastCts?.Dispose();
        _floatingToastCts = null;
        _isFloatingToastShowing = false;
    }

    private void CleanupToastVisuals()
    {
        if (_activeToastInstance != null)
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.ReleaseAddressableInstance(_activeToastInstance);
            }
            else
            {
                Destroy(_activeToastInstance);
            }
        }

        _activeToastInstance = null;
        _activeToastCanvasGroup = null;
        _activeToastText = null;

        if (_toastBlockerCanvasGroup != null)
        {
            _toastBlockerCanvasGroup.alpha = 0f;
            _toastBlockerCanvasGroup.blocksRaycasts = false;
            _toastBlockerCanvasGroup.interactable = false;
        }

        if (_toastBlockerRoot != null)
        {
            _toastBlockerRoot.SetActive(false);
        }
    }

    private void CleanupFloatingToastVisuals()
    {
        if (_activeFloatingToastInstance != null)
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.ReleaseAddressableInstance(_activeFloatingToastInstance);
            }
            else
            {
                Destroy(_activeFloatingToastInstance);
            }
        }

        _activeFloatingToastInstance = null;
        _activeFloatingToastCanvasGroup = null;
        _activeFloatingToastText = null;
        _activeFloatingToastRect = null;
    }

    private static UniTask AwaitTween(Tween tween, CancellationToken token)
    {
        if (tween == null)
        {
            return UniTask.CompletedTask;
        }

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        bool isCompleted = false;

        CancellationTokenRegistration registration = token.Register(() =>
        {
            if (isCompleted) return;

            isCompleted = true;
            if (tween.IsActive())
            {
                tween.Kill(false);
            }

            completionSource.TrySetCanceled(token);
        });

        tween.OnComplete(() =>
        {
            if (isCompleted) return;

            isCompleted = true;
            registration.Dispose();
            completionSource.TrySetResult();
        });

        tween.OnKill(() =>
        {
            if (isCompleted) return;

            isCompleted = true;
            registration.Dispose();
            completionSource.TrySetCanceled(token);
        });

        return completionSource.Task;
    }
}