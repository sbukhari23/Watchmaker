using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;
using Watchmaker.Assembly;

namespace Watchmaker.Core
{
    /// <summary>Direct pinch grab (thumb tip + index tip) that drives WatchPart without relying on ISDK interactor wiring.</summary>
    public sealed class PinchGrabber : MonoBehaviour
    {
        const float PinchOn = 0.024f, PinchOff = 0.040f, GrabRadius = 0.055f;

        sealed class Slot { public Hand Hand; public WatchPart Held; public bool Pinching; public Vector3 Offset; public Vector3 Smooth; }
        Slot[] _slots = new Slot[0];
        WatchPart[] _parts = new WatchPart[0];
        float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => new GameObject("PinchGrabber").AddComponent<PinchGrabber>();

        void Scan()
        {
            _nextScan = Time.unscaledTime + 1.5f;
            Hand[] hands = FindObjectsByType<Hand>(FindObjectsSortMode.None);
            if (hands.Length != _slots.Length)
            {
                _slots = new Slot[hands.Length];
                for (int i = 0; i < hands.Length; i++) _slots[i] = new Slot();
            }
            for (int i = 0; i < hands.Length; i++) _slots[i].Hand = hands[i];
            _parts = FindObjectsByType<WatchPart>(FindObjectsSortMode.None);
            foreach (WatchPart p in _parts)
            {
                var a = p.GetComponent<HandGrabInteractable>(); if (a != null) a.enabled = false;
                var b = p.GetComponent<Oculus.Interaction.GrabInteractable>(); if (b != null) b.enabled = false;
            }
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextScan) Scan();
            WatchAssemblyManager asm = WatchAssemblyManager.Instance;
            foreach (Slot s in _slots)
            {
                Hand h = s.Hand;
                bool ok = h != null && h.isActiveAndEnabled && h.IsConnected;
                Pose thumb = default, index = default;
                ok = ok && h.GetJointPose(HandJointId.HandThumbTip, out thumb) && h.GetJointPose(HandJointId.HandIndexTip, out index);
                float d = ok ? Vector3.Distance(thumb.position, index.position) : float.MaxValue;
                Vector3 mid = ok ? (thumb.position + index.position) * 0.5f : Vector3.zero;

                if (!s.Pinching && d <= PinchOn) { s.Pinching = true; TryGrab(s, mid); }
                else if (s.Pinching && d >= PinchOff) { s.Pinching = false; Release(s, asm); }
                if (!ok && s.Held != null) { s.Pinching = false; Release(s, asm); }

                if (s.Held != null)
                {
                    s.Smooth = Vector3.Lerp(s.Smooth, mid + s.Offset, 1f - Mathf.Exp(-30f * Time.deltaTime));
                    s.Held.transform.position = s.Smooth;
                    if (asm != null) asm.UpdateHeldFeedback(s.Held, s.Smooth);
                }
            }
        }

        void TryGrab(Slot s, Vector3 point)
        {
            WatchPart best = null; float bd = GrabRadius * Mathf.Max(1f, transform.lossyScale.x);
            foreach (WatchPart p in _parts)
            {
                if (p == null || p.State == PartState.Installed || p.State == PartState.Held) continue;
                float d = Vector3.Distance(p.transform.position, point);
                if (d < bd) { bd = d; best = p; }
            }
            if (best == null) return;
            s.Held = best; best.OnGrabbed();
            s.Offset = best.transform.position - point; s.Smooth = best.transform.position;
            Debug.Log("[Pinch] grabbed " + best.PartId);
        }

        void Release(Slot s, WatchAssemblyManager asm)
        {
            if (s.Held == null) return;
            Debug.Log("[Pinch] released " + s.Held.PartId);
            if (asm != null) asm.ResolveRelease(s.Held, s.Held.transform.position);
            s.Held = null;
        }
    }
}
