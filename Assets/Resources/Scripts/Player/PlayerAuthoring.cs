using Unity.Entities;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlayerInput());
            AddComponent(entity, new PlayerMovementData
            {
                MoveSpeed = authoring.moveSpeed,
                RotationSpeed = authoring.rotationSpeed
            });
            AddComponent<CameraTargetTag>(entity);
            AddComponent<SubSceneVisualModel>(entity);
            AddComponent<PhysicsGraphicalInterpolationBuffer>(entity);
        }
    }
}