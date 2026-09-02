using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Dermisache.Stroke
{
    [DisallowMultipleComponent]
    public class StrokeEraseFeedback : MonoBehaviour
    {
        [SerializeField] private Stroke _stroke;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private string _rigName = "XR Origin (XR Rig)";

        [Header("Visual")]
        [SerializeField] private Color _nearCompleteTint = new Color(1.4f, 0.85f, 0.35f);
        [SerializeField, Min(0f)] private float _maxEmission = 2.2f;
        [SerializeField, Range(0f, 1f)] private float _nearCompleteThreshold = 0.85f;
        [SerializeField, Min(0.01f)] private float _deleteAnimDuration = 0.18f;
        [SerializeField, Min(1f)] private float _deleteScale = 1.25f;
        [SerializeField, Min(1f)] private float _deleteWidthMultiplier = 4f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] private float _hapticStartAmp = 0.05f;
        [SerializeField, Min(0.01f)] private float _hapticStartDur = 0.04f;
        [SerializeField, Range(0f, 1f)] private float _hapticNearAmp = 0.18f;
        [SerializeField, Min(0.01f)] private float _hapticNearDur = 0.05f;
        [SerializeField, Min(0.02f)] private float _hapticNearInterval = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _hapticFinishAmp = 0.45f;
        [SerializeField, Min(0.01f)] private float _hapticFinishDur = 0.18f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _mpb;
        private float _lastProgress;
        private bool _wasErasing;
        private float _deleteAnimStart = -1f;
        private Vector3 _baseScale;
        private float _baseWidth;
        private Color _baseStartColor;
        private Color _baseEndColor;
        private XRBaseController[] _controllers;
        private HapticImpulsePlayer[] _hapticPlayers;
        private float _lastNearHapticTime;

        private void Awake()
        {
            if (_stroke == null) _stroke = GetComponentInParent<Stroke>();
            if (_lineRenderer == null) _lineRenderer = GetComponentInChildren<LineRenderer>(true);
            if (_renderer == null && _lineRenderer != null) _renderer = _lineRenderer;
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>(true);

            _baseScale = transform.localScale;
            if (_lineRenderer != null)
            {
                _baseWidth = _lineRenderer.startWidth;
                _baseStartColor = _lineRenderer.startColor;
                _baseEndColor = _lineRenderer.endColor;
            }

            _mpb = new MaterialPropertyBlock();
            CacheControllers();
        }

        private void OnEnable()
        {
            if (_stroke != null) _stroke.StrokeEliminated += OnErased;
        }

        private void OnDisable()
        {
            if (_stroke != null) _stroke.StrokeEliminated -= OnErased;
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, Color.black);
                _renderer.SetPropertyBlock(_mpb);
            }
            transform.localScale = _baseScale;
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = _baseStartColor;
                _lineRenderer.endColor = _baseEndColor;
            }
        }

        private void CacheControllers()
        {
            var rig = GameObject.Find(_rigName);
            if (rig == null)
            {
                _controllers = System.Array.Empty<XRBaseController>();
                _hapticPlayers = System.Array.Empty<HapticImpulsePlayer>();
                return;
            }
            var ctrlList = new List<XRBaseController>();
            var hapList = new List<HapticImpulsePlayer>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Left Controller" && t.name != "Right Controller") continue;
                var xr = t.GetComponentInChildren<XRBaseController>(true);
                if (xr != null && !ctrlList.Contains(xr)) ctrlList.Add(xr);
                var hap = t.GetComponentInChildren<HapticImpulsePlayer>(true);
                if (hap != null && !hapList.Contains(hap)) hapList.Add(hap);
            }
            _controllers = ctrlList.ToArray();
            _hapticPlayers = hapList.ToArray();
        }

        private void Update()
        {
            if (_stroke == null) return;
            if (_stroke.IsErased) return;

            float progress = _stroke.EraseProgress;
            bool isErasing = progress > _lastProgress + 0.0001f && Time.deltaTime > 0f;

            if (_renderer != null)
            {
                float emission = 0f;
                if (isErasing)
                {
                    emission = Mathf.Lerp(0f, _maxEmission, progress);
                    if (progress >= _nearCompleteThreshold)
                    {
                        float pulse = (Mathf.Sin(Time.time * 14f) + 1f) * 0.5f;
                        emission = Mathf.Max(emission, _maxEmission * 0.8f + pulse * 1.4f);
                    }
                }
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, _nearCompleteTint * emission);
                _renderer.SetPropertyBlock(_mpb);
            }

            if (isErasing && !_wasErasing)
            {
                PulseHaptic(_hapticStartAmp, _hapticStartDur);
            }
            if (isErasing && progress >= _nearCompleteThreshold)
            {
                if (Time.time - _lastNearHapticTime >= _hapticNearInterval)
                {
                    _lastNearHapticTime = Time.time;
                    PulseHaptic(_hapticNearAmp, _hapticNearDur);
                }
            }

            _wasErasing = isErasing;
            _lastProgress = progress;
        }

        private void LateUpdate()
        {
            if (_stroke == null) return;

            if (_stroke.IsErased)
            {
                TickDeleteAnimation();
                return;
            }

            if (_lineRenderer != null)
            {
                float progress = Mathf.Clamp01(_stroke.EraseProgress);
                float t = Mathf.Clamp01((progress - 0.5f) / 0.5f);
                Color startC = _baseStartColor;
                Color endC = _baseEndColor;
                startC.r = Mathf.Lerp(startC.r, _nearCompleteTint.r, t * 0.6f);
                startC.g = Mathf.Lerp(startC.g, _nearCompleteTint.g, t * 0.6f);
                startC.b = Mathf.Lerp(startC.b, _nearCompleteTint.b, t * 0.6f);
                endC.r = Mathf.Lerp(endC.r, _nearCompleteTint.r, t * 0.6f);
                endC.g = Mathf.Lerp(endC.g, _nearCompleteTint.g, t * 0.6f);
                endC.b = Mathf.Lerp(endC.b, _nearCompleteTint.b, t * 0.6f);
                _lineRenderer.startColor = startC;
                _lineRenderer.endColor = endC;
            }
        }

        private void OnErased(Stroke stroke)
        {
            _deleteAnimStart = Time.time;
            _lastNearHapticTime = -1f;
            PulseHaptic(_hapticFinishAmp, _hapticFinishDur);
        }

        private void TickDeleteAnimation()
        {
            if (_deleteAnimStart < 0f) return;
            float elapsed = Time.time - _deleteAnimStart;
            float t = Mathf.Clamp01(elapsed / _deleteAnimDuration);
            float env = Mathf.Sin(t * Mathf.PI);

            transform.localScale = _baseScale * Mathf.LerpUnclamped(1f, _deleteScale, env);

            if (_lineRenderer != null)
            {
                float width = _baseWidth * Mathf.LerpUnclamped(1f, _deleteWidthMultiplier, env);
                _lineRenderer.startWidth = width;
                _lineRenderer.endWidth = width;
            }

            if (_renderer != null)
            {
                float flash = (1f - t) * (_maxEmission + 1f);
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, Color.white * flash);
                _renderer.SetPropertyBlock(_mpb);
            }

            if (t >= 1f)
            {
                _deleteAnimStart = -1f;
                if (_lineRenderer != null)
                {
                    _lineRenderer.startWidth = 0f;
                    _lineRenderer.endWidth = 0f;
                }
                transform.localScale = _baseScale;
                gameObject.SetActive(false);
            }
        }

        private void PulseHaptic(float amp, float dur)
        {
            if (_controllers == null || _hapticPlayers == null) CacheControllers();
            if (_controllers != null)
            {
                for (int i = 0; i < _controllers.Length; i++)
                {
                    var c = _controllers[i];
                    if (c != null) c.SendHapticImpulse(amp, dur);
                }
            }
            if (_hapticPlayers != null)
            {
                for (int i = 0; i < _hapticPlayers.Length; i++)
                {
                    var h = _hapticPlayers[i];
                    if (h != null) h.SendHapticImpulse(amp, dur);
                }
            }
        }
    }
}
