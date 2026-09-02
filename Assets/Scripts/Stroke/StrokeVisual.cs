using UnityEngine;

namespace Dermisache.Stroke
{
    [RequireComponent(typeof(LineRenderer))]
    public class StrokeVisual : MonoBehaviour
    {
        [SerializeField] private Stroke _stroke;
        [SerializeField] private Color _color = new Color(1f, 0.8f, 0.2f);
        [SerializeField, Range(0.001f, 0.1f)] private float _width = 0.02f;

        private LineRenderer _line;
        private bool _subscribed;
        private float _lastProgress = -1f;

        private void Awake()
        {
            if (_stroke == null)
            {
                _stroke = GetComponentInParent<Stroke>();
            }

            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.startWidth = _width;
            _line.endWidth = _width;
            _line.startColor = _color;
            _line.endColor = _color;
            _lastProgress = _stroke != null ? _stroke.EraseProgress : -1f;
            ApplyVisual(_lastProgress);

            if (_stroke != null)
            {
                _stroke.StrokeEliminated += OnStrokeErased;
                _subscribed = true;
                if (_stroke.IsErased)
                {
                    OnStrokeErased(_stroke);
                }
            }
        }

        private void OnDestroy()
        {
            if (_stroke != null && _subscribed)
            {
                _stroke.StrokeEliminated -= OnStrokeErased;
            }
        }

        private void Update()
        {
            if (_stroke == null || _stroke.IsErased)
            {
                return;
            }

            if (_line == null)
            {
                return;
            }

            float progress = _stroke.EraseProgress;
            if (Mathf.Approximately(progress, _lastProgress))
            {
                return;
            }

            _lastProgress = progress;
            ApplyVisual(progress);
        }

        private void ApplyVisual(float progress)
        {
            if (_line == null)
            {
                return;
            }

            float t = Mathf.Clamp01(progress);
            float alpha = 1f - t;
            Color c = _color;
            c.a *= alpha;
            _line.startColor = c;
            _line.endColor = c;
            float w = _width * alpha;
            _line.startWidth = w;
            _line.endWidth = w;
        }

        private void OnStrokeErased(Stroke stroke)
        {
        }
    }
}
