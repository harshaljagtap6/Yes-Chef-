using UnityEngine;
using YesChef.Data;
using YesChef.Items;

namespace YesChef.Customers
{
    /// <summary>
    /// Runtime ticket of prepared ingredients a customer is waiting for.
    /// </summary>
    public sealed class CustomerOrder
    {
        private readonly IngredientSO[] _requestedItems;
        private readonly bool[] _fulfilled;
        private int _remainingCount;
        private readonly float _startedAtRealtime;

        public CustomerOrder(IngredientSO[] requestedItems)
        {
            _requestedItems = requestedItems ?? System.Array.Empty<IngredientSO>();
            _fulfilled = new bool[_requestedItems.Length];
            _remainingCount = 0;
            _startedAtRealtime = Time.time;

            for (int index = 0; index < _requestedItems.Length; index++)
            {
                if (_requestedItems[index] != null)
                {
                    _remainingCount++;
                }
                else
                {
                    _fulfilled[index] = true;
                }
            }
        }

        public int ItemCount => _requestedItems.Length;
        public int RemainingCount => _remainingCount;
        public bool IsComplete => _remainingCount <= 0;
        public float ElapsedSeconds => Time.time - _startedAtRealtime;
        public IngredientSO[] RequestedItems => _requestedItems;

        public bool CanAccept(KitchenItem kitchenItem)
        {
            return FindFulfillableSlot(kitchenItem) >= 0;
        }

        public bool TryFulfill(KitchenItem kitchenItem)
        {
            int slotIndex = FindFulfillableSlot(kitchenItem);
            if (slotIndex < 0)
            {
                return false;
            }

            _fulfilled[slotIndex] = true;
            _remainingCount--;
            return true;
        }

        public bool IsSlotFulfilled(int index)
        {
            return index < 0 || index >= _fulfilled.Length || _fulfilled[index];
        }

        public int CalculateScore()
        {
            int baseValue = 0;
            for (int index = 0; index < _requestedItems.Length; index++)
            {
                IngredientSO ingredient = _requestedItems[index];
                if (ingredient != null)
                {
                    baseValue += ingredient.BaseValue;
                }
            }

            int timePenalty = Mathf.FloorToInt(ElapsedSeconds);
            return baseValue - timePenalty;
        }

        private int FindFulfillableSlot(KitchenItem kitchenItem)
        {
            if (kitchenItem == null || kitchenItem.Ingredient == null || !kitchenItem.IsPrepared)
            {
                return -1;
            }

            for (int index = 0; index < _requestedItems.Length; index++)
            {
                if (_fulfilled[index])
                {
                    continue;
                }

                if (_requestedItems[index] == kitchenItem.Ingredient)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
