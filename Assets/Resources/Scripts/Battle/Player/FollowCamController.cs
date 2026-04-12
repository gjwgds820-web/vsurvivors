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
        FindPlayerTarget();
    }

    private void LateUpdate()
    {
        // 타겟이 없으면 매 프레임 찾기 시도 (동적 생성 대응)
        if (_targetTransform == null)
        {
            FindPlayerTarget();
            if (_targetTransform == null) return;
        }

        // 타겟 위치 이동
        transform.position = _targetTransform.position + offset;
    }

    private void FindPlayerTarget()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _targetTransform = playerObj.transform;
        }
    }
}