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
    /// Single chopping slot for raw vegetables. Progresses only while the player stays nearby.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TableStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _itemAnchor;
        [SerializeField] private Image _progressImage;
        [SerializeField, Min(0.01f)] private float _playerPresenceRadius = 1.75f;
        [SerializeField] private Transform _interactionCenter;
        [SerializeField, Min(0.01f)] private float _interactionRadius = 1.25f;
        [SerializeField] private bool _enableDebugLogs = true;

        private KitchenItem _placedItem;
        private PlayerController _choppingPlayer;
        private float _chopProgress;
        private bool _isChopping;

        /// <summary>
        /// Raised with a 0–1 value whenever chopping progress changes. UI should subscribe
        /// instead of polling station state.
        /// </summary>
        public static event Action<float> ProgressChanged;

        /// <summary>
        /// Raised once when the placed vegetable finishes chopping.
        /// </summary>
        public static event Action ChoppingCompleted;

        public KitchenItem PlacedItem => _placedItem;
        public float Progress => _chopProgress;
        public bool IsOccupied => _placedItem != null;
        public bool IsChopping => _isChopping;
        public float InteractionRadius => _interactionRadius;
        public Vector3 InteractionCenter => _interactionCenter != null ? _interactionCenter.position : transform.position;

        private void Update()
        {
            if (!_isChopping || _placedItem == null || _placedItem.Ingredient == null)
            {
                return;
            }

            // if (!IsChoppingPlayerNearby())
            // {
            //     return;
            // }

            float duration = Mathf.Max(_placedItem.Ingredient.PrepDuration, 0.0001f);
            _chopProgress = Mathf.Min(1f, _chopProgress + (Time.deltaTime / duration));
            SetProgressVisual(_chopProgress);
            ProgressChanged?.Invoke(_chopProgress);

            if (_chopProgress < 1f)
            {
                return;
            }

            CompleteChopping();
        }

        private void OnEnable()
        {
            GameManager.OnRoundEnded += ResetStation;
        }

        private void OnDisable()
        {
            GameManager.OnRoundEnded -= ResetStation;
        }

        public bool TryInteract(PlayerController player)
        {
            Log($"Interaction requested. Occupied: {IsOccupied}; chopping: {IsChopping}.");

            if (player == null)
            {
                Log("Rejected interaction: PlayerController is missing.");
                return false;
            }

            if (_placedItem == null)
            {
                return TryPlaceVegetable(player);
            }

            if (!player.CanCarryItem)
            {
                Log("Rejected interaction: the player must have an empty carry slot to operate this occupied table.");
                return false;
            }

            if (_placedItem.PrepState != PrepState.Chopped)
            {
                _choppingPlayer = player;
                _isChopping = true;
                Log("Chopping resumed for the placed vegetable.");
                return true;
            }

            return TryGiveItemToPlayer(player);
        }

        private bool TryPlaceVegetable(PlayerController player)
        {
            if (!player.IsCarryingItem || player.HeldItem == null)
            {
                Log("Rejected placement: the player is not carrying an item.");
                return false;
            }

            if (!player.HeldItem.TryGetComponent(out KitchenItem kitchenItem))
            {
                Log("Rejected placement: the carried object has no KitchenItem component.");
                return false;
            }

            if (!IsValidVegetable(kitchenItem))
            {
                Log("Rejected placement: only raw vegetables can be chopped here.");
                return false;
            }

            GameObject releasedItem = player.ReleaseHeldItem();
            if (releasedItem == null)
            {
                Log("Rejected placement: the player could not release the carried vegetable.");
                return false;
            }

            _placedItem = kitchenItem;
            _chopProgress = 0f;
            _isChopping = true;
            _choppingPlayer = player;
            AttachToAnchor(_placedItem.transform);
            SetProgressVisual(0f);
            ProgressChanged?.Invoke(0f);
            Log($"Placed {kitchenItem.Ingredient.DisplayName}; chopping started.");
            return true;
        }

        private bool TryGiveItemToPlayer(PlayerController player)
        {
            if (!player.TryPickUp(_placedItem.gameObject))
            {
                Log("Rejected pickup: the player could not accept the chopped vegetable.");
                return false;
            }

            _placedItem = null;
            _choppingPlayer = null;
            _chopProgress = 0f;
            _isChopping = false;
            SetProgressVisual(0f);
            ProgressChanged?.Invoke(0f);
            Log("Transferred the chopped vegetable to the player's hold point.");
            return true;
        }

        private void CompleteChopping()
        {
            _isChopping = false;
            _chopProgress = 1f;
            _placedItem.SetPrepState(PrepState.Chopped);
            SetProgressVisual(1f);
            ProgressChanged?.Invoke(1f);
            ChoppingCompleted?.Invoke();
            Log($"Finished chopping {_placedItem.Ingredient.DisplayName}.");
        }

        private void ResetStation()
        {
            if (_placedItem != null)
            {
                Destroy(_placedItem.gameObject);
            }

            _placedItem = null;
            _choppingPlayer = null;
            _chopProgress = 0f;
            _isChopping = false;
            SetProgressVisual(0f);
            ProgressChanged?.Invoke(0f);
            Log("Cleared the table for the next round.");
        }

        private bool IsChoppingPlayerNearby()
        {
            if (_choppingPlayer == null)
            {
                return false;
            }

            Vector3 offset = _choppingPlayer.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _playerPresenceRadius * _playerPresenceRadius;
        }

        private static bool IsValidVegetable(KitchenItem kitchenItem)
        {
            return kitchenItem.Ingredient != null
                   && kitchenItem.Matches(IngredientType.Vegetable, PrepState.Raw)
                   && kitchenItem.Ingredient.RequiresPreparation;
        }

        private void AttachToAnchor(Transform itemTransform)
        {
            Transform parent = _itemAnchor != null ? _itemAnchor : transform;
            itemTransform.SetParent(parent, false);
            itemTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void SetProgressVisual(float progress)
        {
            // if (_progressImage != null)
            // {
            //     _progressImage.fillAmount = progress;
            // }
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
                Debug.Log($"[TableStation] {message}", this);
            }
        }
    }
}
