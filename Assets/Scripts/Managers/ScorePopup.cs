using TMPro;
using UnityEngine;

namespace YesChef.UI
{
    /// <summary>
    /// Runtime UI feedback that rises and fades after an order awards score.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScorePopup : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private TextMeshProUGUI _label;
        private CanvasGroup _canvasGroup;
        private Vector2 _startPosition;
        private float _duration;
        private float _riseDistance;
        private float _elapsed;

        public static ScorePopup Create(RectTransform parent)
        {
            GameObject popupObject = new GameObject("Score Popup", typeof(RectTransform));
            popupObject.SetActive(false);
            popupObject.AddComponent<CanvasRenderer>();
            popupObject.AddComponent<TextMeshProUGUI>();
            popupObject.AddComponent<CanvasGroup>();
            popupObject.transform.SetParent(parent, false);
            popupObject.transform.SetAsLastSibling();
            ScorePopup popup = popupObject.AddComponent<ScorePopup>();
            popupObject.SetActive(true);
            return popup;
        }

        public void Initialize(
            int score,
            Vector2 startPosition,
            TMP_FontAsset font,
            Color color,
            float duration,
            float riseDistance)
        {
            _startPosition = startPosition;
            _duration = Mathf.Max(0.01f, duration);
            _riseDistance = Mathf.Max(0f, riseDistance);
            _elapsed = 0f;

            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.sizeDelta = new Vector2(180f, 56f);
            _rectTransform.anchoredPosition = _startPosition;

            _label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            _label.fontSize = 36f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = color;
            _label.raycastTarget = false;
            _label.SetText(score >= 0 ? "{0}" : "{0}", score);

            _canvasGroup.alpha = 1f;
        }

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _label = GetComponent<TextMeshProUGUI>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            _rectTransform.anchoredPosition = _startPosition + Vector2.up * (_riseDistance * progress);
            _canvasGroup.alpha = 1f - progress;

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
