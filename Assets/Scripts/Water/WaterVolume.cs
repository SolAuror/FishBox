using System.Collections.Generic;
using UnityEngine;
using Sol.Locomotion;

/// <summary>
/// ---------------------------------------------------------------------------
/// WATER VOLUME
/// ---------------------------------------------------------------------------
///
/// Defines a water body for gameplay purposes:
///   - Replicates the Sol.Water shader wave formula in C# so swimming physics
///     and buoyancy stay synced with the visual surface - no guesswork.
///   - Automatically sets LocomotionController.IsSwimming on any character
///     that enters/exits the trigger collider.
///   - Pushes _WaterSurfaceY as a global shader property for the underwater
///     overlay renderer feature.
///   - Acts as a static registry so any code can query WaterVolume.FindVolume().
///
/// SETUP:
///   1.  Create an empty GameObject at the vertical centre of your water body.
///   2.  Add this component. It auto-adds a BoxCollider and sets isTrigger.
///   3.  Resize the BoxCollider to cover the water body. The TOP FACE must
///       align with the flat water surface plane (no waves yet; waves are
///       added at runtime via GetSurfaceHeight()).
///   4.  Assign the Sol.Water material used on the water mesh so wave
///       parameters are synced automatically.
/// ---------------------------------------------------------------------------
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WaterVolume : MonoBehaviour
{
    [Header("Wave Sync")]
    [Tooltip("Sol.Water material to read wave parameters from. Leave null to fill the wave parameters manually below.")]
    public Material waterMaterial;

    [HideInInspector] public float waveAmplitude = 0.3f;
    [HideInInspector] public float waveFrequency = 1.5f;
    [HideInInspector] public float waveSpeed = 1.0f;
    [HideInInspector] public Vector2 wave1Dir = new Vector2(1f, 0f);
    [HideInInspector] public Vector2 wave2Dir = new Vector2(0.496f, 0.868f);
    [HideInInspector] public float wave2Scale = 0.5f;
    [HideInInspector] public float swellAmplitude = 0.15f;
    [HideInInspector] public float swellSpeed = 0.2f;
    [HideInInspector] public Vector2 swellDir = new Vector2(0.05f, 0.03f);

    [Header("Swimming")]
    [Tooltip("How far above the player's feet the water surface must be before LocomotionController.IsSwimming is set to true.")]
    public float swimDepthThreshold = 1.0f;

    [Tooltip("Extra depth margin before exiting swim state. Prevents flickering at the waterline caused by wave oscillation.")]
    public float swimExitHysteresis = 0.3f;

    [Tooltip("Vertical offset applied to SurfaceY (negative = lower the effective water surface).")]
    public float surfaceOffset = 0f;

    [Header("Global Shader Properties")]
    [Tooltip("Push _WaterSurfaceY to the global shader property each frame.")]
    public bool pushGlobalProperties = true;

    static readonly List<WaterVolume> s_Volumes = new();

    BoxCollider _col;
    readonly List<LocomotionController> _swimmers = new();

    static readonly int _SID_WaterSurfaceY = Shader.PropertyToID("_WaterSurfaceY");

    public static int VolumeCount => s_Volumes.Count;

    public static WaterVolume FindVolume(Vector3 worldPos)
    {
        foreach (var v in s_Volumes)
        {
            if (v == null || v._col == null)
                continue;

            Bounds bounds = v._col.bounds;
            if (worldPos.x < bounds.min.x || worldPos.x > bounds.max.x ||
                worldPos.z < bounds.min.z || worldPos.z > bounds.max.z)
                continue;

            if (worldPos.y < bounds.min.y)
                continue;

            if (worldPos.y < v.GetSurfaceHeight(worldPos))
                return v;
        }

        return null;
    }

    public static WaterVolume FindVolumeXZ(Vector3 worldPos)
    {
        foreach (var v in s_Volumes)
        {
            if (v == null || v._col == null)
                continue;

            Bounds bounds = v._col.bounds;
            if (worldPos.x < bounds.min.x || worldPos.x > bounds.max.x ||
                worldPos.z < bounds.min.z || worldPos.z > bounds.max.z)
                continue;

            return v;
        }

        return null;
    }

    public static float DistanceToNearestWaterXZ(Vector3 worldPos)
    {
        float closest = float.PositiveInfinity;

        for (int i = 0; i < s_Volumes.Count; i++)
        {
            WaterVolume volume = s_Volumes[i];
            if (volume == null || volume._col == null)
                continue;

            Bounds bounds = volume._col.bounds;
            float dx = 0f;
            if (worldPos.x < bounds.min.x)
                dx = bounds.min.x - worldPos.x;
            else if (worldPos.x > bounds.max.x)
                dx = worldPos.x - bounds.max.x;

            float dz = 0f;
            if (worldPos.z < bounds.min.z)
                dz = bounds.min.z - worldPos.z;
            else if (worldPos.z > bounds.max.z)
                dz = worldPos.z - bounds.max.z;

            float distance = Mathf.Sqrt((dx * dx) + (dz * dz));
            if (distance < closest)
                closest = distance;
        }

        return float.IsPositiveInfinity(closest) ? float.MaxValue : closest;
    }

    void OnEnable()
    {
        s_Volumes.Add(this);
        _col = GetComponent<BoxCollider>();

        if (!_col.isTrigger)
        {
            _col.isTrigger = true;
            Debug.LogWarning($"[WaterVolume] {name}: BoxCollider.isTrigger forced to true.", this);
        }

        ReadMaterial();
    }

    void OnDisable()
    {
        s_Volumes.Remove(this);

        foreach (var locomotion in _swimmers)
        {
            if (locomotion != null)
                locomotion.IsSwimming = false;
        }

        _swimmers.Clear();
    }

    void OnValidate()
    {
        ReadMaterial();
    }

    void OnTriggerEnter(Collider other)
    {
        var locomotion = other.GetComponentInParent<LocomotionController>();
        if (locomotion != null && !_swimmers.Contains(locomotion))
            _swimmers.Add(locomotion);
    }

    void OnTriggerExit(Collider other)
    {
        var locomotion = other.GetComponentInParent<LocomotionController>();
        if (locomotion == null)
            return;

        locomotion.IsSwimming = false;
        _swimmers.Remove(locomotion);
    }

    void Update()
    {
        for (int i = _swimmers.Count - 1; i >= 0; i--)
        {
            var locomotion = _swimmers[i];
            if (locomotion == null)
            {
                _swimmers.RemoveAt(i);
                continue;
            }

            float surfaceHeight = GetSurfaceHeight(locomotion.transform.position);
            float depth = surfaceHeight - locomotion.transform.position.y;

            if (locomotion.IsSwimming)
                locomotion.IsSwimming = depth > (swimDepthThreshold - swimExitHysteresis);
            else
                locomotion.IsSwimming = depth > swimDepthThreshold;
        }

        if (pushGlobalProperties)
            Shader.SetGlobalFloat(_SID_WaterSurfaceY, GetSurfaceHeight(transform.position));
    }

    public float SurfaceY
    {
        get
        {
            Vector3 scale = transform.lossyScale;
            return transform.position.y
                   + _col.center.y * scale.y
                   + _col.size.y * 0.5f * scale.y
                   + surfaceOffset;
        }
    }

    public float GetSurfaceHeight(Vector3 worldPos)
    {
        SolWaterManager manager = SolWaterManager.Instance;
        float time = manager != null ? manager.WaveTime : Time.time;
        float windStrength = manager != null ? manager.windStrength : 0f;
        Vector3 windDirection = manager != null ? manager.WindDirectionNormalized : Vector3.right;
        float globalSpeed = manager != null ? Mathf.Max(manager.globalWaveSpeedMultiplier, 0.001f) : 1f;

        Vector2 windXZ = new Vector2(windDirection.x, windDirection.z);
        Vector2 dir1 = (wave1Dir + windXZ * windStrength * 0.3f).normalized;
        Vector2 dir2 = (wave2Dir + windXZ * windStrength * 0.15f).normalized;
        if (dir1.sqrMagnitude <= 0f) dir1 = Vector2.right;
        if (dir2.sqrMagnitude <= 0f) dir2 = new Vector2(0.496f, 0.868f);

        float d1 = dir1.x * worldPos.x + dir1.y * worldPos.z;
        float d2 = dir2.x * worldPos.x + dir2.y * worldPos.z;
        float phase1 = d1 * waveFrequency + time * waveSpeed * globalSpeed;
        float phase2 = d2 * waveFrequency * 1.3f + time * waveSpeed * 0.8f * globalSpeed;

        float amp = waveAmplitude * (1f + windStrength * 0.2f);
        float raw1 = Mathf.Sin(phase1);
        float raw2 = Mathf.Sin(phase2);
        float wave1 = Mathf.Sign(raw1) * Mathf.Pow(Mathf.Abs(raw1), 1.5f) * amp;
        float wave2 = (Mathf.Pow(raw2 * 0.5f + 0.5f, 2f) * 2f - 1f) * amp * wave2Scale;

        float swellPhase = (swellDir.x * worldPos.x + swellDir.y * worldPos.z) + time * swellSpeed;
        float swell = Mathf.Sin(swellPhase) * swellAmplitude;

        return SurfaceY + wave1 + wave2 + swell;
    }

    public bool ContainsPoint(Vector3 worldPos)
    {
        return _col != null && _col.bounds.Contains(worldPos);
    }

    public bool IsUnderwater(Vector3 worldPos)
    {
        return ContainsPoint(worldPos) && worldPos.y < GetSurfaceHeight(worldPos);
    }

    void ReadMaterial()
    {
        if (waterMaterial == null)
            return;

        waveAmplitude = waterMaterial.GetFloat("_WaveAmplitude");
        waveFrequency = waterMaterial.GetFloat("_WaveFrequency");
        waveSpeed = waterMaterial.GetFloat("_WaveSpeed");

        Vector4 w1 = waterMaterial.GetVector("_Wave1Direction");
        Vector4 w2 = waterMaterial.GetVector("_Wave2Direction");

        Vector2 dir1 = new Vector2(w1.x, w1.z);
        Vector2 dir2 = new Vector2(w2.x, w2.z);
        wave1Dir = dir1.sqrMagnitude > 0f ? dir1.normalized : Vector2.right;
        wave2Dir = dir2.sqrMagnitude > 0f ? dir2.normalized : new Vector2(0.496f, 0.868f);

        wave2Scale = waterMaterial.GetFloat("_Wave2Scale");

        if (waterMaterial.HasProperty("_SwellAmplitude"))
            swellAmplitude = waterMaterial.GetFloat("_SwellAmplitude");
        if (waterMaterial.HasProperty("_SwellSpeed"))
            swellSpeed = waterMaterial.GetFloat("_SwellSpeed");
        if (waterMaterial.HasProperty("_SwellDirection"))
        {
            Vector4 swell = waterMaterial.GetVector("_SwellDirection");
            Vector2 dir = new Vector2(swell.x, swell.z);
            swellDir = dir.sqrMagnitude > 0f ? dir.normalized : new Vector2(0.05f, 0.03f);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_col == null)
            _col = GetComponent<BoxCollider>();

        if (_col == null)
            return;

        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.35f);
        Vector3 scale = transform.lossyScale;
        Vector3 centre = transform.position + new Vector3(
            _col.center.x * scale.x,
            _col.center.y * scale.y + _col.size.y * 0.5f * scale.y,
            _col.center.z * scale.z);
        Vector3 size = new Vector3(_col.size.x * scale.x, 0.02f, _col.size.z * scale.z);
        Gizmos.DrawCube(centre, size);

        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireCube(centre, size);
    }
#endif
}
