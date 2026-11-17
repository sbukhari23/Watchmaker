using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Core;

namespace Watchmaker.Mechanics
{
    /// <summary>Magnifies the movement while the loupe is on. Snap distances are measured in the movement's local space, so tolerances scale with it.</summary>
    public sealed class LoupeZoom : MonoBehaviour
    {
        [SerializeField] float _zoom = 1.8f;
        [SerializeField] float _lift = 0.02f;
        [SerializeField] float _speed = 2.5f;

        Transform _movement;
        Vector3 _baseScale;
        Vector3 _basePos;
        float _t;
        float _goal;

        void OnEnable() => FlowEvents.LoupeToggled += OnToggle;
        void OnDisable() => FlowEvents.LoupeToggled -= OnToggle;
        void OnToggle(bool on) => _goal = on ? 1f : 0f;

        void Start()
        {
            if (WatchAssemblyManager.Instance != null) _movement = WatchAssemblyManager.Instance.MovementRoot;
            if (_movement == null) { enabled = false; return; }
            _baseScale = _movement.localScale;
            _basePos = _movement.localPosition;
        }

        void Update()
        {
            _t = Mathf.MoveTowards(_t, _goal, _speed * Time.deltaTime);
            float e = _t * _t * (3f - 2f * _t);
            _movement.localScale = _baseScale * Mathf.Lerp(1f, _zoom, e);
            _movement.localPosition = _basePos + Vector3.up * (_lift * e);
        }
    }
}
