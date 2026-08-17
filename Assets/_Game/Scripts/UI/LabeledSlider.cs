using Ekkar.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Deslizador de volumen con etiqueta de porcentaje y un "tic" al moverse.
    /// La suscripcion se hace por codigo (evento de C#), no por UnityEvent, para
    /// que el panel de opciones sea el unico dueno de la logica.
    /// </summary>
    [AddComponentMenu("Ekkar/Labeled Slider")]
    public class LabeledSlider : MonoBehaviour
    {
        [SerializeField] Slider slider;
        [SerializeField] TMP_Text valueLabel;
        [SerializeField] string format = "{0}%";
        [SerializeField] float displayMultiplier = 100f;
        [SerializeField] bool playTick = true;

        public event System.Action<float> ValueChanged;

        int _lastShown = int.MinValue;
        bool _suppress;

        public Slider Slider => slider;

        public float Value
        {
            get => slider != null ? slider.value : 0f;
            set => SetValue(value, false);
        }

        void Awake()
        {
            if (slider == null) slider = GetComponentInChildren<Slider>(true);
            if (slider != null) slider.onValueChanged.AddListener(HandleSliderChanged);
        }

        void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(HandleSliderChanged);
        }

        public void SetValue(float value, bool notify)
        {
            if (slider == null) return;
            _suppress = !notify;
            slider.value = value;
            _suppress = false;
            Refresh();
        }

        void HandleSliderChanged(float value)
        {
            Refresh();
            if (_suppress) return;
            ValueChanged?.Invoke(value);
        }

        void Refresh()
        {
            if (slider == null) return;
            int shown = Mathf.RoundToInt(slider.value * displayMultiplier);
            if (shown == _lastShown) return;

            bool first = _lastShown == int.MinValue;
            _lastShown = shown;
            if (valueLabel != null) valueLabel.text = string.Format(format, shown);
            if (playTick && !first && !_suppress) AudioManager.Tick();
        }
    }
}
