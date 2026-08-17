using Ekkar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.FX
{
    /// <summary>
    /// Latido de opacidad, escala y/o color sobre un Graphic. Es el equivalente
    /// a los @keyframes de "glow" del maquetado HTML.
    /// </summary>
    public class UIPulse : MonoBehaviour
    {
        [SerializeField] Graphic target;

        [Header("Opacidad")]
        [SerializeField] bool affectAlpha = true;
        [SerializeField, Range(0f, 1f)] float minAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] float maxAlpha = 1f;

        [Header("Escala")]
        [SerializeField] float scaleAmount = 0f;

        [Header("Color")]
        [SerializeField] bool affectColor = false;
        [SerializeField] Color colorA = Color.white;
        [SerializeField] Color colorB = Color.white;

        [Header("Ritmo")]
        [SerializeField] float period = 3f;
        [SerializeField] float startDelay = 0f;
        [SerializeField] bool randomizePhase = false;

        Vector3 _baseScale;
        float _phase;
        float _elapsed;
        bool _captured;

        void Awake()
        {
            if (target == null) target = GetComponent<Graphic>();
            if (randomizePhase) _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void OnEnable()
        {
            _captured = false;
            _elapsed = 0f;
        }

        void Update()
        {
            if (!_captured)
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed < startDelay) return;
                _baseScale = transform.localScale;
                _captured = true;
            }

            if (period < 0.01f) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period + _phase);

            if (target != null)
            {
                Color c = affectColor ? Color.Lerp(colorA, colorB, k) : target.color;
                if (affectAlpha) c.a = Mathf.Lerp(minAlpha, maxAlpha, k);
                target.color = c;
            }

            if (Mathf.Abs(scaleAmount) > 0.0001f)
                transform.localScale = _baseScale * (1f + (k - 0.5f) * 2f * scaleAmount);
        }
    }
}
