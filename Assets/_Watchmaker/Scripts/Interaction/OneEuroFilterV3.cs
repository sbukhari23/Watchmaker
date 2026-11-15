using UnityEngine;

namespace Watchmaker.Interaction
{
    /// <summary>One Euro filter for Vector3: heavy smoothing when still, low lag when moving.</summary>
    public sealed class OneEuroFilterV3
    {
        public float MinCutoff;
        public float Beta;
        public float DCutoff;

        Vector3 _x, _dx;
        bool _initialised;

        public OneEuroFilterV3(float minCutoff, float beta, float dCutoff)
        {
            MinCutoff = minCutoff; Beta = beta; DCutoff = dCutoff;
        }

        public void Reset(Vector3 value)
        {
            _x = value; _dx = Vector3.zero; _initialised = true;
        }

        public Vector3 Step(Vector3 value, float dt)
        {
            if (!_initialised || dt <= 0f) { Reset(value); return value; }
            Vector3 dx = (value - _x) / dt;
            _dx = Vector3.Lerp(_dx, dx, Alpha(DCutoff, dt));
            float cutoff = MinCutoff + Beta * _dx.magnitude;
            _x = Vector3.Lerp(_x, value, Alpha(cutoff, dt));
            return _x;
        }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}
