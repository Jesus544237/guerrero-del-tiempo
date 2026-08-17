using System.Collections;
using System.Collections.Generic;
using Ekkar.Core;
using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Dirige el escenario del combate final contra el Senor del Tiempo.
    ///
    /// Fase 1: la pelea ocurre dentro de la arena, en el vacio entre segundos.
    /// Fase 2 en adelante: el tiempo se rompe y la arena empieza a saltar
    /// entre las tres eras que Ekkar ya recorrio, al azar y con un chispazo de
    /// por medio, como si el mundo no supiera en que epoca esta.
    ///
    /// Los telones son grupos de SpriteRenderer que se funden entre si; el
    /// suelo y la arena en si no se tocan, para que el combate siga siendo
    /// jugable en todo momento.
    /// </summary>
    public class BossPhaseDirector : MonoBehaviour
    {
        [Header("Telones")]
        [Tooltip("El vacio: se ve en la fase 1 y entre saltos.")]
        [SerializeField] GameObject arenaBackdrop;
        [Tooltip("Ecos de las tres eras, en orden: medieval, industrial, futuro.")]
        [SerializeField] GameObject[] eraBackdrops;

        [Header("Ritmo de los saltos")]
        [SerializeField] Vector2 swapInterval = new Vector2(6f, 11f);
        [SerializeField] float blendTime = 0.55f;
        [SerializeField] float glitchTime = 0.18f;

        [Header("Chispazo")]
        [SerializeField] SpriteRenderer flashOverlay;
        [SerializeField] Color flashColor = new Color(0.13f, 0.83f, 0.93f, 1f);
        [SerializeField, Range(0f, 1f)] float flashStrength = 0.35f;

        [Header("Estado")]
        [SerializeField] int phase = 1;
        [SerializeField] bool autoStart = true;

        readonly List<SpriteRenderer[]> _eraRenderers = new List<SpriteRenderer[]>();
        SpriteRenderer[] _arenaRenderers;
        Coroutine _loop;
        int _current = -1;      // -1 = arena

        public int Phase => phase;

        void Start()
        {
            _arenaRenderers = Collect(arenaBackdrop);
            _eraRenderers.Clear();
            if (eraBackdrops != null)
                foreach (var go in eraBackdrops) _eraRenderers.Add(Collect(go));

            ShowArenaOnly();
            if (autoStart) SetPhase(phase);
        }

        static SpriteRenderer[] Collect(GameObject go)
        {
            return go != null ? go.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
        }

        void ShowArenaOnly()
        {
            SetAlpha(_arenaRenderers, 1f);
            foreach (var era in _eraRenderers) SetAlpha(era, 0f);
            _current = -1;
        }

        static void SetAlpha(SpriteRenderer[] list, float a)
        {
            if (list == null) return;
            foreach (var sr in list)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = a;
                sr.color = c;
                bool on = a > 0.002f;
                if (sr.enabled != on) sr.enabled = on;
            }
        }

        /// <summary>1 = solo arena. 2 o mas = la arena salta entre eras.</summary>
        public void SetPhase(int value)
        {
            phase = Mathf.Max(1, value);

            if (_loop != null) { StopCoroutine(_loop); _loop = null; }

            if (phase <= 1)
            {
                ShowArenaOnly();
                return;
            }
            _loop = StartCoroutine(SwapLoop());
        }

        public void AdvancePhase() => SetPhase(phase + 1);

        IEnumerator SwapLoop()
        {
            // el primer salto llega enseguida: es el momento en que el mundo cede
            yield return Tween.Wait(1.2f);

            while (true)
            {
                int next = PickNext();
                yield return Glitch();
                yield return CrossFade(next);
                yield return Tween.Wait(Random.Range(swapInterval.x, swapInterval.y));
            }
        }

        int PickNext()
        {
            if (_eraRenderers.Count == 0) return -1;

            // nunca repite la misma era dos veces seguidas
            int next;
            int guard = 0;
            do
            {
                next = Random.Range(0, _eraRenderers.Count);
                guard++;
            } while (next == _current && _eraRenderers.Count > 1 && guard < 12);
            return next;
        }

        IEnumerator Glitch()
        {
            if (flashOverlay == null) { yield return Tween.Wait(glitchTime); yield break; }

            yield return Tween.Value(glitchTime, t =>
            {
                Color c = flashColor;
                c.a = Ease.Spike(t) * flashStrength;
                flashOverlay.color = c;
            }, Ease.Linear);

            Color off = flashColor;
            off.a = 0f;
            flashOverlay.color = off;
        }

        IEnumerator CrossFade(int target)
        {
            SpriteRenderer[] from = _current < 0 ? _arenaRenderers : _eraRenderers[_current];
            SpriteRenderer[] to = target < 0 ? _arenaRenderers : _eraRenderers[target];

            foreach (var sr in to) if (sr != null) sr.enabled = true;

            yield return Tween.Value(blendTime, t =>
            {
                SetAlphaRaw(from, 1f - t);
                SetAlphaRaw(to, t);
            }, Ease.InOutQuad);

            SetAlpha(from, 0f);
            SetAlpha(to, 1f);
            _current = target;
        }

        static void SetAlphaRaw(SpriteRenderer[] list, float a)
        {
            if (list == null) return;
            foreach (var sr in list)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = a;
                sr.color = c;
            }
        }
    }
}
