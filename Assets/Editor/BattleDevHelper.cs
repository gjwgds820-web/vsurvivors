using UnityEditor;
using UnityEngine;
using Unity.Entities;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class BattleDevHelper : EditorWindow
{
    static BattleDevHelper()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (EditorPrefs.GetBool("FastStartBattle", false))
            {
                EditorPrefs.SetBool("FastStartBattle", false);
                
                // 로비 씬의 매니저들이 DontDestroyOnLoad 구조를 완료하도록 한 프레임 지연
                EditorApplication.delayCall += () =>
                {
                    // DataManager에 접근하여 테스트용 그림자(최대 4개) 임시 장착
                    if (DataManager.Instance != null && DataManager.Instance.currentUserData != null)
                    {
                        var dummyShadows = new System.Collections.Generic.List<int>();
                        foreach (var kvp in DataManager.Instance.SkillDict)
                        {
                            if (kvp.Value.Type == SkillType.Shadow)
                            {
                                dummyShadows.Add(kvp.Key);
                                if (dummyShadows.Count >= 4) break;
                            }
                        }
                        
                        DataManager.Instance.currentUserData.SelectedShadowsID = dummyShadows;
                        Debug.Log($"[Battle Dev Helper] 빠른 시작 테스트를 위해 그림자 {dummyShadows.Count}개를 임시 장착했습니다.");
                    }
                    else
                    {
                        Debug.LogWarning("[Battle Dev Helper] DataManager를 찾을 수 없어 그림자 장착을 건너뜁니다.");
                    }

                    if (VSurvivors.Managers.LoadingManager.Instance != null)
                    {
                        VSurvivors.Managers.LoadingManager.Instance.LoadScene("BattleScene", true);
                    }
                    else
                    {
                        SceneManager.LoadScene("BattleScene");
                    }
                };
            }
        }
    }

    [MenuItem("VSurvivors/Battle Dev Helper")]
    public static void ShowWindow()
    {
        GetWindow<BattleDevHelper>("Battle Dev Helper");
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            // 배틀 씬에서 동적으로 생성되는 데이터(GameDirector 등)를 즉각 갱신하기 위해 프레임마다 UI 업데이트
            Repaint();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Testing Tool (Battle Scene)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드(배틀 씬)에서만 동적 조절이 작동합니다.", MessageType.Info);
            
            GUILayout.Space(10);
            if (GUILayout.Button("▶ 로비 거쳐서 배틀 씬 즉시 시작", GUILayout.Height(40)))
            {
                EditorPrefs.SetBool("FastStartBattle", true);
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene("Assets/Resources/Scenes/LobbyScene.unity");
                    EditorApplication.isPlaying = true;
                }
            }
            return;
        }

        // ECS 월드 존재 확인
        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            EditorGUILayout.HelpBox("ECS 월드를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(GameDirectorData));

        if (!query.HasSingleton<GameDirectorData>())
        {
            EditorGUILayout.HelpBox("GameDirectorData 싱글톤을 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        var directorEntity = query.GetSingletonEntity();
        var directorData = em.GetComponentData<GameDirectorData>(directorEntity);

        EditorGUI.BeginChangeCheck();

        float newEnemyInterval = EditorGUILayout.FloatField("일반 몬스터 스폰 간격 (sec)", directorData.EnemySpawnInterval);
        float newBossInterval = EditorGUILayout.FloatField("보스 스폰 주기 (sec)", directorData.BossSpawnInterval);
        float newExpBase = EditorGUILayout.FloatField("레벨업 경험치 기준량", directorData.ExpRequirementBase);

        if (EditorGUI.EndChangeCheck())
        {
            // 값이 바뀌면 ECS 데이터 업데이트
            directorData.EnemySpawnInterval = newEnemyInterval;
            directorData.BossSpawnInterval = newBossInterval;
            directorData.ExpRequirementBase = newExpBase;

            em.SetComponentData(directorEntity, directorData);
            
            // 현재 스폰 타이머가 바뀐 간격보다 크다면 바로 보정 (너무 오래 기다리는 현상 방지)
            if (directorData.EnemySpawnTimer > directorData.EnemySpawnInterval)
            {
                directorData.EnemySpawnTimer = directorData.EnemySpawnInterval;
                em.SetComponentData(directorEntity, directorData);
            }
        }

        // 강제 레벨업/보스소환 버튼 등 편의 기능
        GUILayout.Space(20);
        if (GUILayout.Button("현재 대기중인 보스 즉시 소환"))
        {
            directorData.BossTimer = 0.5f; // 즉시 나오도록
            directorData.GlobalTimer = directorData.CurrentWave * directorData.BossSpawnInterval + 1f;
            em.SetComponentData(directorEntity, directorData);
            Debug.Log("보스 스폰 타이머를 가속했습니다.");
        }

        if (GUILayout.Button("플레이어 즉시 1레벨 업"))
        {
            var playerQuery = em.CreateEntityQuery(typeof(PlayerData));
            if (playerQuery.HasSingleton<PlayerData>())
            {
                var pEntity = playerQuery.GetSingletonEntity();
                var pData = em.GetComponentData<PlayerData>(pEntity);
                
                var queryDirector = em.CreateEntityQuery(typeof(GameDirectorData));
                float expBase = 100f;
                if (queryDirector.HasSingleton<GameDirectorData>())
                    expBase = queryDirector.GetSingleton<GameDirectorData>().ExpRequirementBase;
                
                float requiredExp = expBase * pData.Level;
                // 현재 경험치가 요구치보다 적다면 딱 1레벨업에 필요한 양만 채워주기
                if (pData.EXP < requiredExp)
                {
                    pData.EXP = requiredExp;
                }
                em.SetComponentData(pEntity, pData);
                Debug.Log("플레이어에게 1레벨업 만큼의 경험치를 주었습니다.");
            }
        }

        if (GUILayout.Button("속성 초월 팝업 즉시 띄우기"))
        {
            var eventEntity = em.CreateEntity();
            em.AddComponentData(eventEntity, new ElementAscensionEventTag { BossLevel = 1 });
            Debug.Log("속성 초월 팝업 이벤트를 강제로 발생시켰습니다.");
        }

        GUILayout.Space(20);
        GUILayout.Label("Player Action Testing", EditorStyles.boldLabel);

        if (GUILayout.Button("플레이어 피격 애니메이션 테스트 (10 데미지)"))
        {
            var playerQuery = em.CreateEntityQuery(typeof(PlayerData));
            if (playerQuery.HasSingleton<PlayerData>())
            {
                var pEntity = playerQuery.GetSingletonEntity();
                if (em.HasBuffer<DamageBufferElement>(pEntity))
                {
                    var damageBuffer = em.GetBuffer<DamageBufferElement>(pEntity);
                    damageBuffer.Add(new DamageBufferElement { Damage = 10f });
                    Debug.Log("플레이어에게 10 데미지를 가했습니다. (피격 애니메이션 테스트)");
                }
            }
        }

        if (GUILayout.Button("플레이어 사망 애니메이션 테스트 (체력 0)"))
        {
            var playerQuery = em.CreateEntityQuery(typeof(PlayerData));
            if (playerQuery.HasSingleton<PlayerData>())
            {
                var pEntity = playerQuery.GetSingletonEntity();
                if (em.HasComponent<HealthData>(pEntity))
                {
                    var healthData = em.GetComponentData<HealthData>(pEntity);
                    healthData.CurrentHealth = 0f;
                    em.SetComponentData(pEntity, healthData);
                    Debug.Log("플레이어 체력을 0으로 설정했습니다. (사망 애니메이션 테스트)");
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("동적 변경사항이 즉시 반영됩니다. \nGameDirectorAuthoring의 기본값을 변경하려면 인스펙터에서 수정하세요.", MessageType.None);
    }
}
