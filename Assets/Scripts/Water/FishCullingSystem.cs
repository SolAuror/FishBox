using UnityEngine;

public class FishCullingSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera cullCamera;
    [SerializeField] Transform cullTarget;

    [Header("Distances")]
    [SerializeField, Min(0f)] float nearKeepDistance = 80f;
    [SerializeField, Min(0f)] float farCullDistance = 160f;
    [SerializeField, Min(0f)] float offscreenCullDelay = 4f;
    [SerializeField, Min(0.05f)] float cullInterval = 0.5f;
    [SerializeField, Min(1)] int maxFishCheckedPerPass = 64;

    Transform _resolvedCullTarget;
    float _nextCullTime;

    void OnValidate()
    {
        farCullDistance = Mathf.Max(nearKeepDistance, farCullDistance);
        cullInterval = Mathf.Max(0.05f, cullInterval);
        maxFishCheckedPerPass = Mathf.Max(1, maxFishCheckedPerPass);
    }

    void Update()
    {
        if (Time.time < _nextCullTime)
            return;

        _nextCullTime = Time.time + cullInterval;

        Camera camera = cullCamera != null ? cullCamera : Camera.main;
        Transform target = ResolveCullTarget(camera);
        FishVolume.RunHybridCulling(
            camera,
            target,
            nearKeepDistance,
            farCullDistance,
            offscreenCullDelay,
            maxFishCheckedPerPass);
    }

    Transform ResolveCullTarget(Camera camera)
    {
        if (cullTarget != null)
            return cullTarget;

        if (_resolvedCullTarget != null)
            return _resolvedCullTarget;

        try
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _resolvedCullTarget = playerObj.transform;
                return _resolvedCullTarget;
            }
        }
        catch (UnityException)
        {
            // Camera fallback keeps the system usable in scenes without a Player tag.
        }

        return camera != null ? camera.transform : null;
    }
}
