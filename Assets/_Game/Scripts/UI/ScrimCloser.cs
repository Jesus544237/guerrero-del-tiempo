using Ekkar.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ekkar.UI
{
    /// <summary>Cierra el panel al hacer clic fuera de la caja (sobre el velo).</summary>
    [AddComponentMenu("Ekkar/Scrim Closer")]
    public class ScrimCloser : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] UIPanel panel;
        [SerializeField] bool enabledForThisPanel = true;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!enabledForThisPanel || panel == null) return;
            if (panel.IsAnimating) return;
            AudioManager.Back();
            panel.Close();
        }
    }
}
