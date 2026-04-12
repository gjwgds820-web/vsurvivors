using Unity.Entities;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

#region VisualSync
[UpdateInGroup(typeof(PresentationSystemGroup))] 
public partial class VisualSyncSystem : SystemBase
{
    private EntityQuery _enemyMissingVisualQuery;
    private EntityQuery _bossMissingVisualQuery;
    private EntityQuery _gateMissingVisualQuery;
    private EntityQuery _shadowMissingVisualQuery;
    private EntityQuery _itemMissingVisualQuery;
    private EntityQuery _playerMissingVisualQuery;
    private EntityQuery _effectMissingVisualQuery;

    protected override void OnCreate()
    {
        _enemyMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemyTag>().WithNone<SubSceneVisualModel, Prefab, BossTag>().Build();
        _bossMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemyTag, BossTag>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _gateMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, GateData>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _shadowMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, CShadowData>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _itemMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, DroppedItemData>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _playerMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerData>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _effectMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EffectVisualInfo>().WithNone<SubSceneVisualModel, Prefab>().Build();
    }

    protected override void OnUpdate()
    {
        if (VisualManager.Instance == null) return;
        float deltaTime = SystemAPI.Time.DeltaTime;

        if (!_playerMissingVisualQuery.IsEmpty)
        {
            var entities = _playerMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var transform = EntityManager.GetComponentData<LocalTransform>(entity);
                var pos = transform.Position;
                pos.y = 0f; // 초기 스폰 바닥 배치
                
                string charName = "Mage"; // 기본값
                if (DataManager.Instance != null && DataManager.Instance.currentUserData != null)
                {
                    int charID = DataManager.Instance.currentUserData.SelectedCharacterID;
                    if (DataManager.Instance.CharacterDict.TryGetValue(charID, out var cData))
                    {
                        charName = cData.Name;
                    }
                }

                string pPath = $"Prefabs/VisualPrefabs/{charName}(Battle)";
                GameObject prefab = ResourceManager.Instance.LoadPrefab(pPath);
                
                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, pos, transform.Rotation);
                    EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });

                    var animators = go.GetComponentsInChildren<Animator>(true);
                    if (animators != null && animators.Length > 0)
                    {
                        foreach (var anim in animators)
                        {
                            if (anim.GetComponent<AnimationEventReceiver>() == null)
                                anim.gameObject.AddComponent<AnimationEventReceiver>();
                        }
                        EntityManager.AddComponentObject(entity, new AnimatorModel { Value = animators[0], Animators = animators });
                    }
                    EntityManager.AddComponentData(entity, new VisualAnimationState());

                    // RendererModel 추가 (피격 깜빡임용)
                    var renderers = go.GetComponentsInChildren<Renderer>();
                    var colors = new Color[renderers.Length];
                    var blocks = new MaterialPropertyBlock[renderers.Length];
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        blocks[i] = new MaterialPropertyBlock();
                        renderers[i].GetPropertyBlock(blocks[i]);

                        if (renderers[i].sharedMaterial != null)
                        {
                            if (renderers[i].sharedMaterial.HasProperty("_BaseColor"))
                                colors[i] = renderers[i].sharedMaterial.GetColor("_BaseColor");
                            else if (renderers[i].sharedMaterial.HasProperty("_Color"))
                                colors[i] = renderers[i].sharedMaterial.color;
                            else
                                colors[i] = Color.white;
                        }
                        else
                        {
                            colors[i] = Color.white;
                        }
                    }
                    EntityManager.AddComponentObject(entity, new VisualRendererModel { Renderers = renderers, OriginalColors = colors, PropertyBlocks = blocks });
                }
                else
                {
                    Debug.LogError($"[VisualSystems] Player Visual Prefab NOT FOUND: {pPath}");
                }
            }
            entities.Dispose();
        }

        if (!_enemyMissingVisualQuery.IsEmpty)
        {
            var entities = _enemyMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.EnemyVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });

                var animators = go.GetComponentsInChildren<Animator>(true);
                if (animators != null && animators.Length > 0)
                {
                    foreach (var anim in animators)
                    {
                        if (anim.GetComponent<AnimationEventReceiver>() == null)
                            anim.gameObject.AddComponent<AnimationEventReceiver>();
                    }
                    EntityManager.AddComponentObject(entity, new AnimatorModel { Value = animators[0], Animators = animators });
                }
                EntityManager.AddComponentData(entity, new VisualAnimationState());
            }
            entities.Dispose();
        }

        if (!_bossMissingVisualQuery.IsEmpty)
        {
            var entities = _bossMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.BossVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });

                var animators = go.GetComponentsInChildren<Animator>(true);
                if (animators != null && animators.Length > 0)
                {
                    foreach (var anim in animators)
                    {
                        if (anim.GetComponent<AnimationEventReceiver>() == null)
                            anim.gameObject.AddComponent<AnimationEventReceiver>();
                    }
                    EntityManager.AddComponentObject(entity, new AnimatorModel { Value = animators[0], Animators = animators });
                }
                EntityManager.AddComponentData(entity, new VisualAnimationState());
            }
            entities.Dispose();
        }

        if (!_gateMissingVisualQuery.IsEmpty)
        {
            var entities = _gateMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.GateVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
                
                // 게이트 UI 스크립트가 있다면 초기화
                var gateData = EntityManager.GetComponentData<GateData>(entity);
                var gateUI = go.GetComponentInChildren<GateUI>();
                if (gateUI != null)
                {
                    gateUI.Setup(gateData.RequiredShadows);
                }
            }
            entities.Dispose();
        }

        if (!_shadowMissingVisualQuery.IsEmpty)
        {
            var entities = _shadowMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.ShadowVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });

                var animators = go.GetComponentsInChildren<Animator>(true);
                if (animators != null && animators.Length > 0)
                {
                    foreach (var anim in animators)
                    {
                        if (anim.GetComponent<AnimationEventReceiver>() == null)
                            anim.gameObject.AddComponent<AnimationEventReceiver>();
                    }
                    EntityManager.AddComponentObject(entity, new AnimatorModel { Value = animators[0], Animators = animators });
                }
                EntityManager.AddComponentData(entity, new VisualAnimationState());

                // RendererModel 추가
                var renderers = go.GetComponentsInChildren<Renderer>();
                var colors = new Color[renderers.Length];
                var blocks = new MaterialPropertyBlock[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    blocks[i] = new MaterialPropertyBlock();
                    renderers[i].GetPropertyBlock(blocks[i]);

                    if (renderers[i].sharedMaterial != null)
                    {
                        if (renderers[i].sharedMaterial.HasProperty("_BaseColor"))
                            colors[i] = renderers[i].sharedMaterial.GetColor("_BaseColor");
                        else if (renderers[i].sharedMaterial.HasProperty("_Color"))
                            colors[i] = renderers[i].sharedMaterial.color;
                        else
                            colors[i] = Color.white;
                    }
                    else
                    {
                        colors[i] = Color.white;
                    }
                }
                EntityManager.AddComponentObject(entity, new VisualRendererModel { Renderers = renderers, OriginalColors = colors, PropertyBlocks = blocks });
            }
            entities.Dispose();
        }

        if (!_itemMissingVisualQuery.IsEmpty)
        {
            var entities = _itemMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var itemData = EntityManager.GetComponentData<DroppedItemData>(entity);
                GameObject prefab = null;
                switch (itemData.Type)
                {
                    case DropItemType.Exp:
                        prefab = VisualManager.Instance.ExpVisualPrefab;
                        break;
                    case DropItemType.Gold:
                        prefab = VisualManager.Instance.GoldVisualPrefab;
                        break;
                    case DropItemType.Magnet:
                        prefab = VisualManager.Instance.MagnetVisualPrefab;
                        break;
                    case DropItemType.Bomb:
                        prefab = VisualManager.Instance.BombVisualPrefab;
                        break;
                }
                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, pos, rot);
                    EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
                }
            }
            entities.Dispose();
        }

        if (!_effectMissingVisualQuery.IsEmpty)
        {
            var entities = _effectMissingVisualQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var effectInfo = EntityManager.GetComponentData<EffectVisualInfo>(entity);
                if (VisualManager.Instance.EffectVisualPrefabs != null && effectInfo.PrefabID >= 0 && effectInfo.PrefabID < VisualManager.Instance.EffectVisualPrefabs.Length)
                {
                    GameObject prefab = VisualManager.Instance.EffectVisualPrefabs[effectInfo.PrefabID];
                    if (prefab != null)
                    {
                        var go = Object.Instantiate(prefab, pos, rot);
                        EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
                    }
                }
            }
            entities.Dispose();
        }

        foreach (var (transform, visualModel, entity) in SystemAPI.Query<RefRO<LocalTransform>, SubSceneVisualModel>().WithEntityAccess())
        {
            if (visualModel != null && visualModel.Value != null)
            {
                Vector3 targetPos = transform.ValueRO.Position;

                // 아이템이 아닌 전투 개체(적, 보스, 그림자 등)의 비주얼은 모두 바닥(y=0)에 붙이도록 y값을 고정합니다.
                if (!SystemAPI.HasComponent<DroppedItemData>(entity))
                {
                    targetPos.y = 0f;
                }

                // 실제 ECS Transform 위치와 비주얼 오브젝트의 위치 간 거리 확인
                float distanceSq = (visualModel.Value.position - targetPos).sqrMagnitude;

                // 거리가 너무 멀면 (예: 부활, 텔레포트) Lerp 없이 즉각 이동하여 잔상 방지
                if (distanceSq > 25f) 
                {
                    visualModel.Value.position = targetPos;
                    visualModel.Value.rotation = transform.ValueRO.Rotation;
                }
                else
                {
                    visualModel.Value.position = Vector3.Lerp(visualModel.Value.position, targetPos, deltaTime * 20f);
                    visualModel.Value.rotation = Quaternion.Slerp(visualModel.Value.rotation, transform.ValueRO.Rotation, deltaTime * 20f);
                }
            }
        }
        
        // 게이트 UI 갱신 시스템
        foreach (var (gateData, visualModel) in SystemAPI.Query<RefRO<GateData>, SubSceneVisualModel>())
        {
            if (visualModel != null && visualModel.Value != null)
            {
                var gateUI = visualModel.Value.GetComponentInChildren<GateUI>();
                if (gateUI != null)
                {
                    gateUI.UpdateAbsorbed(gateData.ValueRO.AbsorbedShadows);
                }
            }
        }
    }
}
#endregion

