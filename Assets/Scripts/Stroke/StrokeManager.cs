using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dermisache.Stroke
{
    [DisallowMultipleComponent]
    public class StrokeManager : MonoBehaviour
    {
        public static StrokeManager Instance { get; private set; }

        public event Action<Stroke> ObjectiveErased;
        public event Action AllObjectivesErased;

        [SerializeField] private List<Stroke> _strokes = new List<Stroke>();
        [SerializeField] private bool _autoCollectOnAwake = true;

        private readonly HashSet<Stroke> _objectivesRemaining = new HashSet<Stroke>();

        public int ObjectiveTotal { get; private set; }
        public int ObjectivesRemaining => _objectivesRemaining.Count;
        public bool IsCompleted => _strokes.Count > 0 && _objectivesRemaining.Count == 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (_autoCollectOnAwake)
            {
                var found = FindObjectsByType<Stroke>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (!_strokes.Contains(found[i])) _strokes.Add(found[i]);
                }
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _strokes.Count; i++)
            {
                var s = _strokes[i];
                if (s == null) continue;
                s.StrokeEliminated += OnStrokeEliminated;
            }

            RebuildObjectiveSet();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _strokes.Count; i++)
            {
                if (_strokes[i] != null) _strokes[i].StrokeEliminated -= OnStrokeEliminated;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterStroke(Stroke stroke)
        {
            if (stroke == null || _strokes.Contains(stroke)) return;
            _strokes.Add(stroke);
            if (isActiveAndEnabled)
            {
                stroke.StrokeEliminated += OnStrokeEliminated;
                if (stroke.IsObjective) _objectivesRemaining.Add(stroke);
            }
        }

        public void RebuildObjectiveSet()
        {
            _objectivesRemaining.Clear();
            for (int i = 0; i < _strokes.Count; i++)
            {
                var s = _strokes[i];
                if (s != null && s.IsObjective) _objectivesRemaining.Add(s);
            }
            ObjectiveTotal = _objectivesRemaining.Count;

            if (_strokes.Count > 0 && _objectivesRemaining.Count == 0)
            {
                AllObjectivesErased?.Invoke();
            }
        }

        private void OnStrokeEliminated(Stroke stroke)
        {
            if (stroke == null) return;
            if (!stroke.IsObjective) return;

            _objectivesRemaining.Remove(stroke);
            ObjectiveErased?.Invoke(stroke);
            if (_objectivesRemaining.Count == 0)
            {
                AllObjectivesErased?.Invoke();
            }
        }
    }
}
