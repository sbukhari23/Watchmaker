using System.Collections.Generic;
using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Core;

namespace Watchmaker.Mechanics
{
    /// <summary>Runs the finished movement: stepped gear train and swinging balance, one beat every interval.</summary>
    public sealed class MovementDriver : MonoBehaviour
    {
        sealed class Spin { public Transform T; public float Step; public float Angle; public float Target; }

        [SerializeField] float _beatInterval = 0.25f;

        readonly List<Spin> _spins = new List<Spin>();
        Transform _balance;
        bool _running;
        float _timer;
        float _clock;
        int _beat;

        public bool Running => _running;

        public void Run()
        {
            _spins.Clear();
            foreach (WatchPart p in Object.FindObjectsByType<WatchPart>(FindObjectsSortMode.None))
            {
                float step;
                switch (p.PartId)
                {
                    case "L1_great_wheel": step = 13.3f; break;
                    case "L1_intermediate_wheel": step = -16.7f; break;
                    case "L1_escape_wheel": step = 24f; break;
                    default: continue;
                }
                Transform v = p.transform.Find("Visual");
                _spins.Add(new Spin { T = v != null ? v : p.transform, Step = step });
            }
            _balance = transform.Find("Balance");
            _running = true;
        }

        void Update()
        {
            if (!_running) return;
            float dt = Time.deltaTime;
            _timer += dt;
            _clock += dt;
            if (_timer >= _beatInterval)
            {
                _timer -= _beatInterval;
                _beat++;
                for (int i = 0; i < _spins.Count; i++) _spins[i].Target += _spins[i].Step;
                FlowEvents.RaiseBeat(_beat);
            }
            for (int i = 0; i < _spins.Count; i++)
            {
                Spin s = _spins[i];
                s.Angle = Mathf.MoveTowards(s.Angle, s.Target, Mathf.Abs(s.Step) / 0.08f * dt);
                if (s.T != null) s.T.localRotation = Quaternion.Euler(0f, s.Angle, 0f);
            }
            if (_balance != null)
                _balance.localRotation = Quaternion.Euler(0f, 110f * Mathf.Sin(_clock * Mathf.PI / _beatInterval), 0f);
        }
    }
}
