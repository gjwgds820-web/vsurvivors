using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;

namespace VSurvivors.Managers
{
    public class LoadingManager : MonoBehaviour, IAsyncInitializable
    {
        public static LoadingManager Instance { get; private set; }

        private GameObject loadingCanvas;
        private Slider progressBar;
        private RectTransform spinner;
        
        [Header("Settings")]
        [SerializeField] private float spinSpeed = 360f;
        [Tooltip("너무 빨리 로딩될 경우 프로그레스 바 애니메이션을 위한 최소 대기 시간")]
        [SerializeField] private float minLoadingTime = 1.0f;

        private bool _isLoading = false;
        private UniTaskCompletionSource _manualCompletionSource;

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

        public async UniTask InitAsync()
        {
            // Addressables를 통해 UI_LoadingUI 프리팹을 동적으로 생성
            GameObject loadingPrefab = await Addressables.InstantiateAsync("UI_LoadingUI", transform).Task;
            
            if (loadingPrefab != null)
            {
                // 프리팹 내부에서 필요한 컴포넌트들을 코드로 바인딩
                // 구조가 변경될 수 있으니 이름을 기반으로 재귀 탐색하거나 직접 GetComponentInChildren 호출
                // 구조: Canvas (최상위) -> ... -> Slider, Spinner 등
                loadingCanvas = loadingPrefab;
                progressBar = loadingPrefab.GetComponentInChildren<Slider>(true);
                
                // Spinner는 보통 특정 이름이거나, 스크립트가 붙어있거나 하므로 이름으로 탐색
                var spinnerObj = Util.FindChild(loadingPrefab, "Spinner", true);
                if (spinnerObj != null) spinner = spinnerObj.GetComponent<RectTransform>();
                
                loadingCanvas.SetActive(false);
            }
            else
            {
                Debug.LogError("[LoadingManager] UI_LoadingUI 어드레서블 프리팹 로드 실패!");
            }
        }

        private void Update()
        {
            if (_isLoading && spinner != null)
            {
                // 인디케이터 부드럽게 회전 (TimeScale의 영향을 받지 않도록 unscaledDeltaTime 사용)
                spinner.Rotate(Vector3.forward, -spinSpeed * Time.unscaledDeltaTime);
            }
        }

        public void LoadScene(string sceneName, bool waitForManualCompletion = false)
        {
            if (!_isLoading)
            {
                if (waitForManualCompletion)
                {
                    // 상태 초기화
                    _manualCompletionSource = new UniTaskCompletionSource();
                }
                else
                {
                    _manualCompletionSource = null;
                }
                
                LoadSceneAsync(sceneName).Forget();
            }
        }

        public void FinishLoading()
        {
            if (_manualCompletionSource != null)
            {
                _manualCompletionSource.TrySetResult();
            }
        }

        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            _isLoading = true;

            // 추가: 새로운 씬으로 넘어가기 전, 기존에 캐싱된 어드레서블 메모리를 해제합니다.
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.ClearCache();
            }

            // 로딩 중 UI 스레드가 너무 멈추는 것을 방지하기 위해 백그라운드 로딩 우선순위를 낮춥니다.
            Application.backgroundLoadingPriority = ThreadPriority.Low;

            if (loadingCanvas != null) loadingCanvas.SetActive(true);
            if (progressBar != null) progressBar.value = 0f;

            // 1. 비동기 씬 로딩 시작 (Addressables)
            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single, false);
            
            float timer = 0f;
            float maxVisualProgress = (_manualCompletionSource != null) ? 0.9f : 1.0f; // 수동 대기 시 90%에서 멈춤

            // 2. 진행 상황 대기 및 UI 갱신
            while (!handle.IsDone && handle.PercentComplete < 0.9f)
            {
                timer += Time.unscaledDeltaTime;
                
                // Addressables의 인스턴스 진행도는 PercentComplete로 받아옴
                float targetProgress = Mathf.Min(handle.PercentComplete, maxVisualProgress);
                
                if (progressBar != null)
                {
                    progressBar.value = Mathf.Lerp(progressBar.value, targetProgress, Time.unscaledDeltaTime * 2f);
                }
                
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 씬 파일 로딩은 완료되었으나, 비주얼적인 최소 로딩시간 및 프로그레스바 목표치에 도달할 때까지 대기
            while (timer < minLoadingTime || (progressBar != null && progressBar.value < maxVisualProgress - 0.05f))
            {
                timer += Time.unscaledDeltaTime;
                float targetProgress = Mathf.Clamp01(timer / minLoadingTime) * maxVisualProgress;
                
                if (progressBar != null)
                {
                    progressBar.value = Mathf.Lerp(progressBar.value, targetProgress, Time.unscaledDeltaTime * 5f);
                }

                await UniTask.Yield();
            }

            // 활성화(Awake/Start) 전에 다시 CPU 리소스를 씬 초기화에 전부 투자합니다.
            Application.backgroundLoadingPriority = ThreadPriority.High;
            await handle.Result.ActivateAsync();
            
            if (progressBar != null) progressBar.value = maxVisualProgress;

            // 추가적인 수동 완료 대기
            if (_manualCompletionSource != null)
            {
                // 대기하는 동안 90%에서 99%까지 천천히(점진적으로) 가짜 로딩바를 채워줍니다.
                while (_manualCompletionSource.Task.Status == UniTaskStatus.Pending)
                {
                    if (progressBar != null)
                    {
                        progressBar.value = Mathf.Lerp(progressBar.value, 0.99f, Time.unscaledDeltaTime * 2f);
                    }
                    await UniTask.Yield();
                }

                // 매니저 셋업까지 끝났으므로 남은 게이지를 100%까지 빠르게 채우기
                while (progressBar != null && progressBar.value < 0.99f)
                {
                    progressBar.value = Mathf.Lerp(progressBar.value, 1f, Time.unscaledDeltaTime * 15f);
                    await UniTask.Yield();
                }
            }

            // 프로그레스 바 강제 100%
            if (progressBar != null) progressBar.value = 1f;

            // 로딩 완료 후 화면 닫기 전 0.1초 정도의 아주 짧은 유예
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), ignoreTimeScale: true);

            if (loadingCanvas != null) loadingCanvas.SetActive(false);
            _isLoading = false;
        }
    }
}
