using System.Collections;
using Ekkar.Core;
using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Relampago que recorta el castillo contra el cielo. Doble destello
    /// (el segundo mas debil) para que parezca electrico y no un fundido.
    /// </summary>
    public class LightningFlash : MonoBehaviour
    {
        [SerializeField] SpriteRenderer overlay;
        [SerializeField] Vector2 intervalRange = new Vector2(7f, 18f);
        [SerializeField] Color flashColor = new Color(0.65f, 0.80f, 1f, 1f);
        [SerializeField] float peakAlpha = 0.5f;

        float _next;

        void Start()
        {
            _next = Random.Range(intervalRange.x, intervalRange.y);
            SetAlpha(0f);
        }

        void Update()
        {
            _next -= Time.deltaTime;
            if (_next <= 0f)
            {
                _next = Random.Range(intervalRange.x, intervalRange.y);
                StartCoroutine(Strike());
            }
        }

        IEnumerator Strike()
        {
            yield return Tween.Value(0.13f, t => SetAlpha(Ease.Spike(t) * peakAlpha), Ease.Linear);
            yield return Tween.Wait(Random.Range(0.06f, 0.14f));
            yield return Tween.Value(0.26f, t => SetAlpha(Ease.Spike(t) * peakAlpha * 0.55f), Ease.Linear);
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            if (overlay == null) return;
            Color c = flashColor;
            c.a = a;
            overlay.color = c;
        }
    }
}
