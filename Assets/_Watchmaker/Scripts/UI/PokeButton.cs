using UnityEngine;
using UnityEngine.SceneManagement;
using Watchmaker.Core;

namespace Watchmaker.UI
{
    /// <summary>Physical button on the bench. Hidden until the watch is finished; a fingertip poke restarts the scene.</summary>
    public sealed class PokeButton : MonoBehaviour
    {
        [SerializeField] float _radius = 0.03f;

        PokeState _poke;
        Renderer[] _renderers;
        bool _shown;

        void Awake() { _renderers = GetComponentsInChildren<Renderer>(true); SetShown(false); }
        void OnEnable() => FlowEvents.StageChanged += OnStage;
        void OnDisable() => FlowEvents.StageChanged -= OnStage;
        void OnStage(Stage s) => SetShown(s == Stage.Complete);

        void SetShown(bool shown)
        {
            _shown = shown;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = shown;
        }

        void Update()
        {
            if (_shown && _poke.Update(transform.position, _radius, 1f)) Restart();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
