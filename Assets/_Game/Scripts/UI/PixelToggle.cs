using System.Collections;
using Ekkar.Audio;
using Ekkar.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Interruptor con estetica pixel: la pastilla se desliza y la pista cambia
    /// de color. Equivale al ".toggle" del maquetado HTML.
    /// </summary>
    [AddComponentMenu("Ekkar/Pixel Toggle")]
    public class PixelToggle : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Header("Partes")]
        [SerializeField] Image track;
        [SerializeField] Image knobImage;
        [SerializeField] RectTransform knob;
        [SerializeField] Image focusFrame;

        [Header("Colores")]
        [SerializeField] Color trackOff = new Color(0.10f, 0.04f, 0.18f, 0.9f);
        [SerializeField] Color trackOn = new Color(0.06f, 0.60f, 0.72f, 0.95f);
        [SerializeField] Color knobOff = new Color(0.58f, 0.64f, 0.72f, 1f);
        [SerializeField] Color knobOn = new Color(0.98f, 0.75f, 0.14f, 1f);
        [SerializeField] Color focusIdle = new Color(0.49f, 0.23f, 0.93f, 0f);
        [SerializeField] Color focusActive = new Color(0.13f, 0.83f, 0.93f, 1f);

        [Header("Geometria")]
        [SerializeField] float knobOffX = -17f;
        [SerializeField] float knobOnX = 17f;
        [SerializeField] float animTime = 0.18f;

        [SerializeField] bool isOn = true;

        public event System.Action<bool> ValueChanged;

        public bool IsOn => isOn;

        Coroutine _co;
        bool _wasActive;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
        }

        protected override void Start()
        {
            base.Start();
            ApplyVisual(isOn ? 1f : 0f);
            ApplyFocus(0f);
        }

        public void SetValue(bool value, bool notify = true, bool animate = true)
        {
            bool changed = value != isOn;
            isOn = value;

            if (animate && Application.isPlaying && isActiveAndEnabled)
                Tween.Restart(this, ref _co, AnimateTo(isOn ? 1f : 0f));
            else
                ApplyVisual(isOn ? 1f : 0f);

            if (changed && notify) ValueChanged?.Invoke(isOn);
        }

        public void Toggle()
        {
            if (!IsInteractable()) { AudioManager.Deny(); return; }
            AudioManager.Toggle();
            SetValue(!isOn);
        }

        IEnumerator AnimateTo(float target)
        {
            float from = knob != null
                ? Mathf.InverseLerp(knobOffX, knobOnX, knob.anchoredPosition.x)
                : 1f - target;

            yield return Tween.Value(animTime, t => ApplyVisual(Mathf.LerpUnclamped(from, target, Ease.OutBack(t))), Ease.Linear);
            ApplyVisual(target);
        }

        void ApplyVisual(float k)
        {
            if (track != null) track.color = Color.Lerp(trackOff, trackOn, Mathf.Clamp01(k));
            if (knobImage != null) knobImage.color = Color.Lerp(knobOff, knobOn, Mathf.Clamp01(k));
            if (knob != null)
                knob.anchoredPosition = new Vector2(Mathf.LerpUnclamped(knobOffX, knobOnX, k), knob.anchoredPosition.y);
        }

        void ApplyFocus(float k)
        {
            if (focusFrame != null) focusFrame.color = Color.Lerp(focusIdle, focusActive, k);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            Toggle();
        }

        public void OnSubmit(BaseEventData eventData) => Toggle();

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!Application.isPlaying) return;

            bool active = state == SelectionState.Highlighted
                       || state == SelectionState.Selected
                       || state == SelectionState.Pressed;

            if (active && !_wasActive) AudioManager.Hover();
            _wasActive = active;

            ApplyFocus(active ? 1f : 0f);
        }
    }
}
