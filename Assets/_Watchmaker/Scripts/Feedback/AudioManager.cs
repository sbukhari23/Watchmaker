using System;
using System.Collections;
using UnityEngine;
using Watchmaker.Assembly;

namespace Watchmaker.Core
{
    /// <summary>Procedural sound effects (no imported audio): generated once at startup, played from a small 3D source pool.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int SampleRate = 24000;
        const int PoolSize = 8;

        static readonly System.Random Rng = new System.Random(1234);

        AudioClip _click, _snap, _buzz, _tick, _tock, _whoosh, _ratchet, _chime, _ambient;
        AudioSource[] _pool;
        AudioSource _ambientSource;
        int _next;

        static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float seconds, Func<float, float> gen)
        {
            int n = Mathf.CeilToInt(SampleRate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(gen(i / (float)SampleRate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        void Awake()
        {
            _click = Make("click", 0.05f, t => (Noise() * 0.5f + Sine(1800f, t) * 0.5f) * Mathf.Exp(-t * 110f) * 0.6f);
            _snap = Make("snap", 0.25f, t => (Sine(190f, t) * Mathf.Exp(-t * 18f) + 0.6f * Sine(760f, t) * Mathf.Exp(-t * 30f) + 0.5f * Noise() * Mathf.Exp(-t * 90f)) * 0.7f);
            _buzz = Make("buzz", 0.28f, t => Mathf.Sign(Sine(130f - 60f * t, t)) * 0.22f * Mathf.Exp(-t * 6f));
            _tick = Make("tick", 0.04f, t => (Sine(3400f, t) * 0.6f + Noise() * 0.4f) * Mathf.Exp(-t * 240f) * 0.5f);
            _tock = Make("tock", 0.045f, t => (Sine(2300f, t) * 0.6f + Noise() * 0.4f) * Mathf.Exp(-t * 220f) * 0.5f);
            float lp = 0f;
            _whoosh = Make("whoosh", 0.6f, t => { lp += (Noise() - lp) * 0.12f; return lp * Mathf.Sin(Mathf.PI * t / 0.6f) * 1.8f; });
            _ratchet = Make("ratchet", 0.12f, t =>
            {
                float tt = t % 0.04f;
                return (Noise() * Mathf.Exp(-tt * 300f) * 0.5f + Sine(2200f, tt) * Mathf.Exp(-tt * 200f) * 0.4f);
            });
            _chime = Make("chime", 1.0f, t => (Sine(880f, t) + 0.6f * Sine(1318.5f, t) + 0.4f * Sine(1760f, t)) * Mathf.Exp(-t * 4f) * 0.25f);
            float ap = 0f;
            _ambient = Make("ambient", 4f, t => { ap += (Noise() - ap) * 0.015f; return ap * 0.6f; });

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.85f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 0.3f;
                src.maxDistance = 6f;
                _pool[i] = src;
            }
            var amb = new GameObject("Ambient");
            amb.transform.SetParent(transform, false);
            _ambientSource = amb.AddComponent<AudioSource>();
            _ambientSource.clip = _ambient;
            _ambientSource.loop = true;
            _ambientSource.volume = 0.05f;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.Play();
        }

        void OnEnable()
        {
            GameEvents.PartGrabbed += OnGrabbed;
            GameEvents.PartInstalled += OnInstalled;
            GameEvents.PartRejected += OnRejected;
            FlowEvents.LoupeToggled += OnLoupe;
            FlowEvents.ScrewPoked += OnScrew;
            FlowEvents.CrownClicked += OnScrew;
            FlowEvents.Beat += OnBeat;
            FlowEvents.StageChanged += OnStage;
        }

        void OnDisable()
        {
            GameEvents.PartGrabbed -= OnGrabbed;
            GameEvents.PartInstalled -= OnInstalled;
            GameEvents.PartRejected -= OnRejected;
            FlowEvents.LoupeToggled -= OnLoupe;
            FlowEvents.ScrewPoked -= OnScrew;
            FlowEvents.CrownClicked -= OnScrew;
            FlowEvents.Beat -= OnBeat;
            FlowEvents.StageChanged -= OnStage;
        }

        void Play(AudioClip clip, Vector3 pos, float volume, float pitch = 1f)
        {
            AudioSource s = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            s.transform.position = pos;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }

        void OnGrabbed(WatchPart p) => Play(_click, p.transform.position, 0.7f);
        void OnInstalled(WatchPart p, SnapSocket s, float precision)
        {
            Play(_snap, p.transform.position, 0.9f);
            Play(_chime, p.transform.position, 0.25f + 0.25f * precision, 0.9f + 0.3f * precision);
        }
        void OnRejected(WatchPart p, SnapSocket s, string reason) => Play(_buzz, p.transform.position, 0.6f);
        void OnLoupe(bool on)
        {
            LoupePos(out Vector3 pos);
            Play(_whoosh, pos, 0.6f, on ? 1f : 0.8f);
        }
        void OnScrew(Vector3 pos) => Play(_ratchet, pos, 0.6f);
        void OnBeat(int n) => Play((n & 1) == 0 ? _tick : _tock, WorkspacePos(), 0.5f);

        void OnStage(Stage s)
        {
            if (s == Stage.Complete) StartCoroutine(Arpeggio());
        }

        IEnumerator Arpeggio()
        {
            float[] pitches = { 1f, 1.26f, 1.5f, 2f };
            for (int i = 0; i < pitches.Length; i++)
            {
                Play(_chime, WorkspacePos(), 0.5f, pitches[i]);
                yield return new WaitForSeconds(0.16f);
            }
        }

        static Vector3 WorkspacePos()
        {
            WatchAssemblyManager m = WatchAssemblyManager.Instance;
            return m != null ? m.MovementRoot.position : Vector3.zero;
        }

        static void LoupePos(out Vector3 pos)
        {
            var lever = UnityEngine.Object.FindFirstObjectByType<Watchmaker.Mechanics.LoupeLever>();
            pos = lever != null ? lever.transform.position : WorkspacePos();
        }
    }
}
