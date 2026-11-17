using UnityEngine;
using UnityEngine.InputSystem;
using Watchmaker.Assembly;

namespace Watchmaker.Core
{
    /// <summary>Editor/desktop stand-in for hands: LMB drag picks up parts, hold RMB = fingertip (poke, wind crown).</summary>
    public sealed class DesktopHand : MonoBehaviour
    {
        WatchPart _held;
        float _planeY;
        Vector3 _offset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.platform == RuntimePlatform.Android) return;
            new GameObject("DesktopHand").AddComponent<DesktopHand>();
        }

        void Update()
        {
            Mouse m = Mouse.current;
            Camera cam = Camera.main;
            if (m == null || cam == null) return;
            Vector2 p = m.position.ReadValue();
            Ray ray = cam.ViewportPointToRay(new Vector3(p.x / Screen.width, p.y / Screen.height, 0f));
            WatchAssemblyManager asm = WatchAssemblyManager.Instance;

            if (m.leftButton.wasPressedThisFrame && Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                WatchPart part = hit.collider.GetComponentInParent<WatchPart>();
                if (part != null && part.State != PartState.Installed)
                {
                    _held = part; part.OnGrabbed();
                    _planeY = hit.point.y + 0.03f;
                    _offset = part.transform.position - hit.point; _offset.y = 0f;
                }
            }
            if (_held != null)
            {
                Plane pl = new Plane(Vector3.up, new Vector3(0f, _planeY, 0f));
                if (pl.Raycast(ray, out float d))
                {
                    Vector3 pos = ray.GetPoint(d) + _offset; pos.y = _planeY;
                    _held.transform.position = pos;
                    if (asm != null) asm.UpdateHeldFeedback(_held, pos);
                }
                if (m.leftButton.wasReleasedThisFrame)
                {
                    if (asm != null) asm.ResolveRelease(_held, _held.transform.position);
                    _held = null;
                }
            }

            if (m.rightButton.isPressed)
            {
                Vector3 tip;
                if (Physics.Raycast(ray, out RaycastHit h2, 5f)) tip = h2.point + Vector3.up * 0.004f;
                else
                {
                    Transform mv = asm != null ? asm.MovementRoot : null;
                    float y = mv != null ? mv.position.y + 0.012f : 1f;
                    new Plane(Vector3.up, new Vector3(0f, y, 0f)).Raycast(ray, out float d2);
                    tip = ray.GetPoint(d2);
                }
                HandTracker.VirtualTip = tip; HandTracker.HasVirtual = true;
            }
            else HandTracker.HasVirtual = false;
        }
    }
}
