using Unity.Entities;
using UnityEngine;

public class VisualInstanceObject : IComponentData
{
    public GameObject Value;
    public Transform Transform;
}

public class SubSceneVisualModel : IComponentData
{
    public Transform Value;
}