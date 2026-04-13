using UnityEngine;

/// <summary>
/// Manages interactive water ripples — ring waves that expand outward from
/// objects moving through the water surface.
///
/// Singleton. Maintains a fixed-size ring buffer of ripple events and uploads
/// them to global shader properties every frame so the Sol.Water shader can
/// render concentric ring normals.
///
/// SETUP:
///   1. Add this component to any persistent GameObject (e.g. the same
///      one as SolWaterManager).
///   2. Attach WaterRippleSource to any objects that should create ripples
///      (players, NPCs, boats, barrels, etc.).
///   3. Tune rippleSpeed / rippleFrequency / rippleLifetime to taste.
/// </summary>
[ExecuteAlways]
public class WaterRippleManager : MonoBehaviour
{
    // --- Singleton -------------------------------------------------------
    public static WaterRippleManager Instance { get; private set; }

    // --- Settings --------------------------------------------------------
    [Header("Ripple Settings")]
    [Tooltip("Expansion speed of each ring (world units / second).")]
    [Range(0.5f, 20f)]
    public float rippleSpeed = 5f;

    [Tooltip("Ring density — higher values produce tighter concentric rings.")]
    [Range(1f, 60f)]
    public float rippleFrequency = 20f;

    [Tooltip("Seconds before a ripple fades out completely.")]
    [Range(0.5f, 10f)]
    public float rippleLifetime = 3f;

    // --- Internals -------------------------------------------------------
    const int MaxRipples = 32;

    // Ring buffer: each entry is (worldX, worldZ, birthTime, strength)
    readonly Vector4[] _ripples = new Vector4[MaxRipples];
    int _writeIndex;
    int _activeCount;

    // Shader property IDs
    static readonly int _RipplesID         = Shader.PropertyToID("_Sol_Ripples");
    static readonly int _RippleCountID     = Shader.PropertyToID("_Sol_RippleCount");
    static readonly int _RippleSpeedID     = Shader.PropertyToID("_Sol_RippleSpeed");
    static readonly int _RippleFrequencyID = Shader.PropertyToID("_Sol_RippleFrequency");
    static readonly int _RippleLifetimeID  = Shader.PropertyToID("_Sol_RippleLifetime");

    // --- Lifecycle -------------------------------------------------------

    void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WaterRippleManager] Duplicate instance detected. Destroying this one.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;

        // Clear GPU data so ripples don't persist after disable.
        Shader.SetGlobalInt(_RippleCountID, 0);
    }

    void Update()
    {
        PurgeExpired();
        PushToGPU();
    }

    // --- Public API ------------------------------------------------------

    /// <summary>
    /// Emit a ripple at the given world position.
    /// Call from WaterRippleSource or any gameplay code.
    /// </summary>
    /// <param name="worldPos">World-space position (only XZ is used).</param>
    /// <param name="strength">Normal perturbation strength (0.01–1 typical).</param>
    public void Emit(Vector3 worldPos, float strength = 0.15f)
    {
        _ripples[_writeIndex] = new Vector4(worldPos.x, worldPos.z, Time.time, strength);
        _writeIndex = (_writeIndex + 1) % MaxRipples;
        if (_activeCount < MaxRipples) _activeCount++;
    }

    // --- Internals -------------------------------------------------------

    void PurgeExpired()
    {
        float now = Time.time;
        for (int i = 0; i < MaxRipples; i++)
        {
            if (_ripples[i].w == 0f) continue;
            if (now - _ripples[i].z > rippleLifetime)
            {
                _ripples[i] = Vector4.zero;
                _activeCount = Mathf.Max(0, _activeCount - 1);
            }
        }
    }

    void PushToGPU()
    {
        Shader.SetGlobalVectorArray(_RipplesID, _ripples);
        Shader.SetGlobalInt(_RippleCountID, _activeCount);
        Shader.SetGlobalFloat(_RippleSpeedID, rippleSpeed);
        Shader.SetGlobalFloat(_RippleFrequencyID, rippleFrequency);
        Shader.SetGlobalFloat(_RippleLifetimeID, rippleLifetime);
    }
}
