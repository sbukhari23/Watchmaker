using UnityEngine;
using Watchmaker.Core;

namespace Watchmaker.Mechanics
{
    /// <summary>Poke the lever to swing it open/closed; the loupe magnifies the movement while open.</summary>
    public sealed class LoupeLever : MonoBehaviour
    {
        [SerializeField] Transform _handle;
        [SerializeField] float _pokeRadius = 0.05f;
        [SerializeField] float _openAngle = 60f;
        [SerializeField] float _swingSpeed = 220f;

        PokeState _poke;
        Quaternion _baseRotation;
        float _angle;
        float _target;

        public bool IsOn { get; private set; }

        public void Configure(Transform handle, float pokeRadius) { _handle = handle; _pokeRadius = pokeRadius; }

        void Awake() => _baseRotation = transform.localRotation;

        void Update()
        {
            if (_handle != null && _poke.Update(_handle.position, _pokeRadius, 0.9f)) Toggle();
            _angle = Mathf.MoveTowards(_angle, _target, _swingSpeed * Time.deltaTime);
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, _angle, 0f);
        }

        public void Toggle()
        {
            IsOn = !IsOn;
            _target = IsOn ? _openAngle : 0f;
            FlowEvents.RaiseLoupe(IsOn);
        }
    }
}
