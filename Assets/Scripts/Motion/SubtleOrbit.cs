using DG.Tweening;
using UnityEngine;

namespace Dermisache.Motion
{
    public class SubtleOrbit : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float _radius = 0.05f;
        [SerializeField, Min(0.1f)] private float _loopDuration = 4f;

        private Vector3 _origin;
        private float _directionSign = 1f;
        private Tween _tween;

        private void Awake()
        {
            _origin = transform.localPosition;
            _directionSign = Random.value < 0.5f ? 1f : -1f;

            _tween = DOVirtual.Float(0f, 360f, _loopDuration, OnOrbitUpdate)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }

        private void OnOrbitUpdate(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _directionSign * _radius;
            transform.localPosition = _origin + offset;
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }
    }
}
