using System;
using TMPro;
using UnityEngine;
using YesChef.Customers;
using YesChef.Data;
using YesChef.Interaction;
using YesChef.Items;
using YesChef.Player;
using YesChef.Scoring;

namespace YesChef.Stations
{
    /// <summary>
    /// One customer window. Spawns a waiting customer nearby and only accepts prepared
    /// items that still remain on that customer's order.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CustomerWindowStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private Customer _customerPrefab;
        [SerializeField] private Transform _customerWaitPoint;
        [SerializeField] private IngredientSO[] _menuIngredients;
        [SerializeField, Min(1)] private int _minItemsPerOrder = 1;
        [SerializeField, Min(1)] private int _maxItemsPerOrder = 3;
        [SerializeField, Min(0f)] private float _respawnCooldown = 5f;
        [SerializeField] private Transform _interactionCenter;
        [SerializeField, Min(0.01f)] private float _interactionRadius = 1.25f;
        [SerializeField] private TMP_Text _orderText;
        [SerializeField] private bool _enableDebugLogs = true;

        private Customer _currentCustomer;
        private float _cooldownRemaining;
        private bool _isOnCooldown;

        /// <summary>
        /// Raised when a new customer arrives with an order.
        /// </summary>
        public event Action<Customer, CustomerOrder> CustomerArrived;

        /// <summary>
        /// Raised when a delivered item matches the current order. The player no longer holds the item.
        /// </summary>
        public event Action<IngredientSO> OrderItemAccepted;

        /// <summary>
        /// Raised when the window rejects an item. The player still holds it.
        /// </summary>
        public event Action<KitchenItem> OrderItemRejected;

        /// <summary>
        /// Raised after the full order is delivered, with the awarded order score.
        /// </summary>
        public event Action<CustomerOrder, int> OrderCompleted;

        public Customer CurrentCustomer => _currentCustomer;
        public CustomerOrder CurrentOrder => _currentCustomer != null ? _currentCustomer.Order : null;
        public bool IsOnCooldown => _isOnCooldown;
        public float InteractionRadius => _interactionRadius;
        public Vector3 InteractionCenter => _interactionCenter != null ? _interactionCenter.position : transform.position;

        private void Awake()
        {
            if (_orderText == null)
            {
                _orderText = GetComponentInChildren<TMP_Text>(true);
            }

            RefreshOrderText();
        }

        private void Start()
        {
            SpawnCustomer();
        }

        private void Update()
        {
            if (!_isOnCooldown)
            {
                return;
            }

            _cooldownRemaining -= Time.deltaTime;
            if (_cooldownRemaining > 0f)
            {
                return;
            }

            _isOnCooldown = false;
            _cooldownRemaining = 0f;
            SpawnCustomer();
        }

        private void OnDestroy()
        {
            DespawnCustomer();
        }

        public bool TryInteract(PlayerController player)
        {
            Log("Interaction requested.");

            if (player == null || !player.IsCarryingItem || player.HeldItem == null)
            {
                Log("Rejected interaction: player is missing or not carrying an item.");
                return false;
            }

            if (_isOnCooldown || _currentCustomer == null || !_currentCustomer.IsWaiting)
            {
                Log("Rejected interaction: no customer is waiting at this window.");
                OrderItemRejected?.Invoke(null);
                return false;
            }

            if (!player.HeldItem.TryGetComponent(out KitchenItem kitchenItem))
            {
                Log("Rejected interaction: carried object is not a kitchen item. Player keeps the item.");
                OrderItemRejected?.Invoke(null);
                return false;
            }

            if (!_currentCustomer.CanAccept(kitchenItem))
            {
                Log("Rejected interaction: item is not a remaining prepared order ingredient. Player keeps the item.");
                OrderItemRejected?.Invoke(kitchenItem);
                return false;
            }

            GameObject releasedItem = player.ReleaseHeldItem();
            if (releasedItem == null)
            {
                Log("Rejected interaction: the item could not be taken from the player.");
                return false;
            }

            IngredientSO deliveredIngredient = kitchenItem.Ingredient;
            bool received = _currentCustomer.TryReceiveItem(kitchenItem);
            Destroy(releasedItem);

            if (!received)
            {
                Log("Order state rejected the item after release; this should not happen after CanAccept.");
                return false;
            }

            OrderItemAccepted?.Invoke(deliveredIngredient);
            Log($"Accepted prepared {deliveredIngredient.DisplayName}.");
            RefreshOrderText();

            if (_currentCustomer.Order.IsComplete)
            {
                CompleteCurrentOrder();
            }

            return true;
        }

