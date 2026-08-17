using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Columna de humo que sube hacia el cielo. Se coloca sobre las torres del
    /// castillo y sobre la ciudad lejana: las bocanadas nacen abajo, se
    /// ensanchan al subir, se inclinan con el viento y se deshacen antes de
    /// llegar arriba.
    /// </summary>
    public class RisingSmoke : MonoBehaviour
    {
        [SerializeField] Sprite puffSprite;
        [SerializeField] int puffCount = 26;
        [SerializeField] float spawnWidth = 1.6f;
        [SerializeField] float riseHeight = 7f;
        [SerializeField] Vector2 riseSpeed = new Vector2(0.5f, 1.3f);
        [SerializeField] float windDrift = 0.55f;
        [SerializeField] Vector2 startScale = new Vector2(0.5f, 1.1f);
        [SerializeField] float growth = 2.6f;
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.4f;
        [SerializeField] Color tint = new Color(0.35f, 0.28f, 0.5f, 1f);
        [SerializeField] int sortingOrder = -70;

        struct Puff
        {
            public Transform tr;
            public SpriteRenderer sr;
            public float speed, life, maxLife, baseScale, wobble, phase;
        }

        Puff[] _puffs;

        void Start()
        {
            _puffs = new Puff[Mathf.Max(0, puffCount)];
            for (int i = 0; i < _puffs.Length; i++)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = puffSprite;
                sr.sortingOrder = sortingOrder;
                sr.color = new Color(tint.r, tint.g, tint.b, 0f);

                _puffs[i] = new Puff { tr = go.transform, sr = sr };
                Respawn(ref _puffs[i], true);
            }
        }

        void Respawn(ref Puff p, bool anywhere)
        {
            p.speed = Random.Range(riseSpeed.x, riseSpeed.y);
            p.maxLife = riseHeight / p.speed;
            p.life = anywhere ? Random.Range(0f, p.maxLife) : 0f;
            p.baseScale = Random.Range(startScale.x, startScale.y);
            p.wobble = Random.Range(0.3f, 1.1f);
            p.phase = Random.Range(0f, Mathf.PI * 2f);

            p.tr.localPosition = new Vector3(Random.Range(-spawnWidth, spawnWidth) * 0.5f, 0f, 0f);
            p.tr.localScale = Vector3.one * p.baseScale;
        }

        void Update()
        {
            if (_puffs == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;

            for (int i = 0; i < _puffs.Length; i++)
            {
                ref Puff p = ref _puffs[i];
                if (p.tr == null) continue;

                p.life += dt;
                if (p.life >= p.maxLife) { Respawn(ref p, false); continue; }

                float k = p.life / p.maxLife;          // 0 abajo, 1 arriba
                float y = k * riseHeight;
                float x = p.tr.localPosition.x
                        + (windDrift * k + Mathf.Sin(t * p.wobble + p.phase) * 0.25f) * dt;

                p.tr.localPosition = new Vector3(x, y, 0f);
                p.tr.localScale = Vector3.one * (p.baseScale * (1f + growth * k));

                // aparece rapido y se deshace despacio segun sube
                float envelope = Mathf.Min(Mathf.Clamp01(k * 5f), Mathf.Clamp01((1f - k) * 1.8f));
                Color c = p.sr.color;
                c.a = maxAlpha * envelope;
                p.sr.color = c;
            }
        }
    }
}
