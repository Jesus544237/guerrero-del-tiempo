using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Lluvia de pavesas y ceniza que cae por todo el camino: brasas naranjas
    /// del castillo en llamas mezcladas con ceniza fria y chispas cian de
    /// tiempo roto. Va colgada de la camara, asi que acompana al jugador todo
    /// el nivel sin necesidad de sembrar particulas por el mapa.
    /// </summary>
    public class EmberFall : MonoBehaviour
    {
        [SerializeField] Sprite moteSprite;
        [SerializeField] int count = 120;
        [SerializeField] Vector2 area = new Vector2(24f, 14f);
        [SerializeField] Vector2 sizeRange = new Vector2(0.03f, 0.11f);
        [SerializeField] Vector2 fallSpeed = new Vector2(0.6f, 2.4f);
        [SerializeField] Vector2 drift = new Vector2(-0.9f, 0.35f);
        [SerializeField] float swayAmount = 0.35f;
        [SerializeField] int sortingOrder = 45;

        [Header("Aparicion")]
        [Tooltip("Segundos que tarda el campo en revelarse por completo. 0 = todo visible de golpe.")]
        [SerializeField] float revealSeconds = 0f;

        [Header("Mezcla de brasas")]
        [SerializeField, Range(0f, 1f)] float emberRatio = 0.45f;
        [SerializeField] Color emberHot = new Color(1f, 0.62f, 0.11f, 1f);
        [SerializeField] Color emberCool = new Color(0.86f, 0.20f, 0.14f, 1f);
        [SerializeField] Color ash = new Color(0.58f, 0.60f, 0.72f, 1f);
        [SerializeField] Color chrono = new Color(0.13f, 0.83f, 0.93f, 1f);

        struct Mote
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector2 vel;
            public float alpha, flickerSpeed, phase, sway;
            public bool isEmber;
            public float revealOrder;      // 0..1: en que momento del revelado entra
        }

        Mote[] _motes;
        float _age;

        void Start()
        {
            _motes = new Mote[Mathf.Max(0, count)];
            for (int i = 0; i < _motes.Length; i++)
            {
                var go = new GameObject("Ember");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = moteSprite;
                sr.sortingOrder = sortingOrder;

                _motes[i] = new Mote { tr = go.transform, sr = sr, revealOrder = i / (float)Mathf.Max(1, count) };
                Respawn(ref _motes[i], true);
            }
        }

        void Respawn(ref Mote m, bool anywhere)
        {
            float x = Random.Range(-area.x * 0.5f, area.x * 0.5f);
            float y = anywhere
                ? Random.Range(-area.y * 0.5f, area.y * 0.5f)
                : area.y * 0.5f + Random.Range(0f, 2f);
            m.tr.localPosition = new Vector3(x, y, 0f);

            m.isEmber = Random.value < emberRatio;

            float s = Random.Range(sizeRange.x, sizeRange.y) * (m.isEmber ? 0.8f : 1f);
            m.tr.localScale = new Vector3(s, s, 1f);

            m.vel = new Vector2(Random.Range(drift.x, drift.y),
                                -Random.Range(fallSpeed.x, fallSpeed.y) * (m.isEmber ? 0.7f : 1f));

            m.alpha = Random.Range(0.35f, 0.95f);
            m.flickerSpeed = Random.Range(3f, 11f);
            m.phase = Random.Range(0f, Mathf.PI * 2f);
            m.sway = Random.Range(0.4f, 1.6f);

            Color c;
            if (m.isEmber) c = Color.Lerp(emberHot, emberCool, Random.value);
            else c = Random.value < 0.25f ? chrono : ash;
            c.a = m.alpha;
            m.sr.color = c;
        }

        void Update()
        {
            if (_motes == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;
            _age += dt;

            // 0..1: cuanto del campo se ha revelado ya
            float revealed = revealSeconds <= 0.01f ? 1f : Mathf.Clamp01(_age / revealSeconds);
            float bottom = -area.y * 0.5f - 1f;
            float halfX = area.x * 0.5f;

            for (int i = 0; i < _motes.Length; i++)
            {
                ref Mote m = ref _motes[i];
                if (m.tr == null) continue;

                Vector3 p = m.tr.localPosition;
                p.y += m.vel.y * dt;
                p.x += (m.vel.x + Mathf.Sin(t * m.sway + m.phase) * swayAmount) * dt;

                if (p.y < bottom) { Respawn(ref m, false); continue; }
                if (p.x < -halfX) p.x += area.x;
                else if (p.x > halfX) p.x -= area.x;

                m.tr.localPosition = p;

                // las brasas parpadean fuerte; la ceniza apenas
                float flicker = m.isEmber
                    ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * m.flickerSpeed + m.phase))
                    : 0.75f + 0.25f * Mathf.Sin(t * m.flickerSpeed * 0.3f + m.phase);

                // cada mota entra en su turno, con un pequeno fundido propio,
                // de modo que el campo aparece poco a poco en vez de de golpe
                float fadeIn = revealSeconds <= 0.01f
                    ? 1f
                    : Mathf.Clamp01((revealed - m.revealOrder) * 6f);

                Color c = m.sr.color;
                c.a = m.alpha * flicker * fadeIn;
                m.sr.color = c;
            }
        }
    }
}
