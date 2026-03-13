using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    class Baker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
        }
    }
}