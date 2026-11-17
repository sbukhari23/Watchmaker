using UnityEngine;
using Watchmaker.Core;

namespace Watchmaker.Mechanics
{
    /// <summary>Wind the crown by circling a fingertip around it. Accumulates angular travel; completes at the required total.</summary>
    public sealed class CrownWinder : MonoBehaviour
    {
        [SerializeField] float _radius = 0.04f;
        [SerializeField] float _requiredDegrees = 720f;

        float _total;
        float _lastAngle;
        float _lastClick;
        bool _tracking;

        public bool Active { get; private set; }
        public bool Done { get; private set; }
        public float Progress => Mathf.Clamp01(_total / _requiredDegrees);

        public void SetActive(bool active) { Active = active && !Done; _tracking = false; }

        void Update()
        {
            if (!Active || Done) return;
            Vector3 tip;
            float scale = transform.lossyScale.x;
            if (HandTracker.TryGetNearestTip(transform.position, out tip) &&
                Vector3.Distance(tip, transform.position) <= _radius * scale)
            {
                Vector3 local = transform.parent.InverseTransformPoint(tip) - transform.localPosition;
                float angle = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
                if (_tracking) AddDegrees(Mathf.Abs(Mathf.DeltaAngle(_lastAngle, angle)));
                _lastAngle = angle;
                _tracking = true;
            }
            else _tracking = false;
        }

        public void AddDegrees(float degrees)
        {
            if (!Active || Done) return;
            _total += degrees;
            transform.localRotation = Quaternion.Euler(0f, -_total, 0f);
            if (_total - _lastClick >= 45f) { _lastClick = _total; FlowEvents.RaiseCrownClicked(transform.position); }
            if (_total >= _requiredDegrees)
            {
                Done = true;
                Active = false;
                FlowEvents.RaiseWatchWound();
            }
        }
    }
}
