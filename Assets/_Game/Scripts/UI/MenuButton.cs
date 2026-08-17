using System.Collections;
using Ekkar.Audio;
using Ekkar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Boton del menu con estetica pixel: marco, relleno, barra de acento,
    /// destello que barre de izquierda a derecha y punta de flecha que entra
    /// al enfocar. Hereda de Selectable para que la navegacion con teclado y
    /// mando funcione sin codigo extra.
    /// </summary>
    [AddComponentMenu("Ekkar/Menu Button")]
    public class MenuButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Header("Partes")]
        public Image frame;
        public Image fill;
        public Image accent;
        public Image shine;
        public Image arrow;
        public Image icon;
        public TMP_Text label;

        [Header("Colores en reposo")]
        public Color frameIdle  = new Color(0.49f, 0.23f, 0.93f, 0.55f);
        public Color fillIdle   = new Color(0.10f, 0.04f, 0.18f, 0.72f);
        public Color labelIdle  = new Color(0.89f, 0.91f, 0.94f, 1f);
        public Color accentIdle = new Color(0.49f, 0.23f, 0.93f, 0.45f);

        [Header("Colores al enfocar")]
        public Color frameActive  = new Color(0.13f, 0.83f, 0.93f, 1f);
        public Color fillActive   = new Color(0.02f, 0.34f, 0.42f, 0.55f);
        public Color labelActive  = Color.white;
        public Color accentActive = new Color(0.13f, 0.83f, 0.93f, 1f);

        [Header("Sensacion")]
        public float activeScale = 1.035f;
        public float pressScale = 0.975f;
        public float transitionTime = 0.16f;
        public float accentWidthIdle = 4f;
        public float accentWidthActive = 9f;
        public float arrowSlide = 18f;
        public float labelSpacingIdle = 4f;
        public float labelSpacingActive = 9f;

        [Header("Sonido")]
        public bool playHoverSound = true;
        public bool playClickSound = true;

        [Header("Al pulsar")]
        public UnityEvent onClick = new UnityEvent();

        float _k;                 // 0 = reposo, 1 = enfocado
        float _targetScale = 1f;
        bool _wasActive;
        Vector2 _arrowBasePos;
        Coroutine _visualCo, _shineCo;
        RectTransform _shineRect;
        float _shineTravel = 400f;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;

            if (arrow != null) _arrowBasePos = arrow.rectTransform.anchoredPosition;
            if (shine != null)
            {
                _shineRect = shine.rectTransform;
                _shineTravel = ((RectTransform)transform).rect.width + _shineRect.rect.width;
                SetShineAlpha(0f);
            }
        }

        protected override void Start()
        {
            base.Start();
            if (Application.isPlaying) Apply(0f);
        }

        // ------------------------------------------------------------ estado

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!Application.isPlaying) return;

            bool disabled = state == SelectionState.Disabled;
            bool pressed  = state == SelectionState.Pressed;
            bool active   = !disabled && (pressed
                            || state == SelectionState.Selected
                            || state == SelectionState.Highlighted);

            _targetScale = disabled ? 1f : (pressed ? pressScale : (active ? activeScale : 1f));

            if (active && !_wasActive)
            {
                if (playHoverSound) AudioManager.Hover();
                PlayShine();
            }
            _wasActive = active;

            float target = active ? 1f : 0f;
            if (instant || !isActiveAndEnabled)
            {
                _k = target;
                Apply(_k);
                transform.localScale = Vector3.one * _targetScale;
            }
            else
            {
                Tween.Restart(this, ref _visualCo, VisualRoutine(target));
            }
        }

        IEnumerator VisualRoutine(float target)
        {
            float from = _k;
            float fromScale = transform.localScale.x;
            float toScale = _targetScale;

            yield return Tween.Value(transitionTime, t =>
            {
                _k = Mathf.Lerp(from, target, Ease.OutCubic(t));
                Apply(_k);
                float s = Mathf.LerpUnclamped(fromScale, toScale, Ease.OutBackSoft(t));
                transform.localScale = new Vector3(s, s, 1f);
            }, Ease.Linear);

            transform.localScale = Vector3.one * toScale;
        }

        void Apply(float k)
        {
            if (frame  != null) frame.color  = Color.Lerp(frameIdle,  frameActive,  k);
            if (fill   != null) fill.color   = Color.Lerp(fillIdle,   fillActive,   k);
            if (accent != null)
            {
                accent.color = Color.Lerp(accentIdle, accentActive, k);
                var ar = accent.rectTransform;
                ar.sizeDelta = new Vector2(Mathf.Lerp(accentWidthIdle, accentWidthActive, k), ar.sizeDelta.y);
            }
            if (label != null)
            {
                label.color = Color.Lerp(labelIdle, labelActive, k);
                label.characterSpacing = Mathf.Lerp(labelSpacingIdle, labelSpacingActive, k);
            }
            if (icon != null)
            {
                icon.color = Color.Lerp(labelIdle, accentActive, k);
                icon.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, k);
            }
            if (arrow != null)
            {
                Color c = accentActive;
                c.a = k;
                arrow.color = c;
                arrow.rectTransform.anchoredPosition = _arrowBasePos + new Vector2(Mathf.Lerp(-arrowSlide, 0f, k), 0f);
            }
        }

        // ---------------------------------------------------------- destello

        void PlayShine()
        {
            if (_shineRect == null) return;
            Tween.Restart(this, ref _shineCo, ShineRoutine());
        }

        IEnumerator ShineRoutine()
        {
            float half = _shineTravel * 0.5f;
            yield return Tween.Value(0.6f, t =>
            {
                if (_shineRect == null) return;
                float e = Ease.OutQuad(t);
                _shineRect.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, e), 0f);
                SetShineAlpha(Ease.Spike(t) * 0.85f);
            }, Ease.Linear);
            SetShineAlpha(0f);
        }

        void SetShineAlpha(float a)
        {
            if (shine == null) return;
            Color c = shine.color;
            c.a = a;
            shine.color = c;
        }

        // ------------------------------------------------------------ clics

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public void OnSubmit(BaseEventData eventData) => Press();

        /// <summary>Activa el boton por codigo (misma ruta que el clic).</summary>
        public void Press()
        {
            if (!IsInteractable())
            {
                AudioManager.Deny();
                return;
            }
            if (playClickSound) AudioManager.Click();
            StartCoroutine(PressFeedback());
            onClick?.Invoke();
        }

        IEnumerator PressFeedback()
        {
            yield return Tween.Value(0.08f, t =>
            {
                float s = Mathf.Lerp(pressScale, _targetScale, Ease.OutQuad(t));
                transform.localScale = new Vector3(s, s, 1f);
            }, Ease.Linear);
        }

        public void SetInteractable(bool value) => interactable = value;
    }
}
