using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace VSurvivors.Managers
{
    /// <summary>
    /// 게임 전체의 전역 매니저 초기화 과정을 책임지는 Bootstrapper
    /// 게임 실행 시 가장 처음 로드되는 빈 공간(최상단)에서 동작합니다.
    /// </summary>
    public static class AppManager
    {
        public static bool IsInitialized { get; private set; } = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void InitializeAppAsync()
        {
            IsInitialized = false;
            Debug.Log("[AppManager] 매니저 비동기 부트스트랩 시작...");
            
            // 기존 씬이나 하이어라키에 DataManager, ResourceManager가 이미 올라가있을 수 있으니
            // 필요하다면 새로 생성하는 팩토리 패턴을 쓰거나 씬 내 싱글톤 인스턴스를 찾습니다.
            // 하지만 BeforeSceneLoad 시점엔 씬 객체를 찾기 어렵기 때문에,
            // 동적 프리팹 로드나 코드로 즉시 Inject 하여 붙여줍니다.
            
            GameObject managerRunner = new GameObject("[Global_Managers]");
            Object.DontDestroyOnLoad(managerRunner);
            
            // 핵심 매니저들을 동적으로 한곳에 생성하고 초기화 파이프라인 형성
            var dataManager = managerRunner.AddComponent<DataManager>();
            var resourceManager = managerRunner.AddComponent<ResourceManager>();
            var uiManager = managerRunner.AddComponent<UIManager>();
            var loadingManager = managerRunner.AddComponent<LoadingManager>();

            // WhenAll을 통해 각 매니저들의 비동기 준비 과정을 병렬 처리
            await UniTask.WhenAll(
                dataManager.InitAsync(),
                resourceManager.InitAsync(),
                uiManager.InitAsync(),
                loadingManager.InitAsync()
            );

            IsInitialized = true;
            
            Debug.Log("[AppManager] 모든 전역 매니저 초기화 및 준비 완료!");
            
            // 초기화가 완전히 끝난 뒤, 로비 등 메인 씬으로 자연스럽게 넘어가려면
            // 여기에서 SceneManager 등을 통해 스플래시 -> 로비씬 전환을 지시할 수 있습니다.
            // (현재는 에디터 실행 씬을 그대로 사용하도록 놔둡니다.)
        }
    }
}
