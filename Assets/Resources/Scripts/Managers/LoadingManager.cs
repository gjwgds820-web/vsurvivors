using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace VSurvivors.Managers
{
    public class LoadingManager : MonoBehaviour
    {
        public static LoadingManager Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("로딩 화면의 최상단 캔버스 패널 (검은 배경 포함)")]
        [SerializeField] private GameObject loadingCanvas;
        [Tooltip("하단의 로딩 진행도를 표시할 슬라이더")]
        [SerializeField] private Slider progressBar;
        [Tooltip("회전하는 로딩 인디케이터 중앙 UI 이미지")]
        [SerializeField] private RectTransform spinner;
        
        [Header("Settings")]
        [SerializeField] private float spinSpeed = 360f;
        [Tooltip("너무 빨리 로딩될 경우 프로그레스 바 애니메이션을 위한 최소 대기 시간")]
        [SerializeField] private float minLoadingTime = 1.0f;

        private bool _isLoading = false;
        private bool _waitForManualCompletion = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (loadingCanvas != null)
                {
                    loadingCanvas.SetActive(false);
                }
            }
            else
            {
                Destroy(gameObject);
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
                _waitForManualCompletion = waitForManualCompletion;
                StartCoroutine(LoadSceneCoroutine(sceneName));
            }
        }

        public void FinishLoading()
        {
            _waitForManualCompletion = false;
        }

        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            _isLoading = true;

            // 로딩 중 UI 스레드가 너무 멈추는 것을 방지하기 위해 백그라운드 로딩 우선순위를 낮춥니다.
            Application.backgroundLoadingPriority = ThreadPriority.Low;

            if (loadingCanvas != null) loadingCanvas.SetActive(true);
            if (progressBar != null) progressBar.value = 0f;

            // 1. 비동기 씬 로딩 시작
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            // 메모리에 준비만 해두고 씬을 곧바로 전환하지 않음 (로딩바 애니메이션을 위해)
            operation.allowSceneActivation = false; 

            float timer = 0f;
            float maxVisualProgress = _waitForManualCompletion ? 0.9f : 1.0f; // 수동 대기 시 90%에서 멈춤

            // 2. 진행 상황 대기 및 UI 갱신 (씬 파일 로딩)
            while (operation.progress < 0.9f)
            {
                timer += Time.unscaledDeltaTime;
                
                // operation.progress는 0.9까지가 씬 파일 로딩의 최대치입니다.
                float realProgress = Mathf.Clamp01(operation.progress / 0.9f) * maxVisualProgress;
                
                // 시각적 부드러움을 보장하기 위해 실제 로딩율과 (타이머/최소시간) 중 더 작은 값을 목표치로 삼습니다.
                float targetProgress = Mathf.Min(realProgress, Mathf.Clamp01(timer / minLoadingTime) * maxVisualProgress);

                if (progressBar != null)
                {
                    progressBar.value = Mathf.Lerp(progressBar.value, targetProgress, Time.unscaledDeltaTime * 5f);
                }

                yield return null;
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

                yield return null;
            }

            // 활성화(Awake/Start) 전에 다시 CPU 리소스를 씬 초기화에 전부 투자합니다.
            Application.backgroundLoadingPriority = ThreadPriority.High;
            operation.allowSceneActivation = true;
            
            if (progressBar != null) progressBar.value = maxVisualProgress;

            // 유니티 내부적인 씬 활성화 작업이 끝나서 완전히 씬이 넘어갈 때까지 대기
            while (!operation.isDone)
            {
                yield return null;
            }

            // 3. 추가적인 수동 완료를 기다려야 한다면 (예: 배틀씬 ECS 매니저 등)
            if (_waitForManualCompletion)
            {
                while (_waitForManualCompletion)
                {
                    // 대기하는 동안 90%에서 99%까지 천천히(점진적으로) 가짜 로딩바를 채워줍니다.
                    if (progressBar != null)
                    {
                        progressBar.value = Mathf.Lerp(progressBar.value, 0.99f, Time.unscaledDeltaTime * 2f);
                    }
                    yield return null;
                }

                // 매니저 셋업까지 끝났으므로 남은 게이지를 100%까지 빠르게 채우기
                while (progressBar != null && progressBar.value < 0.99f)
                {
                    progressBar.value = Mathf.Lerp(progressBar.value, 1f, Time.unscaledDeltaTime * 15f);
                    yield return null;
                }
            }

            // 프로그레스 바 강제 100%
            if (progressBar != null) progressBar.value = 1f;

            // 로딩 완료 후 화면 닫기 전 0.1초 정도의 아주 짧은 유예
            yield return new WaitForSecondsRealtime(0.1f);

            if (loadingCanvas != null) loadingCanvas.SetActive(false);
            _isLoading = false;
        }
    }
}