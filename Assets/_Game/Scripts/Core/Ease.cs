using UnityEngine;

namespace Ekkar.Core
{
    /// <summary>
    /// Curvas de interpolacion equivalentes a las de GSAP. Todas reciben y
    /// devuelven un valor normalizado 0..1 (algunas se salen del rango a
    /// proposito para producir rebote / sobreimpulso).
    /// </summary>
    public static class Ease
    {
        public static float Linear(float t) => t;

        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

        public static float InCubic(float t) => t * t * t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        public static float OutQuart(float t) => 1f - Mathf.Pow(1f - t, 4f);
        public static float InOutQuart(float t) => t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;

        public static float OutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);

        public static float InSine(float t) => 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
        public static float OutSine(float t) => Mathf.Sin(t * Mathf.PI * 0.5f);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

        public static float OutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        public static float InExpo(float t) => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float OutBackSoft(float t)
        {
            const float c1 = 0.9f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float InBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>Sube y vuelve: util para "punch" / flashes.</summary>
        public static float Spike(float t) => t < 0.5f ? OutQuad(t * 2f) : OutQuad((1f - t) * 2f);
    }
}
