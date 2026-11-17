using UnityEngine;
using UnityEngine.InputSystem;

namespace Watchmaker.Core
{
    /// <summary>Editor/simulator only: lifts and tilts the table toward the viewer so webcam hands reach it and the top is visible.
    /// PageUp/PageDown = height, Home/End = distance, Insert/Delete = tilt.</summary>
    public sealed class SimTableLift : MonoBehaviour
    {
        Transform _lift, _panel;
        Vector3 _basePos, _panelBase;
        Vector3 _offset = new Vector3(0f, 0.10f, -0.02f);
        float _tilt = 50f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isEditor) return;
            new GameObject("SimTableLift").AddComponent<SimTableLift>();
        }

        void Start()
        {
            GameObject ws = GameObject.Find("WorkspaceRoot");
            _basePos = ws != null ? ws.transform.position : new Vector3(0f, 1f, 0.42f);
            _lift = new GameObject("TableLift").transform;
            _lift.position = _basePos;
            foreach (string n in new[] { "WorkspaceRoot", "Environment", "RestartButton" })
            {
                GameObject g = GameObject.Find(n);
                if (g != null) g.transform.SetParent(_lift, true);
            }
            GameObject p = GameObject.Find("InstructionPanel");
            if (p != null) { _panel = p.transform; _panelBase = _panel.position; }
            Apply();
        }

        void Apply()
        {
            if (_lift == null) return;
            _lift.SetPositionAndRotation(_basePos + _offset, Quaternion.Euler(-_tilt, 0f, 0f));
            if (_panel != null) _panel.position = _panelBase + new Vector3(0f, _offset.y + 0.12f, 0f);
        }

        void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            bool c = false;
            if (k.pageUpKey.wasPressedThisFrame) { _offset.y += 0.02f; c = true; }
            if (k.pageDownKey.wasPressedThisFrame) { _offset.y -= 0.02f; c = true; }
            if (k.homeKey.wasPressedThisFrame) { _offset.z -= 0.02f; c = true; }
            if (k.endKey.wasPressedThisFrame) { _offset.z += 0.02f; c = true; }
            if (k.insertKey.wasPressedThisFrame) { _tilt += 5f; c = true; }
            if (k.deleteKey.wasPressedThisFrame) { _tilt -= 5f; c = true; }
            if (c) { Apply(); Debug.Log("[SimTableLift] offset " + _offset.ToString("F2") + " tilt " + _tilt); }
        }
    }
}
