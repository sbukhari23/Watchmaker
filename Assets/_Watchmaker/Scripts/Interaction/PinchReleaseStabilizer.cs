using UnityEngine;

namespace Watchmaker.Interaction
{
    /// <summary>Ring buffer of recent world positions so a release can use the pose from just before the pinch opened.</summary>
    public sealed class PinchReleaseStabilizer : MonoBehaviour
    {
        const int Capacity = 16;
        readonly Vector3[] _positions = new Vector3[Capacity];
        readonly float[] _times = new float[Capacity];
        int _head;
        int _count;

        public void Clear() { _head = 0; _count = 0; }

        public void Push(Vector3 worldPosition)
        {
            _positions[_head] = worldPosition;
            _times[_head] = Time.time;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        /// <summary>Position recorded closest to (now - ageSeconds). Falls back to the given value if empty.</summary>
        public Vector3 GetPosition(float ageSeconds, Vector3 fallback)
        {
            if (_count == 0) return fallback;
            float target = Time.time - ageSeconds;
            int best = -1;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - 1 - i + Capacity) % Capacity;
                float d = Mathf.Abs(_times[idx] - target);
                if (d < bestDelta) { bestDelta = d; best = idx; }
            }
            return _positions[best];
        }
    }
}
