using System;
using Watchmaker.Assembly;

namespace Watchmaker.Core
{
    /// <summary>Static event bus. Subscribe in OnEnable, unsubscribe in OnDisable.</summary>
    public static class GameEvents
    {
        public static event Action<WatchPart> PartGrabbed;
        public static event Action<WatchPart> PartReleased;
        public static event Action<WatchPart, SnapSocket, float> PartInstalled;
        public static event Action<WatchPart, SnapSocket, string> PartRejected;
        public static event Action AssemblyCompleted;

        public static void RaisePartGrabbed(WatchPart p) => PartGrabbed?.Invoke(p);
        public static void RaisePartReleased(WatchPart p) => PartReleased?.Invoke(p);
        public static void RaisePartInstalled(WatchPart p, SnapSocket s, float precision) => PartInstalled?.Invoke(p, s, precision);
        public static void RaisePartRejected(WatchPart p, SnapSocket s, string reason) => PartRejected?.Invoke(p, s, reason);
        public static void RaiseAssemblyCompleted() => AssemblyCompleted?.Invoke();
    }
}
