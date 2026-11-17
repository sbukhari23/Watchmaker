using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Core;

namespace Watchmaker.Mechanics
{
    /// <summary>Installed screw that is tightened by poking it a few times (hands-only fallback for a screwdriver).</summary>
    [RequireComponent(typeof(WatchPart))]
    public sealed class ScrewTarget : MonoBehaviour
    {
        [SerializeField] int _pokesRequired = 3;
        [SerializeField] float _radius = 0.016f;

        WatchPart _part;
        Transform _visual;
        PokeState _poke;
        int _pokes;

        public bool Active { get; private set; }
        public bool Tightened { get; private set; }
        public int Pokes => _pokes;

        void Awake()
        {
            _part = GetComponent<WatchPart>();
            _visual = transform.Find("Visual");
        }

        public void SetActive(bool active) => Active = active && !Tightened;

        void Update()
        {
            if (!Active || Tightened || _part.State != PartState.Installed) return;
            if (_poke.Update(transform.position, _radius * transform.lossyScale.x)) Poke();
        }

        public void Poke()
        {
            if (Tightened) return;
            _pokes++;
            if (_visual != null)
            {
                _visual.localRotation = Quaternion.Euler(0f, 120f * _pokes, 0f);
                _visual.localPosition += Vector3.down * 0.0004f;
            }
            FlowEvents.RaiseScrewPoked(transform.position);
            if (_pokes >= _pokesRequired)
            {
                Tightened = true;
                Active = false;
                FlowEvents.RaiseScrewTightened(_part);
            }
        }
    }
}
