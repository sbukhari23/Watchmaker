using System;
using System.Collections;
using UnityEngine;

namespace Watchmaker.Assembly
{
    public enum PartState { InTray, Held, Moving, Installed }

    /// <summary>A grabbable watch component. Never falls: it is either in the tray, held, tweening, or installed.</summary>
    public sealed class WatchPart : MonoBehaviour
    {
        [SerializeField] string _partId;
        [SerializeField] string _displayName;
        [SerializeField] Transform _trayHome;
        [Tooltip("Interactable components disabled once the part is installed.")]
        [SerializeField] MonoBehaviour[] _interactables;
        [SerializeField] float _returnDuration = 0.4f;
        [SerializeField] float _snapDuration = 0.12f;

        Coroutine _move;

        public string PartId => _partId;
        public string DisplayName => _displayName;
        public PartState State { get; private set; } = PartState.InTray;

        public void Configure(string partId, string displayName, Transform trayHome, MonoBehaviour[] interactables)
        {
            _partId = partId; _displayName = displayName; _trayHome = trayHome; _interactables = interactables;
        }

        public void OnGrabbed()
        {
            if (_move != null) { StopCoroutine(_move); _move = null; }
            State = PartState.Held;
        }

        public void ReturnToTray()
        {
            if (_trayHome == null) return;
            MoveTo(_trayHome, _returnDuration, () => State = PartState.InTray);
        }

        public void SnapInto(Transform socket, Action onDone)
        {
            SetInteractable(false);
            MoveTo(socket, _snapDuration, () =>
            {
                transform.SetParent(socket, true);
                State = PartState.Installed;
                onDone?.Invoke();
            });
        }

        void SetInteractable(bool value)
        {
            if (_interactables == null) return;
            for (int i = 0; i < _interactables.Length; i++)
                if (_interactables[i] != null) _interactables[i].enabled = value;
        }

        void MoveTo(Transform target, float duration, Action onDone)
        {
            if (_move != null) StopCoroutine(_move);
            State = PartState.Moving;
            _move = StartCoroutine(MoveRoutine(target, duration, onDone));
        }

        IEnumerator MoveRoutine(Transform target, float duration, Action onDone)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPos, target.position, e),
                    Quaternion.SlerpUnclamped(startRot, target.rotation, e));
                yield return null;
            }
            transform.SetPositionAndRotation(target.position, target.rotation);
            _move = null;
            onDone?.Invoke();
        }
    }
}
