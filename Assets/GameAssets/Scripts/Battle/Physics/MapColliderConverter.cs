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

        // 1. Convert Box Colliders
        var boxColliders = root.GetComponentsInChildren<BoxCollider>();
        foreach (var bc in boxColliders)
        {
            if (bc.isTrigger) continue; // Skip triggers for static map physics unless needed

            CreateStaticPhysicsEntity(entityManager, bc.transform, out Entity entity);

            float3 center = bc.center;
            float3 size = bc.size;

            BlobAssetReference<Unity.Physics.Collider> physicsCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = center,
                Size = size,
                Orientation = quaternion.identity
            });

            entityManager.AddComponentData(entity, new PhysicsCollider { Value = physicsCollider });
            
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
            float radius = sc.radius;

            BlobAssetReference<Unity.Physics.Collider> physicsCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = center,
                Radius = radius
            });

            entityManager.AddComponentData(entity, new PhysicsCollider { Value = physicsCollider });

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
            float3 size = bounds.size;

            BlobAssetReference<Unity.Physics.Collider> physicsCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = center,
                Size = size,
                Orientation = quaternion.identity
            });

            entityManager.AddComponentData(entity, new PhysicsCollider { Value = physicsCollider });

            // Cleanup heavy mesh physics component
            Destroy(mc);
        }
    }

    private static void CreateStaticPhysicsEntity(EntityManager entityManager, Transform sourceTransform, out Entity entity)
    {
        entity = entityManager.CreateEntity(
            typeof(LocalToWorld),
            typeof(LocalTransform),
            typeof(PhysicsCollider)
        );

        entityManager.SetComponentData(entity, new LocalTransform
        {
            Position = sourceTransform.position,
            Rotation = sourceTransform.rotation,
            Scale = sourceTransform.localScale.x // Note: Uniform scale assumed for simple conversion
        });
    }
}
