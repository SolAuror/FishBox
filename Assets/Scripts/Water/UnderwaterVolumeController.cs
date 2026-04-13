using UnityEngine;

/// <summary>
/// ---------------------------------------------------------------------------
/// UNDERWATER VOLUME CONTROLLER
/// ---------------------------------------------------------------------------
///
/// Attach to ANY persistent GameObject (player root, InputManager, etc.).
///
/// Each frame it finds the active camera (supports Cinemachine Brain) and
/// checks whether it is below the animated wave surface of the nearest
/// WaterVolume. Two global shader properties are updated:
///
///   _UnderwaterFactor   0 = fully above water      1 = fully submerged
///   _UnderwaterDepth    metres the camera is below the surface (clamped = 0)
///
/// For third-person cameras that orbit above the water while the player is
/// submerged, an optional playerTransform reference is provided. When assigned,
/// the controller checks BOTH the camera position AND the player position —
/// whichever is deeper underwater drives the effect. This prevents the
/// underwater overlay from disappearing just because the orbit camera is
/// above the surface.
///
/// SETUP:
///   1. Add to any GameObject that persists while playing.
///   2. Optionally assign trackedCamera — if left null, Camera.main is used
///      each frame (which is the Cinemachine Brain output camera).
///   3. Optionally assign playerTransform for third-person support.
///   4. Add UnderwaterRendererFeature to your URP Renderer Asset.
/// ---------------------------------------------------------------------------
/// </summary>
public class UnderwaterVolumeController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera to track. If null, Camera.main is used each frame " +
             "(works with Cinemachine Brain).")]
    public Camera trackedCamera;

    [Header("Third-Person Support")]
    [Tooltip("Player root transform. When assigned the controller also checks " +
             "if the player (not just the camera) is underwater, so the overlay " +
             "works even if the orbit camera is above the surface.")]
    public Transform playerTransform;

    [Header("Transition")]
    [Tooltip("How fast _UnderwaterFactor blends when the camera crosses the surface.")]
    public float blendSpeed = 10f;

    [Tooltip("Depth below the surface (metres) at which _UnderwaterFactor reaches 1.")]
    public float fullSubmersionDepth = 0.4f;

    [Tooltip("Extra depth offset added when computing how far the camera is below the surface. " +
             "A small positive value (e.g. 0.15) closes the visual gap between the water mesh " +
             "and where underwater FX begin to appear.")]
    public float surfaceEnterBias = 0.15f;

    [Header("Debug")]
    [Tooltip("Enable to print pipeline diagnostics every second to the Console.")]
    public bool debugLog = true;

    // -----------------------------------------------------------------------
    float _underwaterFactor;
    float _debugTimer;

    static readonly int _SID_UnderwaterFactor = Shader.PropertyToID("_UnderwaterFactor");
    static readonly int _SID_UnderwaterDepth  = Shader.PropertyToID("_UnderwaterDepth");

    // -----------------------------------------------------------------------

    void OnDisable()
    {
        Shader.SetGlobalFloat(_SID_UnderwaterFactor, 0f);
        Shader.SetGlobalFloat(_SID_UnderwaterDepth,  0f);
        _underwaterFactor = 0f;
    }

    void LateUpdate()
    {
        // Resolve the active camera every frame so Cinemachine camera swaps
        // and Brain output changes are handled automatically.
        Camera cam = trackedCamera != null ? trackedCamera : Camera.main;

        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

        float targetFactor    = 0f;
        float underwaterDepth = 0f;

        string debugSource = "none";

        // --- Check camera position ---
        if (cam != null)
        {
            var vol = WaterVolume.FindVolume(camPos);
            if (vol != null)
            {
                float surface   = vol.GetSurfaceHeight(camPos);
                // surfaceEnterBias shifts the effective surface slightly upward so FX
                // begin to appear just as the camera reaches the visual water surface,
                // closing the perceptible gap between mesh and effect onset.
                underwaterDepth = (surface + surfaceEnterBias) - camPos.y;

                if (underwaterDepth > 0f)
                {
                    targetFactor = Mathf.Clamp01(underwaterDepth / Mathf.Max(fullSubmersionDepth, 0.01f));
                    debugSource = $"camera (depth={underwaterDepth:F2})";
                }
            }
        }

        // --- Third-person fallback: check player position ---
        // Only applies when the camera is within the water volume's XZ footprint AND
        // within fullSubmersionDepth above the surface. This prevents the orbit camera
        // being several metres above the water from triggering underwater FX just because
        // the player's feet are submerged.
        bool cameraIsNearSurface = false;
        if (cam != null)
        {
            var camXZVol = WaterVolume.FindVolumeXZ(camPos);
            if (camXZVol != null)
            {
                float camSurf = camXZVol.GetSurfaceHeight(camPos);
                cameraIsNearSurface = (camPos.y - camSurf) < fullSubmersionDepth;
            }
        }

        if (playerTransform != null && targetFactor < 1f && cameraIsNearSurface)
        {
            Vector3 playerPos = playerTransform.position;
            var playerVol = WaterVolume.FindVolume(playerPos);
            if (playerVol != null)
            {
                float playerSurface = playerVol.GetSurfaceHeight(playerPos);
                float playerDepth   = playerSurface - playerPos.y;

                if (playerDepth > underwaterDepth)
                {
                    underwaterDepth = playerDepth;
                    float playerFactor = Mathf.Clamp01(playerDepth / Mathf.Max(fullSubmersionDepth, 0.01f));
                    if (playerFactor > targetFactor)
                    {
                        targetFactor = playerFactor;
                        debugSource = $"player (depth={playerDepth:F2})";
                    }
                }
            }
        }

        _underwaterFactor = Mathf.Lerp(_underwaterFactor, targetFactor, blendSpeed * Time.deltaTime);
        if (_underwaterFactor < 0.002f) _underwaterFactor = 0f;

        Shader.SetGlobalFloat(_SID_UnderwaterFactor, _underwaterFactor);
        Shader.SetGlobalFloat(_SID_UnderwaterDepth,  Mathf.Max(underwaterDepth, 0f));

        // --- Debug logging (once per second) ---
        if (debugLog)
        {
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= 1f)
            {
                _debugTimer = 0f;
                int volumeCount = WaterVolume.VolumeCount;
                bool hasCam = cam != null;
                bool hasPlayer = playerTransform != null;
                string camName = hasCam ? cam.name : "NULL";
                string playerName = hasPlayer ? playerTransform.name : "NULL";
                Debug.Log($"[UnderwaterDebug] " +
                    $"volumes={volumeCount} | " +
                    $"cam={camName} pos={camPos:F1} | " +
                    $"player={playerName} pos={(hasPlayer ? playerTransform.position.ToString("F1") : "N/A")} | " +
                    $"source={debugSource} | " +
                    $"targetFactor={targetFactor:F3} | " +
                    $"_UnderwaterFactor={_underwaterFactor:F3}");
            }
        }
    }
}
