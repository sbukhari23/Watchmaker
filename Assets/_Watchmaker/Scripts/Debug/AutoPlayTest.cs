using System.Collections;
using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Mechanics;

namespace Watchmaker.Core
{
    /// <summary>Development helper: plays the whole game through the real snap pipeline without hands (menu: Watchmaker/Debug/Run AutoPlay).</summary>
    public sealed class AutoPlayTest : MonoBehaviour
    {
        static readonly string[] Order =
        {
            "L1_great_wheel", "L1_intermediate_wheel", "L1_escape_wheel", "L2_bridge", "L3_screw_a", "L3_screw_b"
        };

        public void Run() { StopAllCoroutines(); StartCoroutine(Play()); }

        IEnumerator Play()
        {
            var wait = new WaitForSeconds(0.7f);
            Debug.Log("[AutoPlay] start, stage=" + GameFlow.Instance.Current);
            yield return new WaitForSeconds(0.5f);

            WatchPart[] parts = FindObjectsByType<WatchPart>(FindObjectsSortMode.None);
            SnapSocket[] sockets = FindObjectsByType<SnapSocket>(FindObjectsSortMode.None);
            WatchAssemblyManager asm = WatchAssemblyManager.Instance;

            if (V2Story.Present) yield return V2Story.Instance.AutoPre();
            LoupeLever lever = FindFirstObjectByType<LoupeLever>();
            if (lever != null) { lever.Toggle(); yield return new WaitForSeconds(1.2f); }

            foreach (string id in Order)
            {
                WatchPart part = null; SnapSocket socket = null;
                foreach (WatchPart p in parts) if (p.PartId == id) part = p;
                foreach (SnapSocket s in sockets) if (s.AcceptsPartId == id) socket = s;
                if (part == null || socket == null) { Debug.LogError("[AutoPlay] missing part/socket for " + id); yield break; }
                part.OnGrabbed();
                part.transform.position = socket.transform.position;
                asm.ResolveRelease(part, socket.transform.position);
                Debug.Log("[AutoPlay] released " + id + ", stage=" + GameFlow.Instance.Current);
                yield return wait;
            }

            if (lever != null) { lever.Toggle(); yield return new WaitForSeconds(1.2f); }

            foreach (ScrewTarget t in FindObjectsByType<ScrewTarget>(FindObjectsSortMode.None))
                for (int i = 0; i < 3; i++) { t.Poke(); yield return new WaitForSeconds(0.4f); }
            Debug.Log("[AutoPlay] screws tightened, stage=" + GameFlow.Instance.Current);

            if (V2Story.Present) yield return V2Story.Instance.AutoClose();
            CrownWinder crown = FindFirstObjectByType<CrownWinder>();
            for (int i = 0; i < 20; i++) { crown.AddDegrees(45f); yield return new WaitForSeconds(0.1f); }
            if (V2Story.Present) yield return V2Story.Instance.AutoStart();
            yield return new WaitForSeconds(2f);
            var check = new GameObject("RestartCheck"); DontDestroyOnLoad(check); check.AddComponent<RestartCheck>();
            Debug.Log("[AutoPlay] DONE, stage=" + GameFlow.Instance.Current + ", running=" + FindFirstObjectByType<MovementDriver>().Running + ", time=" + GameFlow.Instance.RunSeconds.ToString("F1") + "s, precision=" + GameFlow.Instance.AveragePrecision.ToString("F2"));
            yield return new WaitForSeconds(1f);
            var btn = FindFirstObjectByType<Watchmaker.UI.PokeButton>();
            if (btn != null) btn.Restart(); else Debug.LogError("[AutoPlay] restart button missing");
        }
    }

    /// <summary>Verifies the Restart button (scene reload) recreates the runtime systems.</summary>
    public sealed class RestartCheck : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            bool pinch = GameObject.Find("PinchGrabber") != null, drv = GameObject.Find("ScrewdriverTool") != null, story = GameObject.Find("V2Story") != null;
            string msg = "[AutoPlay] RESTART pinch=" + pinch + " screwdriver=" + drv + " story=" + story + " stage=" + (GameFlow.Instance != null ? GameFlow.Instance.Current.ToString() : "none");
            if (pinch && drv && story) Debug.Log(msg); else Debug.LogError(msg);
            Destroy(gameObject);
        }
    }
}
