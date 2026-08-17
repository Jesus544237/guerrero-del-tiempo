using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Zoom y desplazamiento muy lentos sobre la ilustracion de fondo, para que
    /// el menu nunca parezca una imagen estatica.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class KenBurns : MonoBehaviour
    {
        [SerializeField] float minZoom = 1.03f;
        [SerializeField] float maxZoom = 1.12f;
        [SerializeField] float zoomPeriod = 42f;
        [SerializeField] Vector2 panAmplitude = new Vector2(26f, 16f);
        [SerializeField] Vector2 panPeriod = new Vector2(55f, 37f);

        RectTransform _rect;
        Vector2 _basePos;

        void Start()
        {
            _rect = (RectTransform)transform;
            _basePos = _rect.anchoredPosition;
        }

        void Update()
        {
            if (_rect == null) return;
            float t = Time.unscaledTime;

            float k = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.01f, zoomPeriod));
            float zoom = Mathf.Lerp(minZoom, maxZoom, k);
            _rect.localScale = new Vector3(zoom, zoom, 1f);

            float x = panPeriod.x > 0.01f ? Mathf.Sin(t * Mathf.PI * 2f / panPeriod.x) * panAmplitude.x : 0f;
            float y = panPeriod.y > 0.01f ? Mathf.Cos(t * Mathf.PI * 2f / panPeriod.y) * panAmplitude.y : 0f;
            _rect.anchoredPosition = _basePos + new Vector2(x, y);
        }
    }
}
