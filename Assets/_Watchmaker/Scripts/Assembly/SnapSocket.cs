using UnityEngine;

namespace Watchmaker.Assembly
{
    public enum GhostState { Hidden, Correct, Wrong }

    /// <summary>Install location for one part. Distances are measured in the movement's local space, so they are scale-invariant.</summary>
    public sealed class SnapSocket : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color CorrectColor = new Color(0.23f, 0.82f, 0.44f, 1f);
        static readonly Color WrongColor = new Color(0.90f, 0.28f, 0.30f, 1f);

        [SerializeField] string _acceptsPartId;
        [Tooltip("Real-world metres. Release inside this radius of the correct socket installs the part.")]
        [SerializeField] float _captureRadius = 0.006f;
        [Tooltip("Real-world metres. Used for the precision score.")]
        [SerializeField] float _installTolerance = 0.0015f;
        [SerializeField] SnapSocket[] _prerequisites;
        [SerializeField] Renderer _ghost;

        MaterialPropertyBlock _block;
        GhostState _ghostState = GhostState.Hidden;

        public string AcceptsPartId => _acceptsPartId;
        public float CaptureRadius => _captureRadius;
        public float InstallTolerance => _installTolerance;
        public bool IsFilled { get; private set; }

        public void Configure(string acceptsPartId, float captureRadius, float installTolerance, SnapSocket[] prerequisites, Renderer ghost)
        {
            _acceptsPartId = acceptsPartId; _captureRadius = captureRadius; _installTolerance = installTolerance;
            _prerequisites = prerequisites; _ghost = ghost;
        }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_ghost != null) _ghost.enabled = false;
        }

        public bool PrerequisitesMet()
        {
            if (_prerequisites == null) return true;
            for (int i = 0; i < _prerequisites.Length; i++)
                if (_prerequisites[i] != null && !_prerequisites[i].IsFilled) return false;
            return true;
        }

        public string FirstMissingPrerequisite()
        {
            if (_prerequisites == null) return null;
            for (int i = 0; i < _prerequisites.Length; i++)
                if (_prerequisites[i] != null && !_prerequisites[i].IsFilled) return _prerequisites[i].AcceptsPartId;
            return null;
        }

        public void MarkFilled() { IsFilled = true; SetGhost(GhostState.Hidden); }

        public void SetGhost(GhostState state)
        {
            if (_ghost == null || state == _ghostState) return;
            _ghostState = state;
            _ghost.enabled = state != GhostState.Hidden;
            if (state == GhostState.Hidden) return;
            _block.SetColor(BaseColor, state == GhostState.Correct ? CorrectColor : WrongColor);
            _ghost.SetPropertyBlock(_block);
        }
    }
}
