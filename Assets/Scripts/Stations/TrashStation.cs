using System;
using UnityEngine;
using YesChef.Interaction;
using YesChef.Player;

namespace YesChef.Stations
{
    /// <summary>
    /// Discards the item currently carried by the player.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TrashStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _interactionCenter;
        [SerializeField, Min(0.01f)] private float _interactionRadius = 1.25f;
        [SerializeField] private bool _enableDebugLogs = true;

        /// <summary>
        /// Raised immediately before the discarded item is destroyed.
        /// </summary>
        public event Action ItemDiscarded;

        public float InteractionRadius => _interactionRadius;
        public Vector3 InteractionCenter => _interactionCenter != null ? _interactionCenter.position : transform.position;

        public bool TryInteract(PlayerController player)
        {
            if (player == null)
            {
                Log("Rejected interaction: PlayerController is missing.");
                return false;
            }

            GameObject discardedItem = player.ReleaseHeldItem();
            if (discardedItem == null)
            {
                Log("Rejected interaction: the player is not carrying an item.");
                return false;
            }

            Log($"Discarding '{discardedItem.name}'.");
            ItemDiscarded?.Invoke();
            Destroy(discardedItem);
            return true;
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
                Debug.Log($"[TrashStation] {message}", this);
            }
        }
    }
}
