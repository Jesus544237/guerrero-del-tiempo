using TMPro;
using UnityEngine;

namespace Ekkar.UI
{
    /// <summary>
    /// Late el resplandor del material SDF del texto (propiedades _GlowPower y
    /// _GlowOuter). Al tocar solo el material y no la geometria, convive sin
    /// problemas con <see cref="TMPTextIntro"/>.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Ekkar/TMP Glow Pulse")]
    public class TMPGlowPulse : MonoBehaviour
    {
        [SerializeField] Color glowColor = new Color(0.98f, 0.75f, 0.14f, 1f);
        [SerializeField] float minPower = 0.15f;
        [SerializeField] float maxPower = 0.65f;
        [SerializeField] float minOuter = 0.10f;
        [SerializeField] float maxOuter = 0.34f;
        [SerializeField] float period = 3.6f;
        [SerializeField] float startDelay = 0f;

        static readonly int ID_GlowColor = Shader.PropertyToID("_GlowColor");
        static readonly int ID_GlowPower = Shader.PropertyToID("_GlowPower");
        static readonly int ID_GlowOuter = Shader.PropertyToID("_GlowOuter");
        static readonly int ID_GlowInner = Shader.PropertyToID("_GlowInner");

        TMP_Text _text;
        Material _material;
        float _elapsed;
        bool _ready;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        void Start()
        {
            if (_text == null) { enabled = false; return; }

            _material = _text.fontMaterial;   // instancia propia, no toca el asset compartido
            if (_material == null || !_material.HasProperty(ID_GlowPower)) { enabled = false; return; }

            _material.EnableKeyword("GLOW_ON");
            _material.SetColor(ID_GlowColor, glowColor);
            if (_material.HasProperty(ID_GlowInner)) _material.SetFloat(ID_GlowInner, 0.05f);
            _ready = true;
        }

        void Update()
        {
            if (!_ready) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < startDelay)
            {
                _material.SetFloat(ID_GlowPower, minPower);
                _material.SetFloat(ID_GlowOuter, minOuter);
                return;
            }

            if (period < 0.01f) return;
            float k = 0.5f + 0.5f * Mathf.Sin((_elapsed - startDelay) * Mathf.PI * 2f / period);
            _material.SetFloat(ID_GlowPower, Mathf.Lerp(minPower, maxPower, k));
            _material.SetFloat(ID_GlowOuter, Mathf.Lerp(minOuter, maxOuter, k));
        }
    }
}
