using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Cambia el telon de fondo segun por donde va el jugador. En "El Sitio
    /// Eterno" la ciudad lejana acompana casi todo el nivel, y en el tramo
    /// final se disuelve para dejar solo el castillo ardiendo contra el cielo.
    /// </summary>
    public class SectionSwap : MonoBehaviour
    {
        [SerializeField] Transform target;          // normalmente la camara
        [SerializeField] GameObject before;         // se ve al principio
        [SerializeField] GameObject after;          // se ve en el tramo final
        [SerializeField] float switchX = 50f;
        [SerializeField] float blendWidth = 14f;

        SpriteRenderer[] _before, _after;

        void Start()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            if (before != null) _before = before.GetComponentsInChildren<SpriteRenderer>(true);
            if (after != null) _after = after.GetComponentsInChildren<SpriteRenderer>(true);
            Apply(0f);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float half = Mathf.Max(0.01f, blendWidth) * 0.5f;
            Apply(Mathf.InverseLerp(switchX - half, switchX + half, target.position.x));
        }

        void Apply(float k)
        {
            SetAlpha(_before, 1f - k);
            SetAlpha(_after, k);
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
                // apagar lo invisible ahorra dibujar de mas
                if (sr.enabled != a > 0.002f) sr.enabled = a > 0.002f;
            }
        }
    }
}
