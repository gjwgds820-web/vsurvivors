using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct PlayerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, physicsVelocity, physicsMass, input, movement) in 
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRO<PlayerInput>, RefRO<PlayerMovementData>>())
        {
            // 역관성 0으로 강제 세팅 (넘어짐 방지)
            physicsMass.ValueRW.InverseInertia = new float3(0f, 0f, 0f);

            float2 inputMove = input.ValueRO.Move;
            float3 moveDirection = new float3(inputMove.x, 0f, inputMove.y);

            if (math.lengthsq(moveDirection) > 0.01f)
            {
                moveDirection = math.normalize(moveDirection);

                // 위치 이동 적용
                float3 currentVelocity = physicsVelocity.ValueRO.Linear;
                physicsVelocity.ValueRW.Linear = new float3(
                    moveDirection.x * movement.ValueRO.MoveSpeed,
                    currentVelocity.y, 
                    moveDirection.z * movement.ValueRO.MoveSpeed
                );
                
                // 💡 회전 제어 (이 부분에서 직접 Y축 방향을 LookRotation으로 보정합니다)
                // Y축 값이 완전히 0인 완벽한 평면 벡터를 생성
                float3 flatForward = new float3(moveDirection.x, 0f, moveDirection.z);
                
                if (math.lengthsq(flatForward) > 0.001f)
                {
                    flatForward = math.normalize(flatForward);
                    quaternion targetRotation = quaternion.LookRotationSafe(flatForward, math.up());
                    
                    // Slerp를 통해 부드럽게 회전
                    quaternion newRotation = math.slerp(transform.ValueRO.Rotation, targetRotation, movement.ValueRO.RotationSpeed * deltaTime);
                    

                    newRotation.value.x = 0f;
                    newRotation.value.z = 0f;
                    newRotation = math.normalize(newRotation);
                    
                    transform.ValueRW.Rotation = newRotation;
                }
            }
            else
            {
                // 입력이 없을 때
                float3 currentVelocity = physicsVelocity.ValueRO.Linear;
                physicsVelocity.ValueRW.Linear = new float3(0f, currentVelocity.y, 0f);
                physicsVelocity.ValueRW.Angular = float3.zero;

                // 멈춰있을 때도 현재 각도에서 X, Z의 기울어짐을 방지
                quaternion currentRot = transform.ValueRO.Rotation;
                currentRot.value.x = 0f;
                currentRot.value.z = 0f;
                transform.ValueRW.Rotation = math.normalize(currentRot);
            }
        }
    }
}