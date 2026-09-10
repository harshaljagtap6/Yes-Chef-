using UnityEngine;
using YesChef.Items;

namespace YesChef.Data
{
    /// <summary>
    /// Data-driven definition for an ingredient: scoring, prep rules, spawn prefab, and visuals.
    /// </summary>
    [CreateAssetMenu(fileName = "Ingredient", menuName = "YesChef/Ingredient")]
    public sealed class IngredientSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private IngredientType _ingredientType;
        [SerializeField] private int _baseValue;
        [SerializeField, Min(0f)] private float _prepDuration;
        [SerializeField] private PrepState _completedPrepState = PrepState.Raw;
        [SerializeField] private KitchenItem _prefab;
        [SerializeField] private Color _rawColor = Color.white;
        [SerializeField] private Color _preparedColor = Color.green;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public IngredientType IngredientType => _ingredientType;
        public int BaseValue => _baseValue;
        public float PrepDuration => _prepDuration;
        public PrepState CompletedPrepState => _completedPrepState;
        public KitchenItem Prefab => _prefab;
        public Color RawColor => _rawColor;
        public Color PreparedColor => _preparedColor;
        public bool RequiresPreparation => _prepDuration > 0f && _completedPrepState != PrepState.Raw;
    }
}
