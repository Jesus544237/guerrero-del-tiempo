using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Ceniza suspendida: las motas caen, se paran a media caida y quedan
    /// colgadas un rato antes de seguir. Es la lectura mas directa de "aqui el
    /// tiempo esta roto" sin necesidad de texto.
    /// </summary>
    public class SuspendedAsh : MonoBehaviour
    {
        [SerializeField] Sprite moteSprite;
        [SerializeField] int count = 90;
        [SerializeField] Vector2 area = new Vector2(26f, 14f);
        [SerializeField] Vector2 sizeRange = new Vector2(0.02f, 0.07f);
        [SerializeField] Vector2 fallSpeedRange = new Vector2(0.25f, 0.9f);
        [SerializeField] Vector2 driftRange = new Vector2(-0.25f, 0.25f);
        [SerializeField] Vector2 frozenTimeRange = new Vector2(0.8f, 3.5f);
        [SerializeField] Vector2 fallTimeRange = new Vector2(0.6f, 2.2f);
        [SerializeField] Color[] tints;
        [SerializeField] int sortingOrder = 40;
        [SerializeField] string sortingLayer = "Default";

        struct Mote
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector2 velocity;
            public float timer;
            public float phaseLength;
            public bool frozen;
            public float alpha;
        }

        Mote[] _motes;

        void Start()
        {
            if (tints == null || tints.Length == 0)
                tints = new[]
                {
                    new Color(0.58f, 0.64f, 0.72f, 1f),
                    new Color(0.13f, 0.83f, 0.93f, 1f),
                    new Color(0.96f, 0.62f, 0.04f, 1f),
                };

            _motes = new Mote[Mathf.Max(0, count)];
            for (int i = 0; i < _motes.Length; i++)
            {
                var go = new GameObject("Ash");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = moteSprite;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;

                _motes[i] = new Mote { tr = go.transform, sr = sr };
                Respawn(ref _motes[i], true);
            }
        }

        void Respawn(ref Mote m, bool anywhere)
        {
            float x = Random.Range(-area.x * 0.5f, area.x * 0.5f);
            float y = anywhere
                ? Random.Range(-area.y * 0.5f, area.y * 0.5f)
                : area.y * 0.5f + Random.Range(0f, 1.5f);

            m.tr.localPosition = new Vector3(x, y, 0f);

            float s = Random.Range(sizeRange.x, sizeRange.y);
            m.tr.localScale = new Vector3(s, s, 1f);

            m.velocity = new Vector2(Random.Range(driftRange.x, driftRange.y),
                                     -Random.Range(fallSpeedRange.x, fallSpeedRange.y));

            m.frozen = Random.value < 0.35f;
            m.phaseLength = m.frozen
                ? Random.Range(frozenTimeRange.x, frozenTimeRange.y)
                : Random.Range(fallTimeRange.x, fallTimeRange.y);
            m.timer = Random.Range(0f, m.phaseLength);

            m.alpha = Random.Range(0.25f, 0.75f);
            Color c = tints[Random.Range(0, tints.Length)];
            c.a = m.alpha;
            m.sr.color = c;
        }

        void Update()
        {
            if (_motes == null) return;
            float dt = Time.deltaTime;
            float bottom = -area.y * 0.5f - 1f;

            for (int i = 0; i < _motes.Length; i++)
            {
                ref Mote m = ref _motes[i];
                if (m.tr == null) continue;

                m.timer += dt;
                if (m.timer >= m.phaseLength)
                {
                    m.timer = 0f;
                    m.frozen = !m.frozen;
                    m.phaseLength = m.frozen
                        ? Random.Range(frozenTimeRange.x, frozenTimeRange.y)
                        : Random.Range(fallTimeRange.x, fallTimeRange.y);
                }

                if (m.frozen)
                {
                    // titileo mientras esta detenida: el instante "vibra"
                    Color c = m.sr.color;
                    c.a = m.alpha * (0.65f + 0.35f * Mathf.Sin(Time.time * 9f + i));
                    m.sr.color = c;
                    continue;
                }

                Vector3 p = m.tr.localPosition + (Vector3)(m.velocity * dt);
                if (p.y < bottom) { Respawn(ref m, false); continue; }
                m.tr.localPosition = p;

                Color cc = m.sr.color;
                cc.a = m.alpha;
                m.sr.color = cc;
            }
        }
    }
}
