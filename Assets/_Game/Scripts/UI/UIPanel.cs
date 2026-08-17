using System.Collections;
using Ekkar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Ventana modal animada (opciones, creditos, confirmacion de salida).
    /// Anima el velo de fondo y la caja con entrada tipo "back out", devuelve
    /// el foco al elemento que estaba seleccionado antes de abrirse y avisa por
    /// eventos cuando termina de abrir o cerrar.
    /// </summary>
    [AddComponentMenu("Ekkar/UI Panel")]
    public class UIPanel : MonoBehaviour
    {
        [Header("Partes")]
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform box;
        [SerializeField] Graphic scrim;
        [SerializeField] Selectable firstSelected;

        [Header("Animacion")]
        [SerializeField] float openTime = 0.32f;
        [SerializeField] float closeTime = 0.18f;
        [SerializeField] float slideFrom = 48f;
        [SerializeField] float scaleFrom = 0.92f;
        [SerializeField, Range(0f, 1f)] float scrimAlpha = 0.88f;

        [Header("Eventos")]
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        public bool IsOpen { get; private set; }
        public bool IsAnimating { get; private set; }

        Vector2 _boxBasePos;
        Coroutine _co;
        GameObject _previousSelection;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (box != null) _boxBasePos = box.anchoredPosition;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            SetScrimAlpha(0f);
        }

        // Se desactiva en Start (no en Awake) para que los componentes hijos
        // lleguen a inicializarse; con alpha 0 no llega a verse ningun destello.
        void Start()
        {
            if (!IsOpen) gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            var es = EventSystem.current;
            _previousSelection = es != null ? es.currentSelectedGameObject : null;

            gameObject.SetActive(true);
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
            if (box != null)
            {
                box.anchoredPosition = _boxBasePos + new Vector2(0f, -slideFrom);
                box.localScale = Vector3.one * scaleFrom;
            }
            SetScrimAlpha(0f);

            Tween.Restart(this, ref _co, OpenRoutine());
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            Tween.Restart(this, ref _co, CloseRoutine());
        }

        IEnumerator OpenRoutine()
        {
            IsAnimating = true;

            yield return Tween.Value(openTime, t =>
            {
                float fade = Ease.OutQuad(Mathf.Clamp01(t * 1.6f));
                if (group != null) group.alpha = fade;
                SetScrimAlpha(fade * scrimAlpha);

                if (box != null)
                {
                    float e = Ease.OutBack(t);
                    box.anchoredPosition = Vector2.LerpUnclamped(_boxBasePos + new Vector2(0f, -slideFrom), _boxBasePos, e);
                    box.localScale = Vector3.LerpUnclamped(Vector3.one * scaleFrom, Vector3.one, e);
                }
            }, Ease.Linear);

            if (group != null) group.alpha = 1f;
            if (box != null)
            {
                box.anchoredPosition = _boxBasePos;
                box.localScale = Vector3.one;
            }

            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);

            IsAnimating = false;
            onOpened?.Invoke();
        }

        IEnumerator CloseRoutine()
        {
            IsAnimating = true;

            float fromAlpha = group != null ? group.alpha : 1f;
            Vector2 fromPos = box != null ? box.anchoredPosition : Vector2.zero;
            Vector3 fromScale = box != null ? box.localScale : Vector3.one;

            yield return Tween.Value(closeTime, t =>
            {
                float e = Ease.InQuad(t);
                if (group != null) group.alpha = Mathf.Lerp(fromAlpha, 0f, e);
                SetScrimAlpha(Mathf.Lerp(fromAlpha * scrimAlpha, 0f, e));

                if (box != null)
                {
                    box.anchoredPosition = Vector2.Lerp(fromPos, _boxBasePos + new Vector2(0f, -slideFrom * 0.6f), e);
                    box.localScale = Vector3.Lerp(fromScale, Vector3.one * scaleFrom, e);
                }
            }, Ease.Linear);

            gameObject.SetActive(false);
            IsAnimating = false;

            if (EventSystem.current != null && _previousSelection != null && _previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_previousSelection);

            onClosed?.Invoke();
        }

        void SetScrimAlpha(float a)
        {
            if (scrim == null) return;
            Color c = scrim.color;
            c.a = a;
            scrim.color = c;
        }
    }
}
