using System;
using UnityEngine;
using UnityEngine.InputSystem;
using YesChef.Interaction;

namespace YesChef.Player
{
    /// <summary>
    /// Moves the player to clicked kitchen stations, interacts on arrival, and owns the
    /// player's single item carry slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float GroundedVerticalVelocity = -2f;
        private const float MovementInputThreshold = 0.0001f;
        private const float InsideColliderSqrThreshold = 0.0001f;
        [Header("Station Selection Input")]
        [Tooltip("Assign the UI/Point action from InputSystem_Actions.")]
        [SerializeField] private InputActionReference _pointerPositionAction;
        [Tooltip("Assign the UI/Click action from InputSystem_Actions.")]
        [SerializeField] private InputActionReference _pointerPressAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _movementSpeed = 5f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 720f;
        [SerializeField] private float _gravity = -20f;
        [Tooltip("The fixed top-down camera used to raycast clicked stations.")]
        [SerializeField] private Camera _stationaryCamera;

        [Header("Interaction")]
        [SerializeField] private Transform _interactionOrigin;
        [SerializeField, Min(0.01f)] private float _interactionRadius = 1.25f;
        [SerializeField] private LayerMask _interactableLayers = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField, Min(0.01f)] private float _selectionDistance = 100f;

        [Header("Carrying")]
        [SerializeField] private Transform _holdPoint;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = true;

        private CharacterController _characterController;
        private InputAction _pointerPositionInputAction;
        private InputAction _pointerPressInputAction;
        private float _verticalVelocity;
        private GameObject _heldItem;
        private Component _selectedStation;
        private Collider _selectedStationCollider;
        private bool _movementEnabled = true;
        private bool _interactionEnabled = true;

        /// <summary>
        /// Raised after the player gains, hands off, or discards an item. UI should subscribe
        /// to this event instead of polling <see cref="HeldItem"/>.
        /// </summary>
        public event Action<GameObject> HeldItemChanged;

        public GameObject HeldItem => _heldItem;
        public bool IsCarryingItem => _heldItem != null;
        public bool CanCarryItem => !IsCarryingItem;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _pointerPositionInputAction = _pointerPositionAction != null ? _pointerPositionAction.action : null;
            _pointerPressInputAction = _pointerPressAction != null ? _pointerPressAction.action : null;

            if (_stationaryCamera == null)
            {
                _stationaryCamera = Camera.main;
            }

            if (_stationaryCamera == null)
            {
                Debug.LogError("[PlayerController] No stationary camera is assigned or tagged MainCamera.", this);
            }
        }

        private void OnEnable()
        {
            if (_pointerPositionInputAction != null)
            {
                _pointerPositionInputAction.Enable();
            }

            if (_pointerPressInputAction != null)
            {
                _pointerPressInputAction.performed += OnPointerPressed;
                _pointerPressInputAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_pointerPositionInputAction != null)
            {
                _pointerPositionInputAction.Disable();
            }

            if (_pointerPressInputAction != null)
            {
                _pointerPressInputAction.performed -= OnPointerPressed;
                _pointerPressInputAction.Disable();
            }

            ClearSelectedStation();
        }

        private void Update()
        {
            Vector3 movement = _movementEnabled ? GetMovementTowardsSelectedStation() : Vector3.zero;

            ApplyGravity();
            movement *= _movementSpeed;
            movement.y = _verticalVelocity;
            _characterController.Move(movement * Time.deltaTime);
        }

        /// <summary>
        /// Enables or disables player locomotion without changing their carried item.
        /// Game state transitions can call this in response to their state-change event.
        /// </summary>
        public void SetMovementEnabled(bool isEnabled)
        {
            _movementEnabled = isEnabled;
            Log($"Movement {(isEnabled ? "enabled" : "disabled")}.");

            if (!isEnabled)
            {
                ClearSelectedStation();
            }
        }

        /// <summary>
        /// Enables or disables nearby-station interactions.
        /// </summary>
        public void SetInteractionEnabled(bool isEnabled)
        {
            _interactionEnabled = isEnabled;
            Log($"Station interaction {(isEnabled ? "enabled" : "disabled")}.");
        }

        /// <summary>
        /// Places an item in the player's only carry slot. Stations should call this only after
        /// their own acceptance rules have passed.
        /// </summary>
        public bool TryPickUp(GameObject kitchenItem)
        {
            if (kitchenItem == null || !CanCarryItem)
            {
                Log("Could not pick up item: the carry slot is already occupied or the item is missing.");
                return false;
            }

            Transform parent = _holdPoint != null ? _holdPoint : transform;

            // Pass 'true' so Unity adjusts localScale to keep the item's original world scale
            kitchenItem.transform.SetParent(parent, true);
            kitchenItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            _heldItem = kitchenItem;
            HeldItemChanged?.Invoke(_heldItem);
            Log($"Picked up '{kitchenItem.name}' and attached it to the hold point.");
            return true;
        }

        /// <summary>
        /// Removes and returns the carried item. The receiving station owns the item's next
        /// state, including whether it is placed, processed, or destroyed.
        /// </summary>
        public GameObject ReleaseHeldItem()
        {
            GameObject releasedItem = _heldItem;
            if (releasedItem == null)
            {
                Log("Could not release item: the carry slot is empty.");
                return null;
            }

            _heldItem = null;
            releasedItem.transform.SetParent(null, true);
            HeldItemChanged?.Invoke(null);
            Log($"Released '{releasedItem.name}' from the hold point.");
            return releasedItem;
        }

        private void OnPointerPressed(InputAction.CallbackContext context)
        {
            if (!_movementEnabled || !_interactionEnabled || _stationaryCamera == null || _pointerPositionInputAction == null)
            {
                Log("Ignored station selection because movement, interaction, camera, or pointer input is unavailable.");
                return;
            }

            Vector2 screenPosition = _pointerPositionInputAction.ReadValue<Vector2>();
            Ray selectionRay = _stationaryCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                    selectionRay,
                    out RaycastHit hit,
                    _selectionDistance,
                    _interactableLayers,
                    _triggerInteraction))
            {
                Log("No interactable station was selected.");
                ClearSelectedStation();
                return;
            }

            Component stationComponent = hit.collider.GetComponentInParent(typeof(IInteractable));
            if (stationComponent is IInteractable)
            {
                _selectedStation = stationComponent;
                _selectedStationCollider = hit.collider;
                Log($"Selected station '{stationComponent.name}'. Moving into interaction range.");
            }
            else
            {
                Log($"'{hit.collider.name}' does not implement IInteractable.");
                ClearSelectedStation();
            }
        }

        private Vector3 GetMovementTowardsSelectedStation()
        {
            if (_selectedStation == null || _selectedStationCollider == null)
            {
                ClearSelectedStation();
                return Vector3.zero;
            }

            Vector3 origin = _interactionOrigin != null ? _interactionOrigin.position : transform.position;
            float effectiveInteractionRadius = GetEffectiveInteractionRadius();
            if (IsInInteractionRange(origin, effectiveInteractionRadius))
            {
                Log($"Arrived at '{_selectedStation.name}'. Attempting interaction.");
                AttemptSelectedInteraction();
                return Vector3.zero;
            }

            Vector3 movement = GetApproachPoint(origin) - origin;
            movement.y = 0f;
            if (movement.sqrMagnitude <= MovementInputThreshold)
            {
                Log($"Reached the collider of '{_selectedStation.name}'. Attempting interaction.");
                AttemptSelectedInteraction();
                return Vector3.zero;
            }

            movement.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime);
            return movement;
        }

        private void ApplyGravity()
        {
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = GroundedVerticalVelocity;
            }

            _verticalVelocity += _gravity * Time.deltaTime;
        }

        private float GetEffectiveInteractionRadius()
        {
            if (_selectedStation is IInteractable interactable)
            {
                return Mathf.Min(_interactionRadius, interactable.InteractionRadius);
            }

            return _interactionRadius;
        }

        private Vector3 GetSelectedStationInteractionCenter()
        {
            return _selectedStation is IInteractable interactable
                ? interactable.InteractionCenter
                : _selectedStation.transform.position;
        }

        private bool IsInInteractionRange(Vector3 origin, float interactionRadius)
        {
            float radiusSqr = interactionRadius * interactionRadius;
            Vector3 toCenter = GetSelectedStationInteractionCenter() - origin;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude <= radiusSqr)
            {
                return true;
            }

            if (_selectedStationCollider == null)
            {
                return false;
            }

            Vector3 toClosestPoint = _selectedStationCollider.ClosestPoint(origin) - origin;
            toClosestPoint.y = 0f;
            return toClosestPoint.sqrMagnitude <= radiusSqr;
        }

        private Vector3 GetApproachPoint(Vector3 origin)
        {
            Vector3 interactionCenter = GetSelectedStationInteractionCenter();
            if (_selectedStationCollider == null)
            {
                return interactionCenter;
            }

            Vector3 closestToCenter = _selectedStationCollider.ClosestPoint(interactionCenter);
            bool centerIsInsideCollider = (closestToCenter - interactionCenter).sqrMagnitude <= InsideColliderSqrThreshold;
            return centerIsInsideCollider
                ? _selectedStationCollider.ClosestPoint(origin)
                : interactionCenter;
        }

        private void AttemptSelectedInteraction()
        {
            if (_selectedStation is IInteractable interactable)
            {
                bool interactionSucceeded = interactable.TryInteract(this);
                Log($"Station '{_selectedStation.name}' interaction {(interactionSucceeded ? "succeeded" : "was rejected")}.");
            }
            else
            {
                Log($"Selected station '{_selectedStation.name}' is no longer interactable.");
            }

            ClearSelectedStation();
        }

        private void ClearSelectedStation()
        {
            _selectedStation = null;
            _selectedStationCollider = null;
        }

        private void OnDrawGizmos()
        {
            Vector3 origin = _interactionOrigin != null ? _interactionOrigin.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, _interactionRadius);
        }

        private void Log(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[PlayerController] {message}", this);
            }
        }
    }
}
