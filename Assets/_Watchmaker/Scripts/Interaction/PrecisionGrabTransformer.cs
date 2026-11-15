using Oculus.Interaction;
using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Core;

namespace Watchmaker.Interaction
{
    /// <summary>
    /// ISDK one-grab transformer for watch parts: filtered pinch position, reduced gain near the correct socket,
    /// rotation locked flat to the movement plate, and release stabilisation.
    /// </summary>
    [RequireComponent(typeof(WatchPart), typeof(PinchReleaseStabilizer))]
    public sealed class PrecisionGrabTransformer : MonoBehaviour, ITransformer
    {
        [SerializeField] float _minCutoff = 0.8f;
        [SerializeField] float _beta = 20f;
        [SerializeField] float _dCutoff = 1f;
        [SerializeField] float _nearGain = 0.5f;
        [SerializeField] float _releaseLookback = 0.08f;

        IGrabbable _grabbable;
        WatchPart _part;
        PinchReleaseStabilizer _stabilizer;
        OneEuroFilterV3 _filter;
        Vector3 _handAnchor;
        Vector3 _partAnchor;
        float _gain = 1f;

        void Awake()
        {
            _part = GetComponent<WatchPart>();
            _stabilizer = GetComponent<PinchReleaseStabilizer>();
            _filter = new OneEuroFilterV3(_minCutoff, _beta, _dCutoff);
        }

        public void Initialize(IGrabbable grabbable) => _grabbable = grabbable;

        public void BeginTransform()
        {
            Vector3 hand = _grabbable.GrabPoints[0].position;
            _filter.Reset(hand);
            _handAnchor = hand;
            _partAnchor = _grabbable.Transform.position;
            _gain = 1f;
            _stabilizer.Clear();
            _part.OnGrabbed();
            GameEvents.RaisePartGrabbed(_part);
        }

        public void UpdateTransform()
        {
            Transform target = _grabbable.Transform;
            Vector3 hand = _filter.Step(_grabbable.GrabPoints[0].position, Time.deltaTime);

            WatchAssemblyManager assembly = WatchAssemblyManager.Instance;
            float gain = assembly != null && assembly.IsNearCorrectSocket(_part, target.position) ? _nearGain : 1f;
            if (!Mathf.Approximately(gain, _gain))
            {
                _partAnchor = target.position;
                _handAnchor = hand;
                _gain = gain;
            }

            target.position = _partAnchor + _gain * (hand - _handAnchor);
            if (assembly != null) target.rotation = assembly.MovementRoot.rotation;

            _stabilizer.Push(target.position);
            if (assembly != null) assembly.UpdateHeldFeedback(_part, target.position);
        }

        public void EndTransform()
        {
            Transform target = _grabbable.Transform;
            Vector3 releasePosition = _stabilizer.GetPosition(_releaseLookback, target.position);
            GameEvents.RaisePartReleased(_part);

            WatchAssemblyManager assembly = WatchAssemblyManager.Instance;
            if (assembly != null) assembly.ResolveRelease(_part, releasePosition);
            else _part.ReturnToTray();
        }
    }
}
