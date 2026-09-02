using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using DG.Tweening;
using StrokeManagerRef = Dermisache.Stroke.StrokeManager;

namespace Dermisache.Door
{
    public class DoorController : MonoBehaviour
    {
        [SerializeField] private StrokeManagerRef _strokeManager;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private string _rigName = "XR Origin (XR Rig)";

        [Header("Slide")]
        [SerializeField, Min(0.1f)] private float _slideDistance = 2.5f;
        [SerializeField, Min(0.05f)] private float _slideDuration = 0.8f;
        [SerializeField] private Ease _slideEase = Ease.InOutQuad;

        [Header("Unlock feedback")]
        [SerializeField] private Color _unlockEmission = new Color(0.4f, 1.4f, 0.6f);
        [SerializeField, Min(0.05f)] private float _unlockFlashDuration = 0.6f;
        [SerializeField, Min(0f)] private float _unlockFlashPeak = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _unlockHapticAmp = 0.35f;
        [SerializeField, Min(0.01f)] private float _unlockHapticDur = 0.25f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private bool _isUnlocked;
        private Vector3 _closedLocalPos;
        private float _unlockAnimStart = -1f;
        private MaterialPropertyBlock _mpb;
        private XRBaseController[] _controllers;
        private HapticImpulsePlayer[] _hapticPlayers;
        private Tween _slideTween;
        private bool _subscribed;

        public bool IsUnlocked => _isUnlocked;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _closedLocalPos = transform.localPosition;
            CacheControllers();
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
            _slideTween?.Kill();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            if (_strokeManager == null)
            {
                _strokeManager = StrokeManagerRef.Instance;
            }
            if (_strokeManager == null) return;

            _strokeManager.AllObjectivesErased += OnAllObjectivesErased;
            _subscribed = true;

            if (_strokeManager.IsCompleted)
            {
                OnAllObjectivesErased();
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_strokeManager != null)
            {
                _strokeManager.AllObjectivesErased -= OnAllObjectivesErased;
            }
            _subscribed = false;
        }

        private void OnAllObjectivesErased()
        {
            Unlock();
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

        private void Unlock()
        {
            if (_isUnlocked) return;
            _isUnlocked = true;
            _unlockAnimStart = Time.time;
            PulseHaptic(_unlockHapticAmp, _unlockHapticDur);
            _slideTween?.Kill();
            var col = GetComponent<Collider>();
            _slideTween = transform.DOLocalMove(_closedLocalPos + Vector3.down * _slideDistance, _slideDuration)
                .SetEase(_slideEase)
                .OnComplete(() =>
                {
                    if (col != null) col.enabled = false;
                });
        }

        private void Update()
        {
            if (_unlockAnimStart < 0f || _renderer == null) return;
            float elapsed = Time.time - _unlockAnimStart;
            float t = Mathf.Clamp01(elapsed / _unlockFlashDuration);
            float env = (1f - t) * Mathf.Sin(t * Mathf.PI);
            float emission = _unlockFlashPeak * env;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, _unlockEmission * emission);
            _renderer.SetPropertyBlock(_mpb);
            if (t >= 1f) _unlockAnimStart = -1f;
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
