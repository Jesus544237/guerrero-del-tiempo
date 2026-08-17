using Ekkar.Core;
using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Desplaza la capa segun la posicion del raton. Con distintas fuerzas por
    /// capa se consigue la profundidad del fondo.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIParallax : MonoBehaviour
    {
        [SerializeField] Vector2 strength = new Vector2(24f, 14f);
        [SerializeField] float smoothing = 5f;
        [SerializeField] bool invert = false;

        RectTransform _rect;
        Vector2 _basePos;
        Vector2 _offset;

        void Start()
        {
            _rect = (RectTransform)transform;
            _basePos = _rect.anchoredPosition;
        }

        void Update()
        {
            if (_rect == null) return;

            Vector2 target = Vector2.zero;
            if (InputCompat.HasPointer && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 m = InputCompat.MousePosition;
                float nx = Mathf.Clamp((m.x / Screen.width) * 2f - 1f, -1f, 1f);
                float ny = Mathf.Clamp((m.y / Screen.height) * 2f - 1f, -1f, 1f);
                target = new Vector2(nx * strength.x, ny * strength.y);
                if (invert) target = -target;
            }

            _offset = Vector2.Lerp(_offset, target, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
            _rect.anchoredPosition = _basePos + _offset;
        }
    }
}
