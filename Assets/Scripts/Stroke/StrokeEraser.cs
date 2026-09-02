using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Dermisache.Stroke
{
    [RequireComponent(typeof(Stroke))]
    public class StrokeEraser : MonoBehaviour
    {
        [SerializeField] private Stroke _stroke;
        [SerializeField, Min(0.1f)] private float _eraseDuration = 2f;
        [SerializeField] private XRBaseInteractable _interactable;

        private Collider _collider;
        private bool _isInteracting;
        private bool _subscribedToStroke;
        [SerializeField] private string _rigName = "XR Origin (XR Rig)";
        private Transform[] _controllerTransforms;

        public float EraseDuration => _eraseDuration;
        public bool IsInteracting => _isInteracting;

        private void Awake()
        {
            if (_stroke == null)
            {
                _stroke = GetComponent<Stroke>();
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.linearDamping = 0f;
                rb.angularDamping = 0.05f;
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            _collider = GetComponent<Collider>();
            if (_collider == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(0.6f, 0.4f, 0.4f);
                _collider = box;
            }
            else
            {
                _collider.isTrigger = true;
            }

            if (_interactable == null)
            {
                _interactable = GetComponent<XRBaseInteractable>();
            }

            if (_interactable == null)
            {
                var grab = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
                grab.trackPosition = false;
                grab.trackRotation = false;
                grab.trackScale = false;
                grab.throwOnDetach = false;
                _interactable = grab;
            }

            if (_interactable != null)
            {
                _interactable.interactionLayers = 1;
                _interactable.enabled = false;
                _interactable.enabled = true;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            if (_stroke != null && _stroke.IsErased)
            {
                DisableInteraction();
            }

            CacheControllers();
        }

        private void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.AddListener(OnHoverEntered);
                _interactable.hoverExited.AddListener(OnHoverExited);
                _interactable.selectEntered.AddListener(OnSelectEntered);
                _interactable.selectExited.AddListener(OnSelectExited);
            }

            if (_stroke != null && !_subscribedToStroke)
            {
                _stroke.StrokeEliminated += OnStrokeErased;
                _subscribedToStroke = true;
                if (_stroke.IsErased)
                {
                    OnStrokeErased(_stroke);
                }
            }
        }

        private void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.RemoveListener(OnHoverEntered);
                _interactable.hoverExited.RemoveListener(OnHoverExited);
                _interactable.selectEntered.RemoveListener(OnSelectEntered);
                _interactable.selectExited.RemoveListener(OnSelectExited);
            }

            if (_stroke != null && _subscribedToStroke)
            {
                _stroke.StrokeEliminated -= OnStrokeErased;
                _subscribedToStroke = false;
            }

            _isInteracting = false;
        }

        private void OnDestroy()
        {
            if (_stroke != null && _subscribedToStroke)
            {
                _stroke.StrokeEliminated -= OnStrokeErased;
                _subscribedToStroke = false;
            }
        }

        private void Update()
        {
            if (_stroke == null || _stroke.IsErased || _collider == null || !_collider.enabled)
            {
                _isInteracting = false;
                return;
            }

            _isInteracting = IsAimingAtStroke();

            if (!_isInteracting)
            {
                return;
            }

            float delta = Time.deltaTime / Mathf.Max(0.01f, _eraseDuration);
            float next = _stroke.EraseProgress + delta;
            _stroke.SetEraseProgress(next);
        }

        public void BeginErase()
        {
            if (_stroke == null || _stroke.IsErased)
            {
                return;
            }

            _isInteracting = true;
        }

        public void EndErase()
        {
            if (_interactable != null && (_interactable.isHovered || _interactable.isSelected))
            {
                return;
            }

            _isInteracting = false;
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            TryBeginErase();
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _isInteracting = false;
        }

        private void CacheControllers()
        {
            var rig = GameObject.Find(_rigName);
            if (rig == null)
            {
                _controllerTransforms = System.Array.Empty<Transform>();
                return;
            }

            var found = new System.Collections.Generic.List<Transform>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n == "Left Controller" || n == "Right Controller" || n == "Left Hand" || n == "Right Hand")
                {
                    found.Add(t);
                }
            }
            _controllerTransforms = found.ToArray();
        }

        private bool IsAimingAtStroke()
        {
            if (_controllerTransforms == null || _controllerTransforms.Length == 0)
            {
                CacheControllers();
                if (_controllerTransforms == null || _controllerTransforms.Length == 0)
                {
                    return false;
                }
            }

            for (int i = 0; i < _controllerTransforms.Length; i++)
            {
                var t = _controllerTransforms[i];
                if (t == null)
                {
                    continue;
                }

                Ray ray = new Ray(t.position, t.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Collide))
                {
                    if (hit.collider == _collider)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isInteracting ? Color.red : Color.yellow;
            if (_collider != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (_collider is BoxCollider box)
                {
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.4f, 0.4f));
            }
        }
#endif

        private void TryBeginErase()
        {
            if (_stroke == null || _stroke.IsErased)
            {
                return;
            }

            _isInteracting = true;
        }

        private void TryEndErase()
        {
            if (_interactable != null && _interactable.isSelected)
            {
                return;
            }

            _isInteracting = false;
        }

        private void OnStrokeErased(Stroke stroke)
        {
            _isInteracting = false;
            DisableInteraction();
        }

        private void DisableInteraction()
        {
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            if (_interactable != null)
            {
                _interactable.enabled = false;
            }

            enabled = false;
        }

        private void OnValidate()
        {
            if (_eraseDuration < 0.1f)
            {
                _eraseDuration = 0.1f;
            }
        }
    }
}