#region ItemVisual
[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct ItemVisualSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float time = (float)SystemAPI.Time.ElapsedTime;

        // 드랍된 아이템들만 찾아서 돌리기
        foreach (var (transform, itemData) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<DroppedItemData>>())
        {
            transform.ValueRW.Rotation = quaternion.RotateY(time * 2f);
            transform.ValueRW.Position.y = 0.5f + math.sin(time * 5f) * 0.1f;
        }
    }
}
#endregion

#region VisualCleanup
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(CleanupDestroyedEntitySystem))]
public partial class VisualCleanupSystem : SystemBase
{
    private EntityQuery _cleanupQuery;

    protected override void OnCreate()
    {
        _cleanupQuery = SystemAPI.QueryBuilder().WithAll<SubSceneVisualModel, DestroyEntityTag>().Build();
    }

    protected override void OnUpdate()
    {
        if (_cleanupQuery.IsEmpty) return;

        var entities = _cleanupQuery.ToEntityArray(Allocator.TempJob);

        foreach (var entity in entities)
        {
            var visualModel = EntityManager.GetComponentObject<SubSceneVisualModel>(entity);

            if (visualModel != null && visualModel.Value != null)
            {
                UnityEngine.Object.Destroy(visualModel.Value.gameObject);
            }
            EntityManager.RemoveComponent<SubSceneVisualModel>(entity);
        }
        entities.Dispose();
    }
}
#endregion

