using Ekkar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Creditos con desplazamiento automatico tipo rodillo de cine. El avance
    /// se detiene mientras el puntero esta encima para que el jugador pueda
    /// leer o desplazarse a mano.
    /// </summary>
    [AddComponentMenu("Ekkar/Credits Panel")]
    public class CreditsPanel : MonoBehaviour
    {
        [SerializeField] UIPanel panel;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform hoverArea;
        [SerializeField] float autoScrollSpeed = 0.045f;   // fraccion del recorrido por segundo
        [SerializeField] float startDelay = 2.2f;
        [SerializeField] bool autoScroll = true;

        float _delayLeft;

        void Awake()
        {
            if (panel == null) panel = GetComponent<UIPanel>();
            if (panel != null) panel.onOpened.AddListener(ResetScroll);
        }

        public void ResetScroll()
        {
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            _delayLeft = startDelay;
        }

        void Update()
        {
            if (!autoScroll || scroll == null || panel == null || !panel.IsOpen) return;

            if (_delayLeft > 0f)
            {
                _delayLeft -= Time.unscaledDeltaTime;
                return;
            }

            if (PointerIsOverArea()) return;
            if (scroll.verticalNormalizedPosition <= 0.0005f) return;

            scroll.verticalNormalizedPosition =
                Mathf.Max(0f, scroll.verticalNormalizedPosition - autoScrollSpeed * Time.unscaledDeltaTime);
        }

        bool PointerIsOverArea()
        {
            if (hoverArea == null || !InputCompat.HasPointer) return false;
            Canvas canvas = hoverArea.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(hoverArea, InputCompat.MousePosition, cam);
        }
    }
}
