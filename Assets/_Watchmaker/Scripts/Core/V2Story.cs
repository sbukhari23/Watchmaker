using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using Watchmaker.Assembly;
using Watchmaker.Mechanics;

namespace Watchmaker.Core
{
    /// <summary>
    /// v2 story layer built on the six Tripo assets (case, case back, hour hand, minute hand, side pusher, loupe):
    /// inspect with the loupe -> flip the watch -> unscrew the case back -> (existing repair loop) -> close the case -> flip -> wind -> press the pusher to start.
    /// Everything is hands-only (pinch / fingertip poke). No-op if the "WatchV2" object is absent.
    /// </summary>
    public sealed class V2Story : MonoBehaviour
    {
        public static V2Story Instance { get; private set; }
        public static bool Present => Instance != null;
        public static bool PartsLocked { get; private set; } = true;
        public static readonly List<CaseScrew> CaseScrews = new List<CaseScrew>();

        const float PinchOn = 0.024f, PinchOff = 0.040f, GrabRadius = 0.07f, LensRadius = 0.07f, HoldSeconds = 1.2f;
        const float HourRestYaw = 0f, FrozenHour = 113.5f, FrozenMinute = 282f;
        static readonly Vector3 MinuteDir = new Vector3(-0.5f, 0f, 0.85f);

        public sealed class CaseScrew
        {
            public Transform T;
            public bool Active, Install;
            public int Count;
            public PokeState Ps;
            public bool Done => Install ? Count >= 3 : Count >= 3;

            public void Poke()
            {
                if (!Active) return;
                Count++;
                float dir = Install ? -1f : 1f;
                T.Rotate(0f, 180f * dir, 0f, Space.Self);
                T.position += T.up * (0.0018f * dir);
                FlowEvents.RaiseScrewPoked(T.position);
                if (Count >= 3)
                {
                    Active = false;
                    if (!Install) T.gameObject.SetActive(false);
                }
                if (Instance != null) Instance.OnScrewPoked();
            }
        }

        sealed class Defect { public Vector3 Pos; public Transform Marker; public Renderer Rend; public float Hold; public bool Logged; public string Name; }

        Transform _root, _case, _back, _hourHand, _minHand, _pusher, _loupe, _hourPivot, _minPivot, _pivot;
        Vector3 _center, _backMounted, _backAside, _pusherRest, _loupeHome;
        Quaternion _loupeHomeRot;
        float _caseTop, _handY;
        readonly List<Defect> _defects = new List<Defect>();
        PokeState _psCase, _psBack, _psPusher;
        Stage _lastStage = (Stage)(-1);
        bool _busy, _backOn, _flipped, _handsRun;
        int _movState = -1;
        CrownWinder _crownOnCase;
        Renderer[] _movRends = new Renderer[0];
        float _handsStart;
        Material _red, _green, _steel;

        // loupe holding
        Hand[] _hands = new Hand[0];
        float _nextScan;
        Hand _holder;
        bool _pinching;
        Vector3 _offset, _smooth;
        Vector3 _lensLocal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; PartsLocked = true; CaseScrews.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene s, LoadSceneMode m)
        {
            if (GameObject.Find("WatchV2") == null) return;
            new GameObject("V2Story").AddComponent<V2Story>();
        }

        void Awake() { Instance = this; PartsLocked = true; CaseScrews.Clear(); }
        void OnDestroy() { if (Instance == this) Instance = null; CaseScrews.Clear(); PartsLocked = false; }