#region VisualAnimationSync
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(VisualSyncSystem))]

public partial class VisualAnimationSyncSystem : SystemBase
{
    private ComponentLookup<Unity.Physics.PhysicsVelocity> _physicsVelocityLookup;

    protected override void OnCreate()
    {
        _physicsVelocityLookup = SystemAPI.GetComponentLookup<Unity.Physics.PhysicsVelocity>(true);
    }

    protected override void OnUpdate()
    {
        _physicsVelocityLookup.Update(this);
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (animModel, animState, entity) in SystemAPI.Query<AnimatorModel, RefRW<VisualAnimationState>>().WithEntityAccess())
        {
            if (animModel.Animators == null || animModel.Animators.Length == 0) continue;

            float currentSpeed = 0f;
            if (_physicsVelocityLookup.HasComponent(entity))
            {
                var vel = _physicsVelocityLookup[entity].Linear;
                vel.y = 0f;
                currentSpeed = math.length(vel);
            }

            animState.ValueRW.Speed = math.lerp(animState.ValueRO.Speed, currentSpeed, dt * 15f);

            bool triggerHit = animState.ValueRW.TriggerHit;
            bool triggerSummon = animState.ValueRW.TriggerSummon;
            bool triggerAttack = animState.ValueRW.TriggerAttack;
            bool isDead = animState.ValueRO.IsDead;
            bool isBoss = EntityManager.HasComponent<BossTag>(entity);
            int attackIdx = animState.ValueRO.AttackIndex;

            foreach (var animator in animModel.Animators)
            {
                if (animator == null) continue;

                animator.SafeSetFloat("Speed", animState.ValueRW.Speed);

                if (triggerHit) animator.SafeSetTrigger("Hit");
                if (triggerSummon) animator.SafeSetTrigger("Summon");

                if (triggerAttack)
                {
                    if (isBoss)
                    {
                        animator.SafeSetInteger("Index", attackIdx);
                        if (animator == animModel.Value)
                        {
                            var eventReceiver = animator.GetComponent<AnimationEventReceiver>();
                            if (eventReceiver != null && VisualManager.Instance != null && EntityManager.HasComponent<SubSceneVisualModel>(entity))
                            {
                                float hitTime = eventReceiver.GetTimeToHitEvent();
                                var visualModel = EntityManager.GetComponentObject<SubSceneVisualModel>(entity);
                                VisualManager.Instance.SpawnTelegraph(visualModel.Value, attackIdx, hitTime);
                            }
                        }
                    }
                    animator.SafeSetTrigger("Attack");
                }

                animator.SafeSetBool("IsDead", isDead);

                var eventReceiverUpdate = animator.GetComponent<AnimationEventReceiver>();
                if (eventReceiverUpdate != null)
                {
                    if (eventReceiverUpdate.EventAttackHit)
                    {
                        animState.ValueRW.EventAttackHit = true;
                        eventReceiverUpdate.EventAttackHit = false;
                    }
                    if (eventReceiverUpdate.EventAttackEnd)
                    {
                        animState.ValueRW.EventAttackEnd = true;
                        eventReceiverUpdate.EventAttackEnd = false;
                    }
                }
            }

            if (triggerHit) animState.ValueRW.TriggerHit = false;
            if (triggerSummon) animState.ValueRW.TriggerSummon = false;
            if (triggerAttack) animState.ValueRW.TriggerAttack = false;
        }
    }
}
  #endregion

