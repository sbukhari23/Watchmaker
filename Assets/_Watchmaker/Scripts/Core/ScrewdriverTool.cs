using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using Watchmaker.Assembly;
using Watchmaker.Mechanics;

namespace Watchmaker.Core
{
    /// <summary>Makes the bench screwdriver a working tool: pinch to pick it up, touch its tip to an installed screw to turn it. Returns to its rest spot when released.</summary>
    public sealed class ScrewdriverTool : MonoBehaviour
    {
        const float PinchOn = 0.024f, PinchOff = 0.040f, GrabRadius = 0.07f, TipRadius = 0.02f, TurnInterval = 0.45f;

        Hand[] _hands = new Hand[0];
        float _nextScan;
        Transform _tool;
        Bounds _bounds;
        Vector3 _home, _offset, _smooth, _tipLocal;
        Quaternion _homeRot;
        Hand _holder;
        bool _pinching;
        float _lastTurn;
        ScrewTarget[] _screws = new ScrewTarget[0];

        public static bool Held { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene s, LoadSceneMode m) => new GameObject("ScrewdriverTool").AddComponent<ScrewdriverTool>();

        void Start()
        {
            Held = false;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.ToLowerInvariant().Contains("screwdriver") && (t.parent == null || !t.parent.name.ToLowerInvariant().Contains("screwdriver")))
                { _tool = t; break; }
            }
            if (_tool == null) { Debug.LogWarning("[Screwdriver] not found in scene"); enabled = false; return; }
            _home = _tool.position; _homeRot = _tool.rotation;
            Debug.Log("[Screwdriver] tool ready: " + _tool.name);
        }

        Bounds CalcBounds()
        {
            Renderer[] rs = _tool.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(_tool.position, Vector3.one * 0.05f);
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1.5f;
                _hands = FindObjectsByType<Hand>(FindObjectsSortMode.None);
                _screws = FindObjectsByType<ScrewTarget>(FindObjectsSortMode.None);
            }
            bool grabbedNow = false;
            foreach (Hand h in _hands)
            {
                bool ok = h != null && h.isActiveAndEnabled && h.IsConnected;
                Pose thumb = default, index = default;
                ok = ok && h.GetJointPose(HandJointId.HandThumbTip, out thumb) && h.GetJointPose(HandJointId.HandIndexTip, out index);
                if (!ok) { if (h == _holder) Drop(); continue; }
                float d = Vector3.Distance(thumb.position, index.position);
                Vector3 mid = (thumb.position + index.position) * 0.5f;
                if (_holder == null)
                {
                    bool pinch = d <= PinchOn;
                    if (pinch && !_pinching) TryGrab(h, mid);
                    _pinching = pinch;
                }
                else if (h == _holder)
                {
                    if (d >= PinchOff) { Drop(); continue; }
                    _smooth = Vector3.Lerp(_smooth, mid + _offset, 1f - Mathf.Exp(-30f * Time.deltaTime));
                    _tool.position = _smooth;
                    grabbedNow = true;
                }
            }
            if (_holder != null && !grabbedNow && _holder == null) Drop();

            if (_holder == null)
            {
                _tool.position = Vector3.Lerp(_tool.position, _home, 1f - Mathf.Exp(-6f * Time.deltaTime));
                _tool.rotation = Quaternion.Slerp(_tool.rotation, _homeRot, 1f - Mathf.Exp(-6f * Time.deltaTime));
                return;
            }
            TurnScrews();
        }

        void TryGrab(Hand h, Vector3 point)
        {
            _bounds = CalcBounds();
            if (_bounds.SqrDistance(point) > GrabRadius * GrabRadius) return;
            Vector3 e = _bounds.extents;
            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : (e.y >= e.z ? Vector3.up : Vector3.forward);
            float half = Vector3.Dot(e, axis);
            Vector3 a = _bounds.center + axis * half, b = _bounds.center - axis * half;
            Vector3 tip = Vector3.Distance(a, point) > Vector3.Distance(b, point) ? a : b; // far end from the pinch = working tip
            _tipLocal = _tool.InverseTransformPoint(tip);
            _holder = h; Held = true;
            _offset = _tool.position - point; _smooth = _tool.position;
            Debug.Log("[Screwdriver] picked up");
        }

        void Drop()
        {
            if (_holder == null) return;
            _holder = null; Held = false; _pinching = true; // wait for pinch release before re-grab
            Debug.Log("[Screwdriver] released");
        }

        void TurnScrews()
        {
            if (Time.time - _lastTurn < TurnInterval) return;
            Vector3 tip = _tool.TransformPoint(_tipLocal);
            foreach (V2Story.CaseScrew cs in V2Story.CaseScrews)
            {
                if (cs == null || !cs.Active || cs.T == null) continue;
                if (Vector3.Distance(tip, cs.T.position) <= TipRadius)
                {
                    _lastTurn = Time.time;
                    cs.Poke();
                    _tool.Rotate(0f, 0f, 40f, Space.Self);
                    return;
                }
            }
            foreach (ScrewTarget st in _screws)
            {
                if (st == null || !st.Active || st.Tightened) continue;
                WatchPart wp = st.GetComponent<WatchPart>();
                if (wp == null || wp.State != PartState.Installed) continue;
                float r = TipRadius * Mathf.Max(1f, st.transform.lossyScale.x);
                if (Vector3.Distance(tip, st.transform.position) <= r)
                {
                    _lastTurn = Time.time;
                    st.Poke();
                    _tool.Rotate(0f, 0f, 40f, Space.Self);
                    return;
                }
            }
        }
    }
}
