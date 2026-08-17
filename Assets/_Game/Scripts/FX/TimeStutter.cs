using System.Collections;
using System.Collections.Generic;
using Ekkar.Core;
using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// El tartamudeo temporal: cada cierto tiempo el mundo retrocede una
    /// fraccion de segundo y vuelve a reproducir ese instante. Es la firma
    /// visual de "Guerrero del Tiempo" — el mundo esta roto y se repite.
    ///
    /// Graba la posicion de los objetos marcados y, al saltar, los devuelve
    /// a donde estaban, con una sacudida de color encima.
    /// </summary>
    public class TimeStutter : MonoBehaviour
    {
        [Header("Ritmo")]
        [SerializeField] Vector2 intervalRange = new Vector2(9f, 15f);
        [SerializeField] float rewindSeconds = 0.4f;
        [SerializeField] float historyStep = 0.05f;

        [Header("Objetos afectados")]
        [Tooltip("Se rebobina la posicion de estos transforms. Vacio = solo efecto visual.")]
        [SerializeField] Transform[] targets;

        [Header("Aviso visual")]
        [SerializeField] SpriteRenderer flashOverlay;
        [SerializeField] Color flashColor = new Color(0.13f, 0.83f, 0.93f, 1f);
        [SerializeField] float flashStrength = 0.22f;
        [SerializeField] float glitchOffset = 0.12f;

        readonly List<Vector3[]> _history = new List<Vector3[]>();
        float _sampleTimer;
        float _nextStutter;
        int _maxSamples;

        public System.Action Stuttered;

        void Start()
        {
            _maxSamples = Mathf.Max(2, Mathf.CeilToInt(rewindSeconds / Mathf.Max(0.01f, historyStep)) + 1);
            _nextStutter = Random.Range(intervalRange.x, intervalRange.y);
            SetFlash(0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            _sampleTimer += dt;
            if (_sampleTimer >= historyStep)
            {
                _sampleTimer = 0f;
                Sample();
            }

            _nextStutter -= dt;
            if (_nextStutter <= 0f)
            {
                _nextStutter = Random.Range(intervalRange.x, intervalRange.y);
                StartCoroutine(DoStutter());
            }
        }

        void Sample()
        {
            if (targets == null || targets.Length == 0) return;

            var snapshot = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                snapshot[i] = targets[i] != null ? targets[i].position : Vector3.zero;

            _history.Add(snapshot);
            while (_history.Count > _maxSamples) _history.RemoveAt(0);
        }

        IEnumerator DoStutter()
        {
            // salto atras: se restaura la instantanea mas antigua guardada
            if (_history.Count > 0 && targets != null)
            {
                var snapshot = _history[0];
                for (int i = 0; i < targets.Length && i < snapshot.Length; i++)
                    if (targets[i] != null) targets[i].position = snapshot[i];
                _history.Clear();
            }

            Stuttered?.Invoke();

            // sacudida en dos tirones, como un fotograma que salta
            Vector3 basePos = transform.position;
            yield return Tween.Value(0.09f, t =>
            {
                SetFlash(Ease.Spike(t) * flashStrength);
                transform.position = basePos + new Vector3(glitchOffset * (t < 0.5f ? 1f : -1f), 0f, 0f);
            }, Ease.Linear);

            yield return Tween.Value(0.12f, t =>
            {
                SetFlash(Ease.Spike(t) * flashStrength * 0.55f);
                transform.position = basePos + new Vector3(glitchOffset * 0.4f * Mathf.Sin(t * 20f), 0f, 0f);
            }, Ease.Linear);

            transform.position = basePos;
            SetFlash(0f);
        }

        void SetFlash(float a)
        {
            if (flashOverlay == null) return;
            Color c = flashColor;
            c.a = a;
            flashOverlay.color = c;
        }
    }
}
