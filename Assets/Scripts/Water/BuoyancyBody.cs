using UnityEngine;

/// <summary>
/// ---------------------------------------------------------------------------
/// BUOYANCY BODY
/// ---------------------------------------------------------------------------
///
/// Attach to any Rigidbody (boats, barrels, crates, debris …) to make it
/// float and rock on the water surface defined by the nearest WaterVolume.
///
/// FEATURES:
///   - Multiple sample points around the object's footprint give realistic
///     tilting and rocking when waves pass underneath.
///   - Upward buoyancy force is proportional to how deeply each point is
///     submerged — deeper = more force, matching Archimedes' principle loosely.
///   - Linear and angular drag increase inside the water volume to simulate
///     the resistance of the water.
///
/// SETUP:
///   1. Add this component alongside a Rigidbody.
///   2. Adjust the sample points to match the object's footprint
///      (visible as gizmos in the Scene view).
///   3. Tune buoyancyForce until the object floats at the right height:
///      - Too low ? object sinks.
///      - Too high ? object bobs aggressively above the surface.
/// ---------------------------------------------------------------------------
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BuoyancyBody : MonoBehaviour
{
    [Header("Buoyancy")]
    [Tooltip("Total upward force (Newtons) applied per fully-submerged sample point. " +
             "Tune until the object floats at the desired height.")]
    public float buoyancyForce = 15f;

    [Header("Water Drag (applied when any sample point is submerged)")]
    public float linearDragInWater  = 3f;
    public float angularDragInWater = 2f;

    [Header("Sample Points")]
    [Tooltip("Local-space positions used to sample the wave surface. " +
             "Four corners of the object's footprint work well for most objects.")]
    public Vector3[] samplePoints = new Vector3[]
    {
        new(-0.5f, 0f,  0.5f),
        new( 0.5f, 0f,  0.5f),
        new(-0.5f, 0f, -0.5f),
        new( 0.5f, 0f, -0.5f),
    };

    // -----------------------------------------------------------------------
    //  Private
    // -----------------------------------------------------------------------

    Rigidbody _rb;
    float     _baseDragLinear;
    float     _baseDragAngular;

    void Awake()
    {
        _rb              = GetComponent<Rigidbody>();
        _baseDragLinear  = _rb.linearDamping;
        _baseDragAngular = _rb.angularDamping;
    }

    void FixedUpdate()
    {
        var vol = WaterVolume.FindVolume(transform.position);

        if (vol == null)
        {
            // Restore defaults when outside every water volume.
            _rb.linearDamping  = _baseDragLinear;
            _rb.angularDamping = _baseDragAngular;
            return;
        }

        _rb.linearDamping  = linearDragInWater;
        _rb.angularDamping = angularDragInWater;

        foreach (var localPt in samplePoints)
        {
            Vector3 worldPt    = transform.TransformPoint(localPt);
            float   surfaceY   = vol.GetSurfaceHeight(worldPt);
            float   submersion = surfaceY - worldPt.y; // positive when submerged

            if (submersion > 0f)
            {
                // Scale force by clamped submersion depth (0–1 m) so objects
                // gently bob at the surface rather than launching out of the water.
                float forceMag = Mathf.Clamp01(submersion) * buoyancyForce;
                _rb.AddForceAtPosition(Vector3.up * forceMag, worldPt, ForceMode.Force);
            }
        }
    }

    // -----------------------------------------------------------------------
    //  Editor gizmos: draw sample point positions
    // -----------------------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (var pt in samplePoints)
            Gizmos.DrawSphere(transform.TransformPoint(pt), 0.06f);
    }
#endif
}
