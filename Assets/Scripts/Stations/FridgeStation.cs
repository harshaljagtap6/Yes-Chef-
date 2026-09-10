using UnityEngine;
using YesChef.Data;
using YesChef.Interaction;
using YesChef.Items;
using YesChef.Player;

namespace YesChef.Stations
{
    /// <summary>
    /// Infinite supply of raw ingredients. Spawns a <see cref="KitchenItem"/> into an empty hand.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FridgeStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private IngredientSO[] _ingredients;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _interactionCenter;
        [SerializeField, Min(0.01f)] private float _interactionRadius = 1.25f;
        [SerializeField] private bool _enableDebugLogs = true;

        private int _nextIngredientIndex;

        public float InteractionRadius => _interactionRadius;
        public Vector3 InteractionCenter => _interactionCenter != null ? _interactionCenter.position : transform.position;

        public bool TryInteract(PlayerController player)
        {
            Log("Interaction requested.");

            if (player == null || !player.CanCarryItem || _ingredients == null || _ingredients.Length == 0)
            {
                Log("Rejected interaction: player is missing, already carrying an item, or no ingredients are configured.");
                return false;
            }

            IngredientSO ingredient = GetNextIngredient();
            if (ingredient == null || ingredient.Prefab == null)
            {
                Log("Rejected interaction: the selected ingredient or its prefab is missing.");
                return false;
            }

            Vector3 spawnPosition = _spawnPoint != null ? _spawnPoint.position : transform.position;
            Quaternion spawnRotation = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
            KitchenItem kitchenItem = Instantiate(ingredient.Prefab, spawnPosition, spawnRotation);
            kitchenItem.Initialize(ingredient, PrepState.Raw);
            Log($"Spawned raw {ingredient.DisplayName}.");

            if (!player.TryPickUp(kitchenItem.gameObject))
            {
                Destroy(kitchenItem.gameObject);
                Log("Could not transfer the spawned item to the player's hold point; item was destroyed.");
                return false;
            }

            Log($"Transferred {ingredient.DisplayName} to the player's hold point.");
            return true;
        }

        private IngredientSO GetNextIngredient()
        {
            int ingredientCount = _ingredients.Length;
            for (int offset = 0; offset < ingredientCount; offset++)
            {
                int index = (_nextIngredientIndex + offset) % ingredientCount;
                IngredientSO ingredient = _ingredients[index];
                if (ingredient == null)
                {
                    continue;
                }

                _nextIngredientIndex = (index + 1) % ingredientCount;
                return ingredient;
            }

            return null;
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
                Debug.Log($"[FridgeStation] {message}", this);
            }
        }
    }
}
