using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;
using SphereCollider = UnityEngine.SphereCollider;
using MeshCollider = UnityEngine.MeshCollider;

public class MapColliderConverter : MonoBehaviour
{
    public static void ConvertColliders(GameObject root)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var filter = new CollisionFilter
        {
            BelongsTo = VSurvivors.Battle.Physics.GamePhysicsLayers.Structure,
            CollidesWith = VSurvivors.Battle.Physics.GamePhysicsLayers.StructureMask,
            GroupIndex = 0
        };

        var cleanupTracker = root.AddComponent<MapPhysicsCleanupTracker>();

        // 1. Convert Box Colliders
        var boxColliders = root.GetComponentsInChildren<BoxCollider>();
        foreach (var bc in boxColliders)
        {
            if (bc.isTrigger) continue; // Skip triggers for static map physics unless needed

            CreateStaticPhysicsEntity(entityManager, bc.transform, out Entity entity);

            float3 center = bc.center;
            float3 size = bc.size;

            if (size.y < 0.05f)
            {
                size.y = 1.0f;
                center.y -= 0.5f; // 바닥 높이를 초과하지 않도록 아래로 시프트
            }
            size = math.max(size, new float3(0.1f, 0.1f, 0.1f));

            BlobAssetReference<Unity.Physics.Collider> physicsCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = center,
                Size = size,
                Orientation = quaternion.identity
            }, filter);

            entityManager.AddComponentData(entity, new PhysicsCollider { Value = physicsCollider });
            cleanupTracker.Track(entity, physicsCollider);
            
            // Cleanup
            Destroy(bc);
        }

        // Convert Sphere Colliders
        var sphereColliders = root.GetComponentsInChildren<SphereCollider>();
        foreach (var sc in sphereColliders)
        {
            if (sc.isTrigger) continue;

            CreateStaticPhysicsEntity(entityManager, sc.transform, out Entity entity);

            float3 center = sc.center;
            float radius = math.max(0.1f, sc.radius);

            BlobAssetReference<Unity.Physics.Collider> physicsCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = center,
                Radius = radius
            }, filter);

            entityManager.AddComponentData(entity, new PhysicsCollider { Value = physicsCollider });
            cleanupTracker.Track(entity, physicsCollider);

            // Cleanup
            Destroy(sc);
        }

        // 3. Convert Mesh Colliders into bounded Box Colliders automatically
        var meshColliders = root.GetComponentsInChildren<MeshCollider>();
        foreach (var mc in meshColliders)
        {
            if (mc.isTrigger || mc.sharedMesh == null) continue;

            CreateStaticPhysicsEntity(entityManager, mc.transform, out Entity entity);

            // Compute local bounds of the mesh geometry
            Bounds bounds = mc.sharedMesh.bounds;
            float3 center = bounds.center;
            
            // Inflate scale by the transform's local scale so the bounding box matches visuals perfectly
            float3 localScale = mc.transform.localScale;
            float3 size = new float3(bounds.size.x * localScale.x, bounds.size.y * localScale.y, bounds.size.z * localScale.z);

            if (size.y < 0.05f)
            {
                size.y = 1.0f;
                center.y -= 0.5f; // 바닥 높이를 초과하지 않도록 아래로 시프트
            }
            size = math.max(size, new float3(0.1f, 0.1f, 0.1f));

            BlobAssetReference<Unity.Physics.Collider> physicsCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = center,
                Size = size,
                Orientation = quaternion.identity
            }, filter);

            entityManager.AddComponentData(entity, new PhysicsCollider { Value = physicsCollider });
            cleanupTracker.Track(entity, physicsCollider);

            // Cleanup heavy mesh physics component
            Destroy(mc);
        }
    }

    private static void CreateStaticPhysicsEntity(EntityManager entityManager, Transform sourceTransform, out Entity entity)
    {
        entity = entityManager.CreateEntity(
            typeof(LocalToWorld),
            typeof(LocalTransform),
            typeof(PhysicsCollider),
            typeof(PhysicsWorldIndex)
        );

#if UNITY_EDITOR
        entityManager.SetName(entity, sourceTransform.name + "_PhysicsCollider");
#endif

        entityManager.SetComponentData(entity, new LocalTransform
        {
            Position = sourceTransform.position,
            Rotation = sourceTransform.rotation,
            Scale = sourceTransform.localScale.x // Note: Uniform scale assumed for simple conversion
        });
        
        entityManager.SetSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
    }
}

public class MapPhysicsCleanupTracker : MonoBehaviour
{
    private List<Entity> _entities = new List<Entity>();
    private List<BlobAssetReference<Unity.Physics.Collider>> _blobs = new List<BlobAssetReference<Unity.Physics.Collider>>();

    public void Track(Entity entity, BlobAssetReference<Unity.Physics.Collider> blob)
    {
        _entities.Add(entity);
        _blobs.Add(blob);
    }

    private void OnDestroy()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            foreach (var e in _entities)
            {
                if (em.Exists(e))
                {
                    em.DestroyEntity(e);
                }
            }
        }

        foreach (var blob in _blobs)
        {
            if (blob.IsCreated)
            {
                blob.Dispose();
            }
        }

        _entities.Clear();
        _blobs.Clear();
    }
}