        static Bounds BoundsOf(Transform t)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.one * 0.02f);
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        void Start()
        {
            GameObject rootGo = GameObject.Find("WatchV2");
            if (rootGo == null) { enabled = false; return; }
            _root = rootGo.transform;
            _case = _root.Find("Case"); _back = _root.Find("CaseBack"); _hourHand = _root.Find("HourHand");
            _minHand = _root.Find("MinuteHand"); _pusher = _root.Find("SidePusher"); _loupe = _root.Find("Loupe");
            if (_case == null || _back == null || _hourHand == null || _minHand == null || _pusher == null)
            { Debug.LogWarning("[V2] missing parts"); enabled = false; return; }

            _red = Unlit(new Color(1f, 0.25f, 0.2f)); _green = Unlit(new Color(0.25f, 0.9f, 0.4f)); _steel = Unlit(new Color(0.72f, 0.74f, 0.78f));

            Bounds cb = BoundsOf(_case);
            _center = new Vector3(cb.center.x, cb.center.y, cb.center.z);
            _caseTop = cb.max.y;
            _handY = _caseTop + 0.0015f;

            // flip pivot at the case centre; case, hands, pusher ride on it
            _pivot = new GameObject("WatchFlip").transform;
            _pivot.SetParent(_root, false);
            _pivot.position = _center;
            _case.SetParent(_pivot, true); _pusher.SetParent(_pivot, true);

            // hand pivots on the dial centre
            _hourPivot = new GameObject("HourPivot").transform; _hourPivot.SetParent(_pivot, false);
            _minPivot = new GameObject("MinutePivot").transform; _minPivot.SetParent(_pivot, false);
            _hourPivot.position = new Vector3(_center.x, _handY, _center.z);
            _minPivot.position = new Vector3(_center.x, _handY + 0.003f, _center.z);
            _hourHand.SetParent(_hourPivot, true); _minHand.SetParent(_minPivot, true);
                        float hl = 0.05f, ml = 0.075f;
            _hourHand.localPosition = Vector3.forward * (hl * 0.5f - 0.008f);
            _minHand.localPosition = MinuteDir.normalized * (ml * 0.5f - 0.008f);
            SetHands(FrozenHour, FrozenMinute);

            // case back: hidden until the watch is flipped, then sits on the open side
            Bounds bb = BoundsOf(_back);
            _backMounted = new Vector3(_center.x, _caseTop + bb.extents.y + 0.001f, _center.z);
            _backAside = new Vector3(_center.x, _center.y - (cb.extents.y) + bb.extents.y + 0.002f, _center.z - 0.19f);
            _back.position = _backMounted;
            _back.gameObject.SetActive(false);

            // four case-back screws
            float r = Mathf.Min(bb.extents.x, bb.extents.z) * 0.72f;
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = "CaseScrew" + i;
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(_root, false);
                g.transform.localScale = new Vector3(0.014f, 0.0012f, 0.014f);
                g.GetComponent<Renderer>().sharedMaterial = _steel;
                g.transform.position = new Vector3(_center.x + Mathf.Cos(a) * r, _caseTop + bb.extents.y * 2f + 0.003f, _center.z + Mathf.Sin(a) * r);
                g.SetActive(false);
                CaseScrews.Add(new CaseScrew { T = g.transform });
            }

            // loupe
            if (_loupe != null) { _loupeHome = _loupe.position; _loupeHomeRot = _loupe.rotation; }

            // defects revealed only by the loupe
            float dialY = _caseTop;
            AddDefect("Hairline crack in the crystal", _center + new Vector3(0.04f, dialY - _center.y + 0.004f, 0.035f));
            AddDefect("Corroded winding crown", new Vector3(_center.x - cb.extents.x * 0.98f, _center.y, _center.z));
            AddDefect("Seized side pusher", _pusher.position + Vector3.up * 0.012f);

            _pusherRest = _pusher.position;
            Debug.Log("[V2] story ready, centre=" + _center + " top=" + _caseTop);
        }

        void AddDefect(string name, Vector3 pos)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "Defect_" + name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(_root, true);
            g.transform.position = pos;
            g.transform.localScale = Vector3.one * 0.014f;
            var rend = g.GetComponent<Renderer>(); rend.sharedMaterial = _red; rend.enabled = false;
            _defects.Add(new Defect { Pos = pos, Marker = g.transform, Rend = rend, Name = name });
        }

        void SetHands(float hourDeg, float minDeg)
        {
            if (_hourPivot == null) return;
            _hourPivot.localRotation = Quaternion.Euler(0f, hourDeg - HourRestYaw, 0f);
            float minRest = Mathf.Atan2(MinuteDir.x, MinuteDir.z) * Mathf.Rad2Deg;
            _minPivot.localRotation = Quaternion.Euler(0f, minDeg - minRest, 0f);
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            GameFlow f = GameFlow.Instance;
            if (f == null || _pivot == null) return;
            Stage s = f.Current;
            if (s != _lastStage) { OnStage(f, s); _lastStage = s; }
            UpdateMovementVisibility();
            UpdateLoupe(f, s);
            if (!_busy)
            {
                switch (s)
                {
                    case Stage.Flip: if (_psCase.Update(_center + Vector3.up * 0.03f, 0.055f, 0.8f)) StartCoroutine(Flip(true)); break;
                    case Stage.FlipBack: if (_psCase.Update(_back.position + Vector3.up * 0.01f, 0.055f, 0.8f)) StartCoroutine(Flip(false)); break;
                    case Stage.OpenCase:
                        FingerScrews();
                        if (AllScrews(true) && _backOn && _psBack.Update(_back.position, 0.05f, 0.8f)) StartCoroutine(MoveBack(false));
                        break;
                    case Stage.CloseCase:
                        if (!_backOn) { if (_psBack.Update(_back.position, 0.05f, 0.8f)) StartCoroutine(MoveBack(true)); }
                        else FingerScrews();
                        break;
                    case Stage.Start:
                        if (_psPusher.Update(_pusher.position, 0.03f, 0.8f)) StartCoroutine(PressPusher());
                        break;
                }
            }
            UpdateHands();
        }

        // the movement is only visible while the case is open (watch face-down, back removed)
        void UpdateMovementVisibility()
        {
            bool vis = _flipped && !_backOn;
            int st = vis ? 1 : 0;
            if (_movRends.Length == 0)
            {
                MovementDriver md = FindFirstObjectByType<MovementDriver>();
                if (md != null) _movRends = md.GetComponentsInChildren<Renderer>(true);
            }
            if (st == _movState) return;
            _movState = st;
            foreach (Renderer r in _movRends)
                if (r != null && (_crownOnCase == null || !r.transform.IsChildOf(_crownOnCase.transform))) r.enabled = vis;
            foreach (WatchPart p in FindObjectsByType<WatchPart>(FindObjectsSortMode.None))
                if (p != null && p.State == PartState.Installed)
                    foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = vis;
        }

        void OnStage(GameFlow f, Stage s)
        {
            if (s != Stage.Inspect && _lastStage == Stage.Inspect)
                foreach (Defect d in _defects) if (d.Rend != null) d.Rend.enabled = false;
            PartsLocked = !(s == Stage.Wheels || s == Stage.Bridge || s == Stage.Screws || s == Stage.Tighten);
            if (s == Stage.Wind) MoveCrownToCase();
            if (s == Stage.CloseCase) f.SetProgress(0, 5);
        }

        void MoveCrownToCase()
        {
            CrownWinder cw = FindFirstObjectByType<CrownWinder>();
            if (cw == null) return;
            Bounds cb = BoundsOf(_case);
            cw.transform.position = new Vector3(cb.min.x - 0.004f, _center.y, _center.z);
            _crownOnCase = cw;
            foreach (Renderer r in cw.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        bool AllScrews(bool removed)
        {
            foreach (CaseScrew c in CaseScrews) if (c.Count < 3) return false;
            return true;
        }

        int ScrewsDone() { int n = 0; foreach (CaseScrew c in CaseScrews) if (c.Count >= 3) n++; return n; }

        void FingerScrews()
        {
            foreach (CaseScrew c in CaseScrews)
                if (c.Active && c.T != null && c.Ps.Update(c.T.position, 0.016f, 0.45f)) c.Poke();
        }

        public void OnScrewPoked()
        {
            GameFlow f = GameFlow.Instance; if (f == null) return;
            int d = ScrewsDone() + (_backOn || f.Current == Stage.OpenCase ? 0 : 0);
            if (f.Current == Stage.OpenCase) f.SetProgress(d, 5);
            else if (f.Current == Stage.CloseCase) f.SetProgress(1 + d, 5);
            if (f.Current == Stage.CloseCase && AllScrews(false) && _backOn) f.V2Advance(Stage.CloseCase);
        }

        // ---------------------------------------------------------------- flip / back
        IEnumerator Flip(bool faceDown)
        {
            _busy = true;
            if (faceDown) { /* back becomes visible when the open side is up */ }
            float t = 0f, dur = 0.9f;
            float from = faceDown ? 0f : 180f, to = faceDown ? 180f : 360f;
            Vector3 basePos = _pivot.position;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                _pivot.rotation = Quaternion.Euler(Mathf.Lerp(from, to, k), 0f, 0f);
                _pivot.position = basePos + Vector3.up * (0.06f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            _pivot.rotation = Quaternion.Euler(to, 0f, 0f);
            _pivot.position = basePos;
            _flipped = faceDown;
            FlowEvents.RaiseCrownClicked(_pivot.position);
            GameFlow f = GameFlow.Instance;
            if (faceDown)
            {
                _back.SetParent(_pivot, true);
                _back.position = _backMounted; _backOn = true;
                _back.gameObject.SetActive(true);
                foreach (CaseScrew c in CaseScrews)
                {
                    c.T.gameObject.SetActive(true); c.Count = 0; c.Install = false; c.Active = false;
                    c.T.SetParent(_pivot, true);
                }
                _busy = false;
                if (f != null) f.V2Advance(Stage.Flip);
                foreach (CaseScrew c in CaseScrews) c.Active = true;
            }
            else
            {
                _busy = false;
                if (f != null) f.V2Advance(Stage.FlipBack);
            }
        }

        IEnumerator MoveBack(bool onto)
        {
            _busy = true;
            _back.SetParent(_root, true);
            foreach (CaseScrew c in CaseScrews) if (!onto) c.T.SetParent(_root, true);
            Vector3 a = _back.position, b = onto ? _backMounted : _backAside;
            float t = 0f, dur = 0.8f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                _back.position = Vector3.Lerp(a, b, k) + Vector3.up * (0.05f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            _back.position = b;
            _backOn = onto;
            GameFlow f = GameFlow.Instance;
            if (onto)
            {
                _back.SetParent(_pivot, true);
                float top = _backMounted.y + BoundsOf(_back).extents.y;
                foreach (CaseScrew c in CaseScrews)
                {
                    c.T.gameObject.SetActive(true);
                    c.T.SetParent(_pivot, true);
                    Vector3 p = c.T.position; p.y = top + 0.0054f; c.T.position = p;
                    c.Count = 0; c.Install = true; c.Active = true;
                }
                if (f != null) f.SetProgress(1, 5);
            }
            else if (f != null) f.V2Advance(Stage.OpenCase);
            _busy = false;
        }

        // ---------------------------------------------------------------- pusher + hands
        IEnumerator PressPusher()
        {
            _busy = true;
            Vector3 inward = _center - _pusher.position; inward.y = 0f; inward.Normalize();
            Vector3 a = _pusherRest, b = _pusherRest + inward * 0.0075f;
            for (float t = 0f; t < 0.1f; t += Time.deltaTime) { _pusher.position = Vector3.Lerp(a, b, t / 0.1f); yield return null; }
            _pusher.position = b;
            _handsStart = Time.time; _handsRun = true;
            GameFlow f = GameFlow.Instance;
            if (f != null) f.V2Advance(Stage.Start);
            yield return new WaitForSeconds(0.15f);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime) { _pusher.position = Vector3.Lerp(b, a, t / 0.2f); yield return null; }
            _pusher.position = a;
            _busy = false;
        }

        void UpdateHands()
        {
            if (!_handsRun) return;
            DateTime now = DateTime.Now;
            float realH = ((now.Hour % 12) + now.Minute / 60f) * 30f;
            float realM = (now.Minute + now.Second / 60f) * 6f;
            float k = Mathf.SmoothStep(0f, 1f, (Time.time - _handsStart) / 2f);
            SetHands(Mathf.LerpAngle(FrozenHour, realH, k), Mathf.LerpAngle(FrozenMinute, realM, k));
        }

        // ---------------------------------------------------------------- loupe
        void UpdateLoupe(GameFlow f, Stage s)
        {
            if (_loupe == null) return;
            if (Time.unscaledTime >= _nextScan) { _nextScan = Time.unscaledTime + 1.5f; _hands = FindObjectsByType<Hand>(FindObjectsSortMode.None); }
            bool moved = false;
            foreach (Hand h in _hands)
            {
                bool ok = h != null && h.isActiveAndEnabled && h.IsConnected;
                Pose th = default, ix = default;
                ok = ok && h.GetJointPose(HandJointId.HandThumbTip, out th) && h.GetJointPose(HandJointId.HandIndexTip, out ix);
                if (!ok) { if (h == _holder) _holder = null; continue; }
                float d = Vector3.Distance(th.position, ix.position);
                Vector3 mid = (th.position + ix.position) * 0.5f;
                if (_holder == null)
                {
                    bool pinch = d <= PinchOn;
                    if (pinch && !_pinching)
                    {
                        Bounds b = BoundsOf(_loupe);
                        if (b.SqrDistance(mid) <= GrabRadius * GrabRadius)
                        {
                            Vector3 e = b.extents;
                            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : (e.y >= e.z ? Vector3.up : Vector3.forward);
                            float half = Vector3.Dot(e, axis);
                            Vector3 p1 = b.center + axis * half, p2 = b.center - axis * half;
                            Vector3 lens = Vector3.Distance(p1, mid) > Vector3.Distance(p2, mid) ? p1 : p2;
                            _lensLocal = _loupe.InverseTransformPoint(lens);
                            _holder = h; _offset = _loupe.position - mid; _smooth = _loupe.position;
                        }
                    }
                    _pinching = pinch;
                }
                else if (h == _holder)
                {
                    if (d >= PinchOff) { _holder = null; _pinching = true; continue; }
                    _smooth = Vector3.Lerp(_smooth, mid + _offset, 1f - Mathf.Exp(-30f * Time.deltaTime));
                    _loupe.position = _smooth;
                    moved = true;
                }
            }
            if (_holder == null)
            {
                _loupe.position = Vector3.Lerp(_loupe.position, _loupeHome, 1f - Mathf.Exp(-6f * Time.deltaTime));
                _loupe.rotation = Quaternion.Slerp(_loupe.rotation, _loupeHomeRot, 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
            if (s == Stage.Inspect) UpdateDefects(f, moved ? _loupe.TransformPoint(_lensLocal) : (Vector3?)null);
        }

        void UpdateDefects(GameFlow f, Vector3? lens)
        {
            int logged = 0;
            foreach (Defect d in _defects)
            {
                bool near = lens.HasValue && Vector3.Distance(lens.Value, d.Pos) <= LensRadius;
                if (!d.Logged)
                {
                    d.Rend.enabled = near;
                    d.Hold = near ? d.Hold + Time.deltaTime : Mathf.Max(0f, d.Hold - Time.deltaTime * 2f);
                    if (near) d.Marker.localScale = Vector3.one * (0.010f + 0.008f * Mathf.Clamp01(d.Hold / HoldSeconds));
                    if (d.Hold >= HoldSeconds) LogDefect(f, d);
                }
                if (d.Logged) logged++;
            }
            f.SetProgress(logged, 3);
            if (logged >= 3) f.V2Advance(Stage.Inspect);
        }

        void LogDefect(GameFlow f, Defect d)
        {
            d.Logged = true; d.Rend.enabled = true; d.Rend.sharedMaterial = _green;
            FlowEvents.RaiseCrownClicked(d.Pos);
            Debug.Log("[V2] defect logged: " + d.Name);
        }

        // ---------------------------------------------------------------- editor AutoPlay helpers
        public IEnumerator AutoPre()
        {
            GameFlow f = GameFlow.Instance;
            TutorialGuide.Dismiss();
            while (f.Current != Stage.Inspect) yield return null;
            foreach (Defect d in _defects) LogDefect(f, d);
            Debug.Log("[AutoPlay] inspected, stage=" + f.Current);
            yield return new WaitForSeconds(0.3f);
            while (f.Current != Stage.Flip) yield return null;
            _psCase = default; yield return Flip(true);
            while (f.Current != Stage.OpenCase) yield return null;
            Debug.Log("[AutoPlay] flipped, stage=" + f.Current);
            yield return new WaitForSeconds(0.3f);
            foreach (CaseScrew c in CaseScrews) { for (int i = 0; i < 3; i++) { c.Poke(); yield return new WaitForSeconds(0.2f); } }
            yield return MoveBack(false);
            Debug.Log("[AutoPlay] case open, stage=" + f.Current);
        }

        public IEnumerator AutoClose()
        {
            GameFlow f = GameFlow.Instance;
            while (f.Current != Stage.CloseCase) yield return null;
            yield return MoveBack(true);
            foreach (CaseScrew c in CaseScrews) { for (int i = 0; i < 3; i++) { c.Poke(); yield return new WaitForSeconds(0.2f); } }
            Debug.Log("[AutoPlay] case closed, stage=" + f.Current);
            yield return new WaitForSeconds(0.3f);
            yield return Flip(false);
            Debug.Log("[AutoPlay] face-up, stage=" + f.Current);
        }

        public IEnumerator AutoStart()
        {
            GameFlow f = GameFlow.Instance;
            while (f.Current != Stage.Start) yield return null;
            yield return PressPusher();
            yield return new WaitForSeconds(2.2f);
            Debug.Log("[AutoPlay] started, stage=" + f.Current);
        }
    }
}