        private void CompleteCurrentOrder()
        {
            CustomerOrder completedOrder = _currentCustomer.Order;
            int orderScore = completedOrder.CalculateScore();
            ScoreTracker.AddOrderScore(orderScore);
            OrderCompleted?.Invoke(completedOrder, orderScore);
            Log($"Order complete. Score awarded: {orderScore}. Total: {ScoreTracker.CurrentScore}.");

            DespawnCustomer();
            BeginCooldown();
        }

        private void SpawnCustomer()
        {
            if (_customerPrefab == null)
            {
                Log("Cannot spawn a customer because the customer prefab is not assigned.");
                return;
            }

            CustomerOrder order = CreateRandomOrder();
            if (order == null || order.RemainingCount == 0)
            {
                Log("Cannot spawn a customer because the menu has no valid ingredients.");
                return;
            }

            Vector3 spawnPosition = _customerWaitPoint != null ? _customerWaitPoint.position : transform.position;
            Quaternion spawnRotation = _customerWaitPoint != null ? _customerWaitPoint.rotation : transform.rotation;
            _currentCustomer = Instantiate(_customerPrefab, spawnPosition, spawnRotation);
            _currentCustomer.Initialize(this, order);
            CustomerArrived?.Invoke(_currentCustomer, order);
            RefreshOrderText();
            Log("Spawned a waiting customer.");
        }

        private void DespawnCustomer()
        {
            if (_currentCustomer == null)
            {
                return;
            }

            Destroy(_currentCustomer.gameObject);
            _currentCustomer = null;
        }

        private void BeginCooldown()
        {
            _isOnCooldown = true;
            _cooldownRemaining = _respawnCooldown;
            RefreshOrderText();
            Log($"Window cooling down for {_respawnCooldown:0.##} seconds.");
        }

        private CustomerOrder CreateRandomOrder()
        {
            int availableCount = CountAvailableIngredients();
            if (availableCount <= 0)
            {
                return null;
            }

            int maxItems = Mathf.Clamp(_maxItemsPerOrder, 1, availableCount);
            int minItems = Mathf.Clamp(_minItemsPerOrder, 1, maxItems);
            int itemCount = UnityEngine.Random.Range(minItems, maxItems + 1);
            IngredientSO[] requestedItems = new IngredientSO[itemCount];
            int filledCount = 0;
            int safetyLimit = availableCount * 4;

            for (int attempt = 0; attempt < safetyLimit && filledCount < itemCount; attempt++)
            {
                IngredientSO candidate = _menuIngredients[UnityEngine.Random.Range(0, _menuIngredients.Length)];
                if (candidate == null || ContainsIngredient(requestedItems, filledCount, candidate))
                {
                    continue;
                }

                requestedItems[filledCount] = candidate;
                filledCount++;
            }

            if (filledCount < itemCount)
            {
                IngredientSO[] packed = new IngredientSO[filledCount];
                for (int index = 0; index < filledCount; index++)
                {
                    packed[index] = requestedItems[index];
                }

                requestedItems = packed;
            }

            return new CustomerOrder(requestedItems);
        }

        private int CountAvailableIngredients()
        {
            if (_menuIngredients == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < _menuIngredients.Length; index++)
            {
                if (_menuIngredients[index] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsIngredient(IngredientSO[] items, int filledCount, IngredientSO candidate)
        {
            for (int index = 0; index < filledCount; index++)
            {
                if (items[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshOrderText()
        {
            if (_orderText == null)
            {
                return;
            }

            CustomerOrder order = CurrentOrder;
            if (_isOnCooldown || order == null || order.IsComplete)
            {
                _orderText.text = string.Empty;
                return;
            }

            _orderText.text = BuildRemainingOrderLabel(order);
        }

        private static string BuildRemainingOrderLabel(CustomerOrder order)
        {
            IngredientSO[] items = order.RequestedItems;
            string label = string.Empty;
            int writtenCount = 0;

            for (int index = 0; index < items.Length; index++)
            {
                if (order.IsSlotFulfilled(index))
                {
                    continue;
                }

                IngredientSO ingredient = items[index];
                if (ingredient == null)
                {
                    continue;
                }

                label = writtenCount == 0
                    ? ingredient.DisplayName
                    : $"{label}\n{ingredient.DisplayName}";
                writtenCount++;
            }

            return label;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(InteractionCenter, _interactionRadius);

            if (_customerWaitPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_customerWaitPoint.position, 0.25f);
            }
        }

        private void Log(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[CustomerWindowStation] {message}", this);
            }
        }
    }
}
