using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public class FollowCamController : MonoBehaviour
{
    private Transform _targetTransform;
    
    public Vector3 offset = new Vector3(0f, 10f, -15f);
    public float smoothTime = 0.1f;


    private void Start()
    {
        if (_targetTransform == null)
        {
            _targetTransform = GameObject.FindWithTag("Player")?.transform;
        }
    }

    private void LateUpdate()
    {
        // 타겟이 없으면 에러 방지
        if (_targetTransform == null) return;

        // 타겟 위치 이동
        transform.position = _targetTransform.position + offset;
    }
}