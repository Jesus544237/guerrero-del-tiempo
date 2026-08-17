using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// La luna de sangre a mitad de eclipse. Es lo unico del cielo que se
    /// mueve, y lo hace tan despacio que casi no se nota: el eclipse lleva
    /// seiscientos anos sin terminar.
    /// </summary>
    public class MoonEclipse : MonoBehaviour
    {
        [SerializeField] Transform shadow;
        [SerializeField] float travel = 0.55f;
        [SerializeField] float period = 240f;
        [SerializeField] SpriteRenderer halo;
        [SerializeField] float haloPeriod = 11f;
        [SerializeField] float haloMin = 0.16f;
        [SerializeField] float haloMax = 0.34f;

        Vector3 _shadowBase;

        void Start()
        {
            if (shadow != null) _shadowBase = shadow.localPosition;
        }

        void Update()
        {
            float t = Time.time;

            if (shadow != null && period > 0.01f)
            {
                float k = Mathf.Sin(t * Mathf.PI * 2f / period);
                shadow.localPosition = _shadowBase + new Vector3(k * travel, 0f, 0f);
            }

            if (halo != null && haloPeriod > 0.01f)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / haloPeriod);
                Color c = halo.color;
                c.a = Mathf.Lerp(haloMin, haloMax, k);
                halo.color = c;
            }
        }
    }
}
