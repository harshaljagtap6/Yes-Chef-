using UnityEngine;
using YesChef.Player;

namespace YesChef.Interaction
{
    /// <summary>
    /// Contract implemented by every kitchen station the player can use.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Maximum distance at which this station accepts a player interaction.
        /// </summary>
        float InteractionRadius { get; }

        /// <summary>
        /// World-space center of this station's interaction radius.
        /// </summary>
        Vector3 InteractionCenter { get; }

        /// <summary>
        /// Attempts the station-specific interaction. Return false when the station cannot
        /// accept the player's current state, such as an incompatible carried item.
        /// </summary>
        bool TryInteract(PlayerController player);
    }
}
