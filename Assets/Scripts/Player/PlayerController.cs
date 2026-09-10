using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using YesChef.Core;
using YesChef.Interaction;
using YesChef.Stations;

namespace YesChef.Player
{
    /// <summary>
    /// Moves the player to clicked kitchen stations using NavMesh pathfinding, 
    /// interacts on arrival, and owns the player's single item carry slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerController : MonoBehaviour
    {
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

        private NavMeshAgent _navMeshAgent;
        private InputAction _pointerPositionInputAction;
        private InputAction _pointerPressInputAction;
        private GameObject _heldItem;
        private Component _selectedStation;
        private Collider _selectedStationCollider;
        private bool _movementEnabled = true;
        private bool _interactionEnabled = true;

        /// <summary>
        /// Raised after the player gains, hands off, or discards an item.
        /// </summary>
        public event Action<GameObject> HeldItemChanged;

        public GameObject HeldItem => _heldItem;
        public bool IsCarryingItem => _heldItem != null;
        public bool CanCarryItem => !IsCarryingItem;

        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();

            // Sync inspector settings to NavMeshAgent
            _navMeshAgent.speed = _movementSpeed;
            _navMeshAgent.angularSpeed = _rotationSpeed;

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
            GameManager.OnRoundEnded += CallReleaseHeldItem;
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

            GameManager.OnRoundEnded -= CallReleaseHeldItem;

            ClearSelectedStation();
        }

        private void Update()
        {
            if (!_movementEnabled || _selectedStation == null)
            {
                return;
            }

            CheckStationArrivalAndInteraction();
        }

        public void SetMovementEnabled(bool isEnabled)
        {
            _movementEnabled = isEnabled;
            _navMeshAgent.isStopped = !isEnabled;
            Log($"Movement {(isEnabled ? "enabled" : "disabled")}.");

            if (!isEnabled)
            {
                ClearSelectedStation();
            }
        }
        private void CallReleaseHeldItem()
        {
            ReleaseHeldItem();
        }

        public void SetInteractionEnabled(bool isEnabled)
        {
            _interactionEnabled = isEnabled;
            Log($"Station interaction {(isEnabled ? "enabled" : "disabled")}.");
        }

        public bool TryPickUp(GameObject kitchenItem)
        {
            if (kitchenItem == null || !CanCarryItem)
            {
                Log("Could not pick up item: the carry slot is already occupied or the item is missing.");
                return false;
            }

            Transform parent = _holdPoint != null ? _holdPoint : transform;
            kitchenItem.transform.SetParent(parent, true);
            kitchenItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            _heldItem = kitchenItem;
            HeldItemChanged?.Invoke(_heldItem);
            Log($"Picked up '{kitchenItem.name}' and attached it to the hold point.");
            return true;
        }

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
            if (GameManager.CurrentGameState != GameManager.GameState.Playing)
            {
                Log("Ignored station selection because the game is not in the Playing state.");
                return;
            }
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

                Vector3 origin = _interactionOrigin != null ? _interactionOrigin.position : transform.position;
                Vector3 destination = GetApproachPoint(origin);

                _navMeshAgent.isStopped = false;
                _navMeshAgent.stoppingDistance = GetEffectiveInteractionRadius();
                _navMeshAgent.SetDestination(destination);

                Log($"Selected station '{stationComponent.name}'. Pathfinding to target.");
            }
            else
            {
                Log($"'{hit.collider.name}' does not implement IInteractable.");
                ClearSelectedStation();
            }
        }

        private void CheckStationArrivalAndInteraction()
        {
            if (_selectedStation == null || _selectedStationCollider == null)
            {
                ClearSelectedStation();
                return;
            }

            Vector3 origin = _interactionOrigin != null ? _interactionOrigin.position : transform.position;
            float effectiveInteractionRadius = GetEffectiveInteractionRadius();

            // Check if player is within range or path complete
            if (IsInInteractionRange(origin, effectiveInteractionRadius) || 
               (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance))
            {
                Log($"Arrived at '{_selectedStation.name}'. Attempting interaction.");
                AttemptSelectedInteraction();
            }
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
            bool shouldTurnAround = false;

            if (_selectedStation is IInteractable interactable)
            {
                bool interactionSucceeded = interactable.TryInteract(this);
                shouldTurnAround = interactionSucceeded && ShouldTurnAroundAfterInteraction(interactable);
                Log($"Station '{_selectedStation.name}' interaction {(interactionSucceeded ? "succeeded" : "was rejected")}.");
            }
            else
            {
                Log($"Selected station '{_selectedStation.name}' is no longer interactable.");
            }

            ClearSelectedStation();

            if (shouldTurnAround)
            {
                transform.Rotate(0f, 180f, 0f, Space.World);
            }
        }

        private static bool ShouldTurnAroundAfterInteraction(IInteractable interactable)
        {
            return interactable is FridgeStation
                   or StoveStation
                   or TrashStation
                   or TableStation;
        }

        private void ClearSelectedStation()
        {
            _selectedStation = null;
            _selectedStationCollider = null;

            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
            }
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
