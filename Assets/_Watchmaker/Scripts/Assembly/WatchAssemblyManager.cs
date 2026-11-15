using UnityEngine;
using Watchmaker.Core;

namespace Watchmaker.Assembly
{
    /// <summary>Owns the sockets of the active movement: held-part feedback, install validation and completion.</summary>
    public sealed class WatchAssemblyManager : MonoBehaviour
    {
        public static WatchAssemblyManager Instance { get; private set; }

        [SerializeField] Transform _movementRoot;
        [SerializeField] SnapSocket[] _sockets;
        [Tooltip("Gain is reduced when the held part is within this multiple of the capture radius of its correct socket.")]
        [SerializeField] float _nearGainRadiusFactor = 2f;

        int _installedCount;

        public Transform MovementRoot => _movementRoot;
        public bool IsComplete => _sockets != null && _installedCount >= _sockets.Length;

        public void Configure(Transform movementRoot, SnapSocket[] sockets)
        {
            _movementRoot = movementRoot; _sockets = sockets;
        }

        void Awake() => Instance = this;

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Distance in the movement's local space (real metres, independent of workspace scale).</summary>
        float LocalDistance(SnapSocket socket, Vector3 worldPosition)
        {
            Vector3 a = _movementRoot.InverseTransformPoint(worldPosition);
            Vector3 b = _movementRoot.InverseTransformPoint(socket.transform.position);
            return Vector3.Distance(a, b);
        }

        SnapSocket Nearest(Vector3 worldPosition, out float distance)
        {
            SnapSocket best = null;
            distance = float.MaxValue;
            for (int i = 0; i < _sockets.Length; i++)
            {
                SnapSocket s = _sockets[i];
                if (s == null || s.IsFilled) continue;
                float d = LocalDistance(s, worldPosition);
                if (d < distance) { distance = d; best = s; }
            }
            return best;
        }

        static bool Accepts(SnapSocket socket, WatchPart part) =>
            socket.AcceptsPartId == part.PartId && socket.PrerequisitesMet();

        public bool IsNearCorrectSocket(WatchPart part, Vector3 worldPosition)
        {
            for (int i = 0; i < _sockets.Length; i++)
            {
                SnapSocket s = _sockets[i];
                if (s == null || s.IsFilled || !Accepts(s, part)) continue;
                if (LocalDistance(s, worldPosition) <= s.CaptureRadius * _nearGainRadiusFactor) return true;
            }
            return false;
        }

        public void UpdateHeldFeedback(WatchPart part, Vector3 worldPosition)
        {
            float distance;
            SnapSocket nearest = Nearest(worldPosition, out distance);
            for (int i = 0; i < _sockets.Length; i++)
            {
                SnapSocket s = _sockets[i];
                if (s == null || s.IsFilled) continue;
                if (s == nearest && distance <= s.CaptureRadius)
                    s.SetGhost(Accepts(s, part) ? GhostState.Correct : GhostState.Wrong);
                else
                    s.SetGhost(GhostState.Hidden);
            }
        }

        public void ResolveRelease(WatchPart part, Vector3 worldPosition)
        {
            float distance;
            SnapSocket nearest = Nearest(worldPosition, out distance);
            for (int i = 0; i < _sockets.Length; i++)
                if (_sockets[i] != null) _sockets[i].SetGhost(GhostState.Hidden);

            if (nearest == null || distance > nearest.CaptureRadius)
            {
                part.ReturnToTray();
                return;
            }

            if (!Accepts(nearest, part))
            {
                string reason = nearest.AcceptsPartId != part.PartId
                    ? "WrongPart"
                    : "PrereqMissing:" + nearest.FirstMissingPrerequisite();
                GameEvents.RaisePartRejected(part, nearest, reason);
                part.ReturnToTray();
                return;
            }

            float precision = Mathf.Clamp01(1f - Mathf.Max(0f, distance - nearest.InstallTolerance) /
                                            Mathf.Max(nearest.CaptureRadius - nearest.InstallTolerance, 0.0001f));
            nearest.MarkFilled();
            _installedCount++;
            SnapSocket socket = nearest;
            part.SnapInto(socket.transform, () =>
            {
                GameEvents.RaisePartInstalled(part, socket, precision);
                if (IsComplete) GameEvents.RaiseAssemblyCompleted();
            });
        }
    }
}
