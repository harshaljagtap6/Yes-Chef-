using UnityEngine;
using YesChef.Data;

namespace YesChef.Items
{
    /// <summary>
    /// Runtime instance of an ingredient the player can carry or place on a station.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitchenItem : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private IngredientSO _ingredient;
        [SerializeField] private Renderer _visualRenderer;

        private MaterialPropertyBlock _propertyBlock;
        private PrepState _prepState = PrepState.Raw;

        public IngredientSO Ingredient => _ingredient;
        public PrepState PrepState => _prepState;
        public IngredientType IngredientType => _ingredient != null ? _ingredient.IngredientType : default;
        public bool IsPrepared => _ingredient != null && _prepState == _ingredient.CompletedPrepState;

        private void Awake()
        {
            _propertyBlock ??= new MaterialPropertyBlock();

            if (_visualRenderer == null)
            {
                _visualRenderer = GetComponentInChildren<Renderer>();
            }

            ApplyVisual();
        }

        public void Initialize(IngredientSO ingredient, PrepState prepState = PrepState.Raw)
        {
            _ingredient = ingredient;
            _prepState = prepState;

            if (_visualRenderer == null)
            {
                _visualRenderer = GetComponentInChildren<Renderer>();
            }

            ApplyVisual();
        }

        public void SetPrepState(PrepState prepState)
        {
            _prepState = prepState;
            ApplyVisual();
        }

        public bool Matches(IngredientType ingredientType, PrepState prepState)
        {
            return _ingredient != null && _ingredient.IngredientType == ingredientType && _prepState == prepState;
        }

        private void ApplyVisual()
        {
            if (_visualRenderer == null || _ingredient == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            Color color = _prepState == _ingredient.CompletedPrepState && _ingredient.RequiresPreparation
                ? _ingredient.PreparedColor
                : _ingredient.RawColor;

            _visualRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            _visualRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
