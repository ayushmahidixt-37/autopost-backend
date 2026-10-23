using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Displays a collected object's smooth 3D model for inspection (drag to rotate, pinch to zoom).</summary>
    public sealed class CollectionViewer : MonoBehaviour
    {
        GameManager _game;
        Transform _root;
        Transform _pop;
        GameObject _model;
        float _time;

        public bool IsShowing { get { return _model != null; } }

        public void Initialize(GameManager game)
        {
            _game = game;
            _root = new GameObject("ViewerRoot").transform;
            _root.SetParent(transform, false);
            // The pop container animates scale; the model inside keeps the scale/offset from FitToHeight.
            _pop = new GameObject("Pop").transform;
            _pop.SetParent(_root, false);
        }

        public void Show(ObjectTemplate template)
        {
            Hide();
            _model = ObjectModelBuilder.Build(template, _pop);
            ObjectModelBuilder.FitToHeight(_model.transform, template, 2.4f);
            _pop.localScale = Vector3.zero;
            _time = 0f;
            var cam = _game.CameraRig;
            cam.SetTarget(_root, 1.7f, true, true);
            cam.SetFraming(1.05f, 0.05f);
            cam.SetAutoSpin(14f);
            cam.UserControlEnabled = true;
            _game.Environment.SetShadow(1.4f, true);
            _game.Audio.Play(Sfx.Reveal);
        }

        void Update()
        {
            if (_model == null || _time > 0.5f) return;
            _time += Time.unscaledDeltaTime;
            _pop.localScale = Vector3.one * Tween.EaseOutBack(Mathf.Clamp01(_time / 0.45f));
        }

        public void Hide()
        {
            if (_model != null) Destroy(_model);
            _model = null;
            if (_game == null) return;
            _game.CameraRig.SetAutoSpin(0f);
            _game.Environment.SetShadow(1f, false);
        }
    }
}
