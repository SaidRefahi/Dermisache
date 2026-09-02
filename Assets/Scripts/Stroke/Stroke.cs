using System;
using UnityEngine;

namespace Dermisache.Stroke
{
    public enum StrokeState
    {
        Active,
        Erased
    }

    public enum StrokeType
    {
        Soft,
        Hard
    }

    public class Stroke : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private StrokeType _type = StrokeType.Soft;
        [SerializeField, Range(0f, 1f)] private float _eraseProgress;
        [SerializeField] private StrokeState _state = StrokeState.Active;

        public event Action<Stroke> StrokeEliminated;

        public string Id => _id;
        public StrokeType Type => _type;
        public bool IsObjective => _type == StrokeType.Hard;
        public float EraseProgress => _eraseProgress;
        public StrokeState State => _state;
        public bool IsErased => _state == StrokeState.Erased;

        public void SetEraseProgress(float value)
        {
            if (IsErased) return;

            _eraseProgress = Mathf.Clamp01(value);
            CompleteEraseIfReady();
        }

        private void CompleteEraseIfReady()
        {
            if (_eraseProgress < 1f) return;

            _eraseProgress = 1f;
            _state = StrokeState.Erased;
            StrokeEliminated?.Invoke(this);
        }

        private void OnValidate()
        {
            if (IsErased)
            {
                _eraseProgress = 1f;
                return;
            }

            _eraseProgress = Mathf.Clamp01(_eraseProgress);
            CompleteEraseIfReady();
        }

        [ContextMenu("Reset Erase")]
        private void ResetErase()
        {
            _eraseProgress = 0f;
            _state = StrokeState.Active;
        }
    }
}
