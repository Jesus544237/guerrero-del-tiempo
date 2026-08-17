using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.Core
{
    /// <summary>
    /// Motor de animacion minimalista basado en corrutinas, con la misma idea
    /// que GSAP: se describe "de donde a donde, cuanto dura y con que curva".
    /// Todo corre en tiempo no escalado para que el menu siga animando aunque
    /// el juego este en pausa (Time.timeScale = 0).
    /// </summary>
    public static class Tween
    {
        // ------------------------------------------------------------ base

        public static IEnumerator Value(float duration, Action<float> apply,
                                        Func<float, float> ease = null, float delay = 0f,
                                        Action onComplete = null)
        {
            if (ease == null) ease = Ease.Linear;

            if (delay > 0f) yield return Wait(delay);

            if (duration <= 0f)
            {
                apply(ease(1f));
                onComplete?.Invoke();
                yield break;
            }

            apply(ease(0f));
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                apply(ease(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            apply(ease(1f));
            onComplete?.Invoke();
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>Reinicia una corrutina guardada en <paramref name="slot"/>.</summary>
        public static void Restart(MonoBehaviour host, ref Coroutine slot, IEnumerator routine)
        {
            if (host == null) return;
            if (slot != null) host.StopCoroutine(slot);
            slot = host.isActiveAndEnabled ? host.StartCoroutine(routine) : null;
        }

        public static void Stop(MonoBehaviour host, ref Coroutine slot)
        {
            if (host != null && slot != null) host.StopCoroutine(slot);
            slot = null;
        }

        // ------------------------------------------------------- atajos UI

        public static IEnumerator Fade(CanvasGroup group, float to, float duration,
                                       Func<float, float> ease = null, float delay = 0f)
        {
            if (group == null) yield break;
            float from = group.alpha;
            yield return Value(duration, t => { if (group != null) group.alpha = Mathf.LerpUnclamped(from, to, t); }, ease, delay);
        }

        public static IEnumerator FadeGraphic(Graphic graphic, float to, float duration,
                                              Func<float, float> ease = null, float delay = 0f)
        {
            if (graphic == null) yield break;
            float from = graphic.color.a;
            yield return Value(duration, t =>
            {
                if (graphic == null) return;
                Color c = graphic.color;
                c.a = Mathf.LerpUnclamped(from, to, t);
                graphic.color = c;
            }, ease, delay);
        }

        public static IEnumerator ColorTo(Graphic graphic, Color to, float duration,
                                          Func<float, float> ease = null, float delay = 0f)
        {
            if (graphic == null) yield break;
            Color from = graphic.color;
            yield return Value(duration, t => { if (graphic != null) graphic.color = Color.LerpUnclamped(from, to, t); }, ease, delay);
        }

        public static IEnumerator MoveAnchored(RectTransform rect, Vector2 to, float duration,
                                               Func<float, float> ease = null, float delay = 0f)
        {
            if (rect == null) yield break;
            Vector2 from = rect.anchoredPosition;
            yield return Value(duration, t => { if (rect != null) rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t); }, ease, delay);
        }

        public static IEnumerator ScaleTo(Transform target, Vector3 to, float duration,
                                          Func<float, float> ease = null, float delay = 0f)
        {
            if (target == null) yield break;
            Vector3 from = target.localScale;
            yield return Value(duration, t => { if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, t); }, ease, delay);
        }

        /// <summary>Golpe de escala tipo GSAP ".to(scale).yoyo()".</summary>
        public static IEnumerator Punch(Transform target, float amount = 0.12f, float duration = 0.25f)
        {
            if (target == null) yield break;
            Vector3 baseScale = target.localScale;
            yield return Value(duration, t =>
            {
                if (target == null) return;
                target.localScale = baseScale * (1f + Ease.Spike(t) * amount);
            }, Ease.Linear);
            if (target != null) target.localScale = baseScale;
        }
    }
}