public static class AnimatorParameterExtensions
{
    private static System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>> _controllerParamCache = 
        new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>();

    public static bool SafeHasParameter(this UnityEngine.Animator animator, string paramName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        int controllerId = animator.runtimeAnimatorController.GetInstanceID();
        if (!_controllerParamCache.TryGetValue(controllerId, out var set))
        {
            set = new System.Collections.Generic.HashSet<int>();
            foreach (var param in animator.parameters)
            {
                set.Add(param.nameHash);
            }
            _controllerParamCache[controllerId] = set;
        }
        return set.Contains(UnityEngine.Animator.StringToHash(paramName));
    }
    
    public static void SafeSetFloat(this UnityEngine.Animator animator, string name, float value)
    {
        if (animator.SafeHasParameter(name)) animator.SetFloat(name, value);
    }
    
    public static void SafeSetTrigger(this UnityEngine.Animator animator, string name)
    {
        if (animator.SafeHasParameter(name)) animator.SetTrigger(name);
    }
    
    public static void SafeSetBool(this UnityEngine.Animator animator, string name, bool value)
    {
        if (animator.SafeHasParameter(name)) animator.SetBool(name, value);
    }
    
    public static void SafeSetInteger(this UnityEngine.Animator animator, string name, int value)
    {
        if (animator.SafeHasParameter(name)) animator.SetInteger(name, value);
    }
}

