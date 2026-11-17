using UnityEngine;

namespace Watchmaker.UI
{
    /// <summary>Keeps a world-space panel facing the player (yaw only, slight downward tilt).</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        [SerializeField] float _pitch = 12f;

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
