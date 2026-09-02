using UnityEngine;
using DG.Tweening;

namespace Dermisache.Environment
{
    public class ZoneReveal : MonoBehaviour
    {
        [SerializeField] private string _opacityProperty = "_Opacity";
        [SerializeField] private Renderer[] _renderers;
        [SerializeField, Min(0f)] private float _duration = 1.5f;
        [SerializeField] private float _startDelay = 0.6f;
        [SerializeField] private Ease _ease = Ease.InOutSine;
        [SerializeField] private GameObject[] _enableOnRevealed;
        [SerializeField] private Collider[] _disableOnRevealed;

        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private MaterialPropertyBlock[] _mpbs;
        private float _opacity;
        private Tween _tween;
        private bool _revealed;

        public bool IsRevealed => _revealed;

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }
            _mpbs = new MaterialPropertyBlock[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _mpbs[i] = new MaterialPropertyBlock();
            }
            ApplyOpacity(0f);
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }

        public void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            _tween?.Kill();
            _tween = DOTween.To(() => _opacity, SetOpacity, 1f, _duration)
                .SetDelay(_startDelay)
                .SetEase(_ease)
                .OnComplete(OnRevealed);
        }

        private void SetOpacity(float value)
        {
            _opacity = value;
            ApplyOpacity(value);
        }

        private void ApplyOpacity(float value)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpbs[i]);
                _mpbs[i].SetFloat(OpacityId, value);
                r.SetPropertyBlock(_mpbs[i]);
            }
        }

        private void OnRevealed()
        {
            for (int i = 0; i < _enableOnRevealed.Length; i++)
            {
                if (_enableOnRevealed[i] != null) _enableOnRevealed[i].SetActive(true);
            }
            for (int i = 0; i < _disableOnRevealed.Length; i++)
            {
                if (_disableOnRevealed[i] != null) _disableOnRevealed[i].enabled = false;
            }
        }
    }
}