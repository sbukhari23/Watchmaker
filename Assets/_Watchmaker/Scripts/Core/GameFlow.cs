using System.Collections.Generic;
using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Mechanics;

namespace Watchmaker.Core
{
    /// <summary>Linear game flow: wheels -> bridge -> screws -> tighten -> wind -> complete. Drives instructions, hints and scoring.</summary>
    public sealed class GameFlow : MonoBehaviour
    {
        static readonly string[] WheelOrder = { "L1_great_wheel", "L1_intermediate_wheel", "L1_escape_wheel" };
        static readonly string[] ScrewOrder = { "L3_screw_a", "L3_screw_b" };

        public static GameFlow Instance { get; private set; }

        public Stage Current { get; private set; } = Stage.Welcome;
        public string Title { get; private set; } = "";
        public string Body { get; private set; } = "";
        public int Done { get; private set; }
        public int Total { get; private set; }
        public float RunSeconds { get; private set; }
        public float AveragePrecision { get; private set; }

        readonly List<float> _precisions = new List<float>();
        WatchPart[] _parts;
        ScrewTarget[] _screwTargets;
        CrownWinder _crown;
        MovementDriver _driver;
        float _stageStart;
        float _runStart = -1f;
        int _wheels, _bridge, _screws, _tight;
        WatchPart _hint;
        Transform _hintVisual;
        Vector3 _hintBase;

        public float Progress01
        {
            get
            {
                if (Current == Stage.Wind && _crown != null) return _crown.Progress;
                if (Current == Stage.Complete) return 1f;
                return Total > 0 ? Mathf.Clamp01((float)Done / Total) : 0f;
            }
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            GameEvents.PartGrabbed += OnGrabbed;
            GameEvents.PartInstalled += OnInstalled;
            FlowEvents.ScrewTightened += OnScrewTightened;
            FlowEvents.WatchWound += OnWound;
        }

        void OnDisable()
        {
            GameEvents.PartGrabbed -= OnGrabbed;
            GameEvents.PartInstalled -= OnInstalled;
            FlowEvents.ScrewTightened -= OnScrewTightened;
            FlowEvents.WatchWound -= OnWound;
            ClearHint();
        }

        void Start()
        {
            _parts = FindObjectsByType<WatchPart>(FindObjectsSortMode.None);
            _screwTargets = FindObjectsByType<ScrewTarget>(FindObjectsSortMode.None);
            _crown = FindFirstObjectByType<CrownWinder>();
            _driver = FindFirstObjectByType<MovementDriver>();
            Enter(Stage.Welcome);
        }

        void Update()
        {
            if (Current == Stage.Welcome && !TutorialGuide.Showing && Time.time - _stageStart > 4f) Enter(Stage.Wheels);
            if (_runStart >= 0f && Current != Stage.Complete) RunSeconds = Time.time - _runStart;
            UpdateHint();
        }

        void OnGrabbed(WatchPart p)
        {
            TutorialGuide.Dismiss();
            if (Current == Stage.Welcome) Enter(Stage.Wheels);
        }

        void OnInstalled(WatchPart p, SnapSocket s, float precision)
        {
            TutorialGuide.Dismiss();
            if (Current == Stage.Welcome) Enter(Stage.Wheels);
            _precisions.Add(precision);
            if (p.PartId.StartsWith("L1_")) _wheels++;
            else if (p.PartId == "L2_bridge") _bridge++;
            else if (p.PartId.StartsWith("L3_screw")) _screws++;

            switch (Current)
            {
                case Stage.Wheels: Done = _wheels; if (_wheels >= 3) Enter(Stage.Bridge); break;
                case Stage.Bridge: Done = _bridge; if (_bridge >= 1) Enter(Stage.Screws); break;
                case Stage.Screws: Done = _screws; if (_screws >= 2) Enter(Stage.Tighten); break;
            }
        }

        void OnScrewTightened(WatchPart p)
        {
            _tight++;
            if (Current == Stage.Tighten) { Done = _tight; if (_tight >= 2) Enter(Stage.Wind); }
        }

        void OnWound()
        {
            if (Current == Stage.Wind) Enter(Stage.Complete);
        }

