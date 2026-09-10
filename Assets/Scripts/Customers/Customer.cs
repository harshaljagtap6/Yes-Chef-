using System;
using UnityEngine;
using YesChef.Data;
using YesChef.Items;
using YesChef.Stations;

namespace YesChef.Customers
{
    /// <summary>
    /// Spawned beside a customer window, holds one order, and waits until it is fulfilled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Customer : MonoBehaviour
    {
        [SerializeField] private bool _enableDebugLogs = true;

        private CustomerWindowStation _station;
        private CustomerOrder _order;

        /// <summary>
        /// Raised when this customer is given a new order.
        /// </summary>
        public event Action<CustomerOrder> OrderAssigned;

        /// <summary>
        /// Raised after a matching prepared item is accepted toward the order.
        /// </summary>
        public event Action<IngredientSO> ItemReceived;

        /// <summary>
        /// Raised when every requested item has been delivered.
        /// </summary>
        public event Action<Customer, CustomerOrder> OrderCompleted;

        public CustomerWindowStation Station => _station;
        public CustomerOrder Order => _order;
        public bool IsWaiting => _order != null && !_order.IsComplete;

        public void Initialize(CustomerWindowStation station, CustomerOrder order)
        {
            _station = station;
            _order = order;
            OrderAssigned?.Invoke(_order);
            Log(DescribeOrder());
        }

        public bool CanAccept(KitchenItem kitchenItem)
        {
            return IsWaiting && _order.CanAccept(kitchenItem);
        }

        public bool TryReceiveItem(KitchenItem kitchenItem)
        {
            if (!IsWaiting || !_order.TryFulfill(kitchenItem))
            {
                return false;
            }

            ItemReceived?.Invoke(kitchenItem.Ingredient);
            Log($"Received prepared {kitchenItem.Ingredient.DisplayName}. Remaining: {_order.RemainingCount}.");

            if (_order.IsComplete)
            {
                Log("Order complete. Leaving the window.");
                OrderCompleted?.Invoke(this, _order);
            }

            return true;
        }

        private string DescribeOrder()
        {
            if (_order == null || _order.RequestedItems.Length == 0)
            {
                return "Waiting with an empty order.";
            }

            string summary = "Waiting for";
            IngredientSO[] items = _order.RequestedItems;
            for (int index = 0; index < items.Length; index++)
            {
                IngredientSO ingredient = items[index];
                if (ingredient == null)
                {
                    continue;
                }

                summary = index == 0
                    ? $"{summary} {ingredient.DisplayName}"
                    : $"{summary}, {ingredient.DisplayName}";
            }

            return summary + ".";
        }

        private void Log(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[Customer] {message}", this);
            }
        }
    }
}
