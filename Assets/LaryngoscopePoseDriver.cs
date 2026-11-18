using UnityEngine;

// Moves the virtual laryngoscope from X / Y / Z (and optional rotation) values.
// For now the values come from sliders (Inspector or an in-headset panel).
// Later, your IMU / tracker code just calls SetFromSensor(...) instead.
//
// Put this on the laryngoscope's top-level object (NOT parented under a controller).
public class LaryngoscopePoseDriver : MonoBehaviour
{
    [Tooltip("Offsets are measured along this object's axes (e.g. the mannequin). Leave empty to use world axes.")]
    public Transform reference;

    [Header("Position offset from starting pose (metres)")]
    [Range(-0.3f, 0.3f)] public float x;
    [Range(-0.3f, 0.3f)] public float y;
    [Range(-0.3f, 0.3f)] public float z;

    [Header("Rotation from starting pose (degrees)")]
    public bool driveRotation = true;
    [Range(-180f, 180f)] public float pitch; // about X
    [Range(-180f, 180f)] public float yaw;   // about Y
    [Range(-180f, 180f)] public float roll;  // about Z

    [Header("Smoothing (0 = instant, ~10 = smooth, useful for noisy sensor data)")]
    [Range(0f, 30f)] public float smoothing = 0f;

    Vector3 startPos;    // starting position, in reference space (or world)
    Quaternion startRot; // starting rotation, in reference space (or world)

    void Start()
    {
        if (reference)
        {
            startPos = Quaternion.Inverse(reference.rotation) * (transform.position - reference.position);
            startRot = Quaternion.Inverse(reference.rotation) * transform.rotation;
        }
        else
        {
            startPos = transform.position;
            startRot = transform.rotation;
        }
    }

    void Update()
    {
        Vector3 local = startPos + new Vector3(x, y, z);
        Vector3 targetPos = reference ? reference.position + reference.rotation * local : local;

        Quaternion targetRot = transform.rotation;
        if (driveRotation)
        {
            Quaternion r = startRot * Quaternion.Euler(pitch, yaw, roll);
            targetRot = reference ? reference.rotation * r : r;
        }

        if (smoothing <= 0f)
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, targetPos, t),
                Quaternion.Slerp(transform.rotation, targetRot, t));
        }
    }

    // ---- Hooks for UI sliders (Slider > On Value Changed > Dynamic float) ----
    public void SetX(float v) => x = v;
    public void SetY(float v) => y = v;
    public void SetZ(float v) => z = v;
    public void SetPitch(float v) => pitch = v;
    public void SetYaw(float v) => yaw = v;
    public void SetRoll(float v) => roll = v;

    public void ResetPose()
    {
        x = y = z = 0f;
        pitch = yaw = roll = 0f;
    }

    // ---- For later: call this from your IMU / UDP receiver script ----
    // positionOffset: metres, along the reference axes. eulerDegrees: pitch, yaw, roll.
    public void SetFromSensor(Vector3 positionOffset, Vector3 eulerDegrees)
    {
        x = positionOffset.x; y = positionOffset.y; z = positionOffset.z;
        pitch = eulerDegrees.x; yaw = eulerDegrees.y; roll = eulerDegrees.z;
    }
}
