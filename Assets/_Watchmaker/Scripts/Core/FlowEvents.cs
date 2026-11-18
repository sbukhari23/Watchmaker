using System;
using UnityEngine;
using Watchmaker.Assembly;

namespace Watchmaker.Core
{
    public enum Stage { Welcome, Wheels, Bridge, Screws, Tighten, Wind, Complete, Inspect, Flip, OpenCase, CloseCase, FlipBack, Start }

    /// <summary>Events for the post-snap game flow (loupe, screws, crown, running movement).</summary>
    public static class FlowEvents
    {
        public static event Action<Stage> StageChanged;
        public static event Action<bool> LoupeToggled;
        public static event Action<Vector3> ScrewPoked;
        public static event Action<WatchPart> ScrewTightened;
        public static event Action<Vector3> CrownClicked;
        public static event Action WatchWound;
        public static event Action<int> Beat;

        public static void RaiseStage(Stage s) => StageChanged?.Invoke(s);
        public static void RaiseLoupe(bool on) => LoupeToggled?.Invoke(on);
        public static void RaiseScrewPoked(Vector3 p) => ScrewPoked?.Invoke(p);
        public static void RaiseScrewTightened(WatchPart p) => ScrewTightened?.Invoke(p);
        public static void RaiseCrownClicked(Vector3 p) => CrownClicked?.Invoke(p);
        public static void RaiseWatchWound() => WatchWound?.Invoke();
        public static void RaiseBeat(int n) => Beat?.Invoke(n);
    }
}
