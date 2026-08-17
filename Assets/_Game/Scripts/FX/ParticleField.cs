using Ekkar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.FX
{
    /// <summary>
    /// Campo de particulas hecho con Images agrupadas (equivale al canvas de
    /// particulas del maquetado). Crea sus hijos al arrancar, asi que la
    /// jerarquia de la escena se mantiene limpia.
    /// Respeta el ajuste "Particulas" de Opciones.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ParticleField : MonoBehaviour
    {
        public enum FieldMode
        {
            Embers,   // motas que ascienden (energia temporal)
            Stars     // puntos fijos que parpadean
        }

        [SerializeField] FieldMode mode = FieldMode.Embers;
        [SerializeField] Sprite particleSprite;
        [SerializeField] int count = 55;
        [SerializeField] Vector2 sizeRange = new Vector2(3f, 8f);
        [SerializeField] Vector2 riseSpeedRange = new Vector2(10f, 34f);
        [SerializeField] Vector2 driftSpeedRange = new Vector2(-8f, 8f);
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.6f;
        [SerializeField] Color[] tints;
        [SerializeField] bool obeyParticleSetting = true;

        struct Mote
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float life, maxLife, alpha, twinkleSpeed, twinklePhase;
            public Color tint;
        }

        Mote[] _motes;
        RectTransform _rect;
        Vector2 _area;
        Transform _container;

        void Awake()
        {
            _rect = (RectTransform)transform;
            if (tints == null || tints.Length == 0)
                tints = new[] { Palette.Cyan, Palette.PurpleLight, Palette.Gold, Palette.CyanBright };
        }

        void OnEnable()  { GameSettings.FxChanged += ApplySetting; }
        void OnDisable() { GameSettings.FxChanged -= ApplySetting; }

        void Start()
        {
            Build();
            ApplySetting();
        }

        void ApplySetting()
        {
            if (!obeyParticleSetting || _container == null) return;
            _container.gameObject.SetActive(GameSettings.Particles);
        }

        void Build()
        {
            _area = _rect.rect.size;
            if (_area.x < 1f || _area.y < 1f) _area = new Vector2(1920f, 1080f);

            var containerGo = new GameObject("Motes", typeof(RectTransform));
            var containerRect = (RectTransform)containerGo.transform;
            containerRect.SetParent(_rect, false);
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;
            _container = containerGo.transform;

            _motes = new Mote[Mathf.Max(0, count)];
            for (int i = 0; i < _motes.Length; i++)
            {
                var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(containerRect, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = go.GetComponent<Image>();
                img.sprite = particleSprite;
                img.raycastTarget = false;
                if (particleSprite == null) img.color = Color.white;

                _motes[i] = new Mote { rect = rt, image = img };
                Respawn(ref _motes[i], true);
            }
        }

        void Respawn(ref Mote m, bool anywhere)
        {
            float half = 0.5f;
            float x = Random.Range(-_area.x * half, _area.x * half);
            float y = mode == FieldMode.Embers && !anywhere
                ? -_area.y * half - Random.Range(0f, 60f)
                : Random.Range(-_area.y * half, _area.y * half);

            m.rect.anchoredPosition = new Vector2(x, y);

            float size = Random.Range(sizeRange.x, sizeRange.y);
            m.rect.sizeDelta = new Vector2(size, size);

            m.tint = tints[Random.Range(0, tints.Length)];
            m.alpha = Random.Range(maxAlpha * 0.35f, maxAlpha);
            m.twinkleSpeed = Random.Range(0.4f, 2.2f);
            m.twinklePhase = Random.Range(0f, Mathf.PI * 2f);

            if (mode == FieldMode.Embers)
            {
                m.velocity = new Vector2(Random.Range(driftSpeedRange.x, driftSpeedRange.y),
                                         Random.Range(riseSpeedRange.x, riseSpeedRange.y));
                m.maxLife = Random.Range(6f, 15f);
                m.life = anywhere ? Random.Range(0f, m.maxLife) : 0f;
            }
            else
            {
                m.velocity = new Vector2(0f, Random.Range(1.5f, 5f));
                m.maxLife = float.PositiveInfinity;
                m.life = 0f;
            }

            m.image.color = new Color(m.tint.r, m.tint.g, m.tint.b, 0f);
        }

        void Update()
        {
            if (_motes == null) return;
            float dt = Time.unscaledDeltaTime;
            float halfY = _area.y * 0.5f;
            float halfX = _area.x * 0.5f;

            for (int i = 0; i < _motes.Length; i++)
            {
                ref Mote m = ref _motes[i];
                if (m.rect == null) continue;

                Vector2 p = m.rect.anchoredPosition + m.velocity * dt;

                if (mode == FieldMode.Embers)
                {
                    m.life += dt;
                    if (m.life >= m.maxLife || p.y > halfY + 40f) { Respawn(ref m, false); continue; }

                    if (p.x < -halfX) p.x += _area.x;
                    else if (p.x > halfX) p.x -= _area.x;

                    // aparece y se apaga suavemente a lo largo de su vida
                    float k = m.life / m.maxLife;
                    float envelope = Mathf.Min(Mathf.Clamp01(k * 6f), Mathf.Clamp01((1f - k) * 3f));
                    float twinkle = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * m.twinkleSpeed + m.twinklePhase);
                    m.image.color = new Color(m.tint.r, m.tint.g, m.tint.b, m.alpha * envelope * twinkle);
                }
                else
                {
                    if (p.y > halfY) p.y -= _area.y;
                    float twinkle = 0.45f + 0.55f * Mathf.Sin(Time.unscaledTime * m.twinkleSpeed + m.twinklePhase);
                    m.image.color = new Color(m.tint.r, m.tint.g, m.tint.b, m.alpha * twinkle);
                }

                m.rect.anchoredPosition = p;
            }
        }
    }
}
