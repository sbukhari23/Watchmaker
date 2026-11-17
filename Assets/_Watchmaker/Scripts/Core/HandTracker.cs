using Oculus.Interaction.Input;
using UnityEngine;

namespace Watchmaker.Core
{
    /// <summary>Index fingertip positions for poke interactions. Reads Meta Interaction SDK hands; no per-frame allocations.</summary>
    public static class HandTracker
    {
        static Hand[] _hands = new Hand[0];
        static float _nextScan;
        static int _frame = -1;
        static readonly Vector3[] Tips = new Vector3[2];
        static int _tipCount;
        public static bool HasVirtual;
        public static Vector3 VirtualTip;

        static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1.5f;
                _hands = Object.FindObjectsByType<Hand>(FindObjectsSortMode.None);
            }
            _tipCount = 0;
            if (HasVirtual) Tips[_tipCount++] = VirtualTip;
            for (int i = 0; i < _hands.Length && _tipCount < Tips.Length; i++)
            {
                Hand h = _hands[i];
                if (h == null || !h.isActiveAndEnabled || !h.IsConnected) continue;
                Pose pose;
                if (h.GetJointPose(HandJointId.HandIndexTip, out pose)) Tips[_tipCount++] = pose.position;
            }
        }

        public static float NearestDistance(Vector3 point)
        {
            Refresh();
            float best = float.MaxValue;
            for (int i = 0; i < _tipCount; i++)
            {
                float d = Vector3.Distance(Tips[i], point);
                if (d < best) best = d;
            }
            return best;
        }

        public static bool TryGetNearestTip(Vector3 point, out Vector3 tip)
        {
            Refresh();
            tip = Vector3.zero;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _tipCount; i++)
            {
                float d = Vector3.Distance(Tips[i], point);
                if (d < best) { best = d; tip = Tips[i]; found = true; }
            }
            return found;
        }
    }

    /// <summary>Edge-triggered "fingertip enters radius" detector with cooldown.</summary>
    public struct PokeState
    {
        bool _inside;
        float _last;

        public bool Update(Vector3 point, float radius, float cooldown = 0.35f)
        {
            bool now = HandTracker.NearestDistance(point) <= radius;
            bool fired = now && !_inside && Time.time - _last >= cooldown;
            if (fired) _last = Time.time;
            _inside = now;
            return fired;
        }
    }
}
