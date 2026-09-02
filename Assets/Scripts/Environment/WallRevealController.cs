using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using DG.Tweening;
using Dermisache.Stroke;
using StrokeRef = Dermisache.Stroke.Stroke;

namespace Dermisache.Environment
{
    public class WallRevealController : MonoBehaviour
    {
        [SerializeField] private List<StrokeRef> _requiredStrokes = new List<StrokeRef>();
        [SerializeField] private Transform _wallPivot;
        [SerializeField] private Renderer _wallRenderer;
        [SerializeField] private ZoneReveal _zone;

        [Header("Rotation")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField, Range(0f, 180f)] private float _openAngle = 60f;
        [SerializeField, Min(0.05f)] private float _rotateDuration = 1.2f;
        [SerializeField] private Ease _rotateEase = Ease.InOutCubic;
        [SerializeField, Range(0f, 20f)] private float _settleDegrees = 3f;
        [SerializeField, Min(0.05f)] private float _settleDuration = 0.6f;

        [Header("Unlock feedback")]
        [SerializeField] private Color _unlockEmission = new Color(0.4f, 1.4f, 0.6f);
        [SerializeField, Min(0.05f)] private float _unlockFlashDuration = 0.6f;
        [SerializeField, Min(0f)] private float _unlockFlashPeak = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _hapticAmp = 0.35f;
        [SerializeField, Min(0.01f)] private float _hapticDur = 0.25f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly HashSet<StrokeRef> _remaining = new HashSet<StrokeRef>();
        private bool _revealed;
        private bool _subscribed;
        private Quaternion _startLocalRot;
        private float _unlockAnimStart = -1f;
        private MaterialPropertyBlock _mpb;
        private Tween _tween;
        private XRBaseController[] _controllers;
        private HapticImpulsePlayer[] _hapticPlayers;
        private string _rigName = "XR Origin (XR Rig)";

        public bool IsRevealed => _revealed;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_wallPivot != null) _startLocalRot = _wallPivot.localRotation;
            _remaining.Clear();
            for (int i = 0; i < _requiredStrokes.Count; i++)
            {
                StrokeRef s = _requiredStrokes[i];
                if (s != null && !s.IsErased) _remaining.Add(s);
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _tween?.Kill();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            for (int i = 0; i < _requiredStrokes.Count; i++)
            {
                StrokeRef s = _requiredStrokes[i];
                if (s != null) s.StrokeEliminated += OnStrokeEliminated;
            }
            _subscribed = true;

            if (_requiredStrokes.Count > 0 && _remaining.Count == 0)
            {
                TriggerReveal();
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            for (int i = 0; i < _requiredStrokes.Count; i++)
            {
                StrokeRef s = _requiredStrokes[i];
                if (s != null) s.StrokeEliminated -= OnStrokeEliminated;
            }
            _subscribed = false;
        }

        private void OnStrokeEliminated(StrokeRef stroke)
        {
            if (stroke == null) return;
            _remaining.Remove(stroke);
            if (_remaining.Count == 0)
            {
                TriggerReveal();
            }
        }

        private void TriggerReveal()
        {
            if (_revealed) return;
            _revealed = true;
            PulseHaptic(_hapticAmp, _hapticDur);
            _unlockAnimStart = Time.time;

            if (_wallPivot != null)
            {
                _tween?.Kill();
                Quaternion openRot = _startLocalRot * Quaternion.Euler(_rotationAxis * _openAngle);
                Quaternion settleRot = _startLocalRot * Quaternion.Euler(_rotationAxis * Mathf.Max(0f, _openAngle - _settleDegrees));
                _tween = _wallPivot.DORotateQuaternion(openRot, _rotateDuration)
                    .SetEase(_rotateEase)
                    .OnComplete(() =>
                    {
                        _tween = _wallPivot.DORotateQuaternion(settleRot, _settleDuration)
                            .SetEase(Ease.InOutBack);
                    });
            }

            if (_zone != null) _zone.Reveal();
        }

        private void Update()
        {
            if (_unlockAnimStart < 0f || _wallRenderer == null) return;
            float elapsed = Time.time - _unlockAnimStart;
            float t = Mathf.Clamp01(elapsed / _unlockFlashDuration);
            float env = (1f - t) * Mathf.Sin(t * Mathf.PI);
            _wallRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, _unlockEmission * (_unlockFlashPeak * env));
            _wallRenderer.SetPropertyBlock(_mpb);
            if (t >= 1f) _unlockAnimStart = -1f;
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

        private void PulseHaptic(float amp, float dur)
        {
            if (_controllers == null || _hapticPlayers == null) CacheControllers();
            if (_controllers != null)
            {
                for (int i = 0; i < _controllers.Length; i++)
                {
                    if (_controllers[i] != null) _controllers[i].SendHapticImpulse(amp, dur);
                }
            }
            if (_hapticPlayers != null)
            {
                for (int i = 0; i < _hapticPlayers.Length; i++)
                {
                    if (_hapticPlayers[i] != null) _hapticPlayers[i].SendHapticImpulse(amp, dur);
                }
            }
        }
    }
}