        void Enter(Stage s)
        {
            Current = s;
            _stageStart = Time.time;
            Done = 0;
            switch (s)
            {
                case Stage.Welcome:
                    Title = "Welcome, watchmaker";
                    Body = "Follow the guide, then pinch a part from the tray and release it over its green outline.";
                    Total = 0;
                    break;
                case Stage.Wheels:
                    if (_runStart < 0f) _runStart = Time.time;
                    Title = "Install the three wheels";
                    Body = "Start with the large brass wheel. Pinch it, move it over the movement and let go.";
                    Total = 3; Done = _wheels;
                    break;
                case Stage.Bridge:
                    Title = "Fit the bridge";
                    Body = "Place the bridge across the wheels to hold them in place.";
                    Total = 1;
                    break;
                case Stage.Screws:
                    Title = "Secure the bridge";
                    Body = "Place a screw at each end of the bridge.";
                    Total = 2; Done = _screws;
                    break;
                case Stage.Tighten:
                    Title = "Tighten the screws";
                    Body = "Pinch the screwdriver on the right and touch its tip to each screw (or poke it three times with a fingertip).";
                    Total = 2;
                    if (_screwTargets != null) foreach (ScrewTarget t in _screwTargets) t.SetActive(true);
                    break;
                case Stage.Wind:
                    Title = "Wind the watch";
                    Body = "Circle your fingertip around the brass crown.";
                    Total = 1;
                    if (_crown != null) _crown.SetActive(true);
                    break;
                case Stage.Complete:
                    RunSeconds = Time.time - _runStart;
                    float sum = 0f;
                    for (int i = 0; i < _precisions.Count; i++) sum += _precisions[i];
                    AveragePrecision = _precisions.Count > 0 ? sum / _precisions.Count : 1f;
                    Title = "Your watch is running!";
                    Body = string.Format("Time {0}:{1:00}    Precision {2:0}%\nPoke the brass button to start again.",
                        (int)RunSeconds / 60, (int)RunSeconds % 60, AveragePrecision * 100f);
                    Total = 1; Done = 1;
                    if (_driver != null) _driver.Run();
                    ClearHint();
                    break;
            }
            FlowEvents.RaiseStage(s);
        }

        // ---- hint: gently pulse the next part that should be picked up
        string NextPartId()
        {
            switch (Current)
            {
                case Stage.Wheels: return _wheels < WheelOrder.Length ? WheelOrder[_wheels] : null;
                case Stage.Bridge: return "L2_bridge";
                case Stage.Screws: return _screws < ScrewOrder.Length ? ScrewOrder[_screws] : null;
                default: return null;
            }
        }

        void UpdateHint()
        {
            if (Current == Stage.Welcome || _parts == null) { ClearHint(); return; }
            string id = NextPartId();
            if (_hint == null || _hint.PartId != id || _hint.State != PartState.InTray)
            {
                ClearHint();
                if (id == null) return;
                for (int i = 0; i < _parts.Length; i++)
                    if (_parts[i] != null && _parts[i].PartId == id && _parts[i].State == PartState.InTray)
                    {
                        _hint = _parts[i];
                        _hintVisual = _hint.transform.Find("Visual");
                        if (_hintVisual != null) _hintBase = _hintVisual.localScale;
                        break;
                    }
            }
            ShowTarget(id);
            if (_hint != null && _hintVisual != null)
                _hintVisual.localScale = _hintBase * (1f + 0.04f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f)));
        }

        SnapSocket[] _sockets;
        void ShowTarget(string id)
        {
            if (id == null || _parts == null) return;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i] != null && _parts[i].State == PartState.Held) return;
            if (_sockets == null || _sockets.Length == 0) _sockets = FindObjectsByType<SnapSocket>(FindObjectsSortMode.None);
            for (int i = 0; i < _sockets.Length; i++)
                if (_sockets[i] != null && !_sockets[i].IsFilled && _sockets[i].AcceptsPartId == id && _sockets[i].PrerequisitesMet())
                    _sockets[i].SetGhost(GhostState.Correct);
        }

        void ClearHint()
        {
            if (_hintVisual != null) _hintVisual.localScale = _hintBase;
            _hint = null;
            _hintVisual = null;
        }
    }
}
