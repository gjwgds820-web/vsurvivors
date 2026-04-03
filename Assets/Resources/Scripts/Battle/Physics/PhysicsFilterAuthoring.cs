using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace VSurvivors.Battle.Physics
{
    public enum EntityColliderType
    {
        Player,
        Enemy,
        Shadow,
        Structure,
        Hitbox,
        Item
    }

    public class PhysicsFilterAuthoring : MonoBehaviour
    {
        public EntityColliderType ColliderType;

        public class PhysicsFilterBaker : Baker<PhysicsFilterAuthoring>
        {
            public override void Bake(PhysicsFilterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                uint belongsTo = GamePhysicsLayers.None;
                uint collidesWith = GamePhysicsLayers.None;

                switch (authoring.ColliderType)
                {
                    case EntityColliderType.Player:
                        belongsTo = GamePhysicsLayers.Player;
                        collidesWith = GamePhysicsLayers.PlayerMask;  // 플레이어 전용 마스크 (아이템 포함)
                        break;
                    case EntityColliderType.Enemy:
                        belongsTo = GamePhysicsLayers.Enemy;
                        collidesWith = GamePhysicsLayers.EnemyMask;   // 적 전용 마스크
                        break;
                    case EntityColliderType.Shadow:
                        belongsTo = GamePhysicsLayers.Shadow;
                        collidesWith = GamePhysicsLayers.EnemyMask;   // 그림자는 적과 동일 패턴 공유
                        break;
                    case EntityColliderType.Structure:
                        belongsTo = GamePhysicsLayers.Structure;
                        collidesWith = GamePhysicsLayers.StructureMask;
                        break;
                    case EntityColliderType.Hitbox:
                        belongsTo = GamePhysicsLayers.Hitbox;
                        collidesWith = GamePhysicsLayers.HitboxMask;
                        break;
                    case EntityColliderType.Item:
                        belongsTo = GamePhysicsLayers.Item;
                        collidesWith = GamePhysicsLayers.ItemMask;    // 아이템은 플레이어와 구조물과 상호작용
                        break;
                }

                AddComponent(entity, new CustomCollisionFilter
                {
                    Value = new CollisionFilter
                    {
                        BelongsTo = belongsTo,
                        CollidesWith = collidesWith,
                        GroupIndex = 0 
                    }
                });
            }
        }
    }

    public struct CustomCollisionFilter : IComponentData
    {
        public CollisionFilter Value;
    }
}