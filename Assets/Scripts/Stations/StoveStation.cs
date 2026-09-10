using System;
using UnityEngine;
using UnityEngine.UI;
using YesChef.Data;
using YesChef.Interaction;
using YesChef.Items;
using YesChef.Player;
using YesChef.Core;

namespace YesChef.Stations
{
    /// <summary>
    /// Two independent stove slots. Raw meat cooks hands-off, then can be picked up when done.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class StoveStation : MonoBehaviour, IInteractable
    {
        private const int SlotCount = 2;

        [SerializeField] private SlotView[] _slots = new SlotView[SlotCount];
        [SerializeField] private Transform _interactionCenter;
        [SerializeField, Min(0.01f)] private float _interactionRadius = 1.25f;
        [SerializeField] private bool _enableDebugLogs = true;

        private readonly SlotRuntime[] _slotRuntimes = new SlotRuntime[SlotCount];

        /// <summary>
        /// Raised with slot index and 0–1 progress whenever a slot's cook timer changes.
        /// </summary>
        public static event Action<int, float> SlotProgressChanged;

        /// <summary>
        /// Raised when a slot finishes cooking its meat.
        /// </summary>
        public static event Action<int> SlotCookingCompleted;

        public float InteractionRadius => _interactionRadius;
        public Vector3 InteractionCenter => _interactionCenter != null ? _interactionCenter.position : transform.position;

        private void Awake()
        {
            EnsureSlotArray();

            for (int index = 0; index < SlotCount; index++)
            {
                _slotRuntimes[index] = new SlotRuntime();
                SetSlotProgressVisual(index, 0f);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int index = 0; index < SlotCount; index++)
            {
                TickSlot(index, deltaTime);
            }
        }

        void OnEnable()
        {
            GameManager.OnRoundEnded += ResetSlots;
        }

        void OnDisable()
        {
            GameManager.OnRoundEnded -= ResetSlots;
        }

        public bool TryInteract(PlayerController player)
        {
            Log($"Interaction requested. Player carrying item: {player != null && player.IsCarryingItem}.");

            if (player == null)
            {
                Log("Rejected interaction: PlayerController is missing.");
                return false;
            }

            if (player.IsCarryingItem)
            {
                return TryPlaceMeat(player);
            }

            return TryTakeFromSlot(player);
        }

        private bool TryPlaceMeat(PlayerController player)
        {
            if (player.HeldItem == null || !player.HeldItem.TryGetComponent(out KitchenItem kitchenItem))
            {
                Log("Rejected placement: the carried object has no KitchenItem component.");
                return false;
            }

            if (!IsValidRawMeat(kitchenItem))
            {
                Log("Rejected placement: only raw meat can cook on the stove.");
                return false;
            }

            int slotIndex = FindFirstEmptySlot();
            if (slotIndex < 0)
            {
                Log("Rejected placement: both cooking slots are occupied.");
                return false;
            }

            GameObject releasedItem = player.ReleaseHeldItem();
            if (releasedItem == null)
            {
                Log("Rejected placement: the player could not release the raw meat.");
                return false;
            }

            SlotRuntime runtime = _slotRuntimes[slotIndex];
            runtime.Item = kitchenItem;
            runtime.Progress = 0f;
            runtime.IsCooking = true;
            AttachToSlot(slotIndex, kitchenItem.transform);
            SetSlotProgressVisual(slotIndex, 0f);
            SlotProgressChanged?.Invoke(slotIndex, 0f);
            Log($"Placed {kitchenItem.Ingredient.DisplayName} in slot {slotIndex + 1}; cooking started.");
            return true;
        }

        private bool TryTakeFromSlot(PlayerController player)
        {
            int slotIndex = FindBestPickupSlot();
            if (slotIndex < 0)
            {
                Log("Rejected pickup: there is no meat on the stove.");
                return false;
            }

            SlotRuntime runtime = _slotRuntimes[slotIndex];
            KitchenItem item = runtime.Item;
            if (item == null || !player.TryPickUp(item.gameObject))
            {
                Log("Rejected pickup: the player could not accept the stove item.");
                return false;
            }

            runtime.Item = null;
            runtime.Progress = 0f;
            runtime.IsCooking = false;
            SetSlotProgressVisual(slotIndex, 0f);
            SlotProgressChanged?.Invoke(slotIndex, 0f);
            Log($"Transferred the item from slot {slotIndex + 1} to the player's hold point.");
            return true;
        }

        private void TickSlot(int slotIndex, float deltaTime)
        {
            SlotRuntime runtime = _slotRuntimes[slotIndex];
            if (!runtime.IsCooking || runtime.Item == null || runtime.Item.Ingredient == null)
            {
                return;
            }

            float duration = Mathf.Max(runtime.Item.Ingredient.PrepDuration, 0.0001f);
            runtime.Progress = Mathf.Min(1f, runtime.Progress + (deltaTime / duration));
            SetSlotProgressVisual(slotIndex, runtime.Progress);
            SlotProgressChanged?.Invoke(slotIndex, runtime.Progress);

            if (runtime.Progress < 1f)
            {
                return;
            }

            runtime.IsCooking = false;
            runtime.Item.SetPrepState(PrepState.Cooked);
            SlotCookingCompleted?.Invoke(slotIndex);
            Log($"Finished cooking {runtime.Item.Ingredient.DisplayName} in slot {slotIndex + 1}.");
        }

        private int FindFirstEmptySlot()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                if (_slotRuntimes[index].Item == null)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindBestPickupSlot()
        {
            int cookingSlot = -1;

            for (int index = 0; index < SlotCount; index++)
            {
                SlotRuntime runtime = _slotRuntimes[index];
                if (runtime.Item == null)
                {
                    continue;
                }

                if (runtime.Item.PrepState == PrepState.Cooked)
                {
                    return index;
                }

                if (cookingSlot < 0)
                {
                    cookingSlot = index;
                }
            }

            return cookingSlot;
        }

        private void AttachToSlot(int slotIndex, Transform itemTransform)
        {
            Transform parent = transform;
            if (_slots != null && slotIndex < _slots.Length && _slots[slotIndex] != null && _slots[slotIndex].Anchor != null)
            {
                parent = _slots[slotIndex].Anchor;
            }

            itemTransform.SetParent(parent, false);
            itemTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void SetSlotProgressVisual(int slotIndex, float progress)
        {
            // if (_slots == null || slotIndex >= _slots.Length || _slots[slotIndex] == null)
            // {
            //     return;
            // }

            // Image progressImage = _slots[slotIndex].ProgressImage;
            // if (progressImage != null)
            // {
            //     progressImage.fillAmount = progress;
            // }
        }

        private void EnsureSlotArray()
        {
            if (_slots != null && _slots.Length == SlotCount)
            {
                return;
            }

            SlotView[] resized = new SlotView[SlotCount];
            if (_slots != null)
            {
                int copyCount = Mathf.Min(_slots.Length, SlotCount);
                for (int index = 0; index < copyCount; index++)
                {
                    resized[index] = _slots[index];
                }
            }

            _slots = resized;
        }

        private void ResetSlots()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                SlotRuntime runtime = _slotRuntimes[index];
                if (runtime.Item != null)
                {
                    Destroy(runtime.Item.gameObject);
                }

                runtime.Item = null;
                runtime.Progress = 0f;
                runtime.IsCooking = false;
                SetSlotProgressVisual(index, 0f);
                SlotProgressChanged?.Invoke(index, 0f);
            }

            Log("Cleared both stove slots for the next round.");
        }

        private static bool IsValidRawMeat(KitchenItem kitchenItem)
        {
            return kitchenItem.Ingredient != null
                   && kitchenItem.Matches(IngredientType.Meat, PrepState.Raw)
                   && kitchenItem.Ingredient.RequiresPreparation;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(InteractionCenter, _interactionRadius);
        }

        private void Log(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[StoveStation] {message}", this);
            }
        }

        [Serializable]
        private sealed class SlotView
        {
            [SerializeField] private Transform _anchor;
            [SerializeField] private Image _progressImage;

            public Transform Anchor => _anchor;
            public Image ProgressImage => _progressImage;
        }

        private sealed class SlotRuntime
        {
            public KitchenItem Item;
            public float Progress;
            public bool IsCooking;
        }
    }
}
