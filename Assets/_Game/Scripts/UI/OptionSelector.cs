using System.Collections;
using Ekkar.Audio;
using Ekkar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Selector horizontal "◀ valor ▶". Sustituye al desplegable del maquetado
    /// porque encaja mejor con la estetica pixel y se maneja con teclado y
    /// mando sin abrir ninguna lista.
    /// </summary>
    [AddComponentMenu("Ekkar/Option Selector")]
    public class OptionSelector : Selectable
    {
        [Header("Partes")]
        [SerializeField] TMP_Text valueLabel;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] Image frame;

        [Header("Colores")]
        [SerializeField] Color frameIdle = new Color(0.49f, 0.23f, 0.93f, 0.5f);
        [SerializeField] Color frameActive = new Color(0.13f, 0.83f, 0.93f, 1f);
        [SerializeField] Color labelIdle = new Color(0.89f, 0.91f, 0.94f, 1f);
        [SerializeField] Color labelActive = Color.white;

        [Header("Comportamiento")]
        [SerializeField] bool wrapAround = true;
        [SerializeField] string[] options = new string[0];
        [SerializeField] int index;

        public event System.Action<int> IndexChanged;

        public int Index => index;
        public int Count => options != null ? options.Length : 0;
        public string Current => (options != null && index >= 0 && index < options.Length) ? options[index] : string.Empty;

        Coroutine _labelCo;
        bool _wasActive;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            if (prevButton != null) prevButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
        }

        protected override void Start()
        {
            base.Start();
            Refresh(false);
            ApplyHighlight(0f);
        }

        public void SetOptions(string[] newOptions, int newIndex, bool notify = false)
        {
            options = newOptions ?? new string[0];
            index = Mathf.Clamp(newIndex, 0, Mathf.Max(0, options.Length - 1));
            Refresh(false);
            if (notify) IndexChanged?.Invoke(index);
        }

        public void SetIndex(int newIndex, bool notify = true, bool animate = true)
        {
            if (options == null || options.Length == 0) return;
            int clamped = wrapAround
                ? ((newIndex % options.Length) + options.Length) % options.Length
                : Mathf.Clamp(newIndex, 0, options.Length - 1);

            bool changed = clamped != index;
            index = clamped;
            Refresh(animate && changed);
            if (changed && notify) IndexChanged?.Invoke(index);
        }

        public void Next()
        {
            if (!IsInteractable()) { AudioManager.Deny(); return; }
            if (!wrapAround && index >= Count - 1) { AudioManager.Deny(); return; }
            AudioManager.Toggle();
            SetIndex(index + 1);
        }

        public void Previous()
        {
            if (!IsInteractable()) { AudioManager.Deny(); return; }
            if (!wrapAround && index <= 0) { AudioManager.Deny(); return; }
            AudioManager.Toggle();
            SetIndex(index - 1);
        }

        void Refresh(bool animate)
        {
            if (valueLabel != null) valueLabel.text = Current;
            if (animate && Application.isPlaying && isActiveAndEnabled && valueLabel != null)
                Tween.Restart(this, ref _labelCo, LabelPop());
        }

        IEnumerator LabelPop()
        {
            var t = valueLabel.transform;
            yield return Tween.Value(0.22f, k =>
            {
                if (t == null) return;
                float s = 1f + Ease.Spike(k) * 0.12f;
                t.localScale = new Vector3(s, s, 1f);
            }, Ease.Linear);
            if (t != null) t.localScale = Vector3.one;
        }

        // -------------------------------------------------- foco y teclado

        public override void OnMove(AxisEventData eventData)
        {
            if (IsInteractable() && eventData != null)
            {
                if (eventData.moveDir == MoveDirection.Left)  { Previous(); return; }
                if (eventData.moveDir == MoveDirection.Right) { Next();     return; }
            }
            base.OnMove(eventData);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!Application.isPlaying) return;

            bool active = state == SelectionState.Highlighted
                       || state == SelectionState.Selected
                       || state == SelectionState.Pressed;

            if (active && !_wasActive) AudioManager.Hover();
            _wasActive = active;

            ApplyHighlight(active ? 1f : 0f);
        }

        void ApplyHighlight(float k)
        {
            if (frame != null) frame.color = Color.Lerp(frameIdle, frameActive, k);
            if (valueLabel != null) valueLabel.color = Color.Lerp(labelIdle, labelActive, k);
        }
    }
}
