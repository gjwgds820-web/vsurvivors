using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace VSurvivors.Battle.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial struct ApplyCollisionFilterSystem : ISystem
    {
        private EntityQuery _filterQuery;

        // OnCreate에서는 typeof()와 같은 참조 관리 코드(Managed Code)가 쓰이므로 BurstCompile을 제외해야 합니다.
        public void OnCreate(ref SystemState state)
        {
            // 빌더를 통해 쿼리를 생성하고, 변경 필터(ChangedVersionFilter)를 수동으로 적용합니다.
            _filterQuery = SystemAPI.QueryBuilder()
                .WithAll<CustomCollisionFilter>()
                .WithAllRW<PhysicsCollider>()
                .Build();
            
            _filterQuery.SetChangedVersionFilter(typeof(CustomCollisionFilter));
            state.RequireForUpdate(_filterQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new ApplyFilterJob();
            // Source Generator가 아니라 명시적 쿼리를 넘겨주어 스케줄링합니다.
            state.Dependency = job.ScheduleParallel(_filterQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ApplyFilterJob : IJobEntity
    {
        public void Execute(in CustomCollisionFilter filterData, ref PhysicsCollider collider)
        {
            unsafe
            {
                var colliderPtr = collider.ColliderPtr;
                if (colliderPtr != null)
                {
                    colliderPtr->SetCollisionFilter(filterData.Value);
                }
            }
        }
    }
}