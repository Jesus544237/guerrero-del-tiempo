using Ekkar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.FX
{
    /// <summary>
    /// Lineas horizontales de energia temporal que barren la pantalla: crecen
    /// desde el centro, brillan y se apagan. Equivale a las ".energy-line" del
    /// maquetado, pero apareciendo en posiciones aleatorias.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EnergyLines : MonoBehaviour
    {
        [SerializeField] Sprite lineSprite;
        [SerializeField] int lineCount = 4;
        [SerializeField] Vector2 widthRange = new Vector2(180f, 480f);
        [SerializeField] float thickness = 2f;
        [SerializeField] Vector2 intervalRange = new Vector2(1.4f, 4.5f);
        [SerializeField] float lifetime = 1.6f;
        [SerializeField, Range(0f, 1f)] float peakAlpha = 0.55f;
        [SerializeField] Color[] tints;

        RectTransform _rect;
        RectTransform[] _lines;
        Image[] _images;
        float[] _timers;
        float[] _delays;

        void Awake()
        {
            _rect = (RectTransform)transform;
            if (tints == null || tints.Length == 0)
                tints = new[] { Palette.Cyan, Palette.CyanBright, Palette.PurpleLight };
        }

        void Start()
        {
            _lines = new RectTransform[lineCount];
            _images = new Image[lineCount];
            _timers = new float[lineCount];
            _delays = new float[lineCount];

            for (int i = 0; i < lineCount; i++)
            {
                var go = new GameObject("EnergyLine", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_rect, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = go.GetComponent<Image>();
                img.sprite = lineSprite;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0f);

                _lines[i] = rt;
                _images[i] = img;
                _timers[i] = 0f;
                _delays[i] = Random.Range(0f, intervalRange.y);
            }
        }

        void Update()
        {
            if (_lines == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector2 area = _rect.rect.size;
            if (area.x < 1f) area = new Vector2(1920f, 1080f);

            for (int i = 0; i < _lines.Length; i++)
            {
                if (_delays[i] > 0f)
                {
                    _delays[i] -= dt;
                    if (_delays[i] <= 0f) Spawn(i, area);
                    continue;
                }

                _timers[i] += dt;
                float k = _timers[i] / lifetime;
                if (k >= 1f)
                {
                    _images[i].color = new Color(1f, 1f, 1f, 0f);
                    _delays[i] = Random.Range(intervalRange.x, intervalRange.y);
                    continue;
                }

                float grow = Ease.OutCubic(Mathf.Clamp01(k * 1.6f));
                float alpha = Ease.Spike(k) * peakAlpha;

                Vector3 s = _lines[i].localScale;
                s.x = grow;
                _lines[i].localScale = s;

                Color c = _images[i].color;
                c.a = alpha;
                _images[i].color = c;
            }
        }

        void Spawn(int i, Vector2 area)
        {
            _timers[i] = 0f;
            float w = Random.Range(widthRange.x, widthRange.y);
            _lines[i].sizeDelta = new Vector2(w, thickness);
            _lines[i].anchoredPosition = new Vector2(
                Random.Range(-area.x * 0.45f, area.x * 0.45f),
                Random.Range(-area.y * 0.45f, area.y * 0.45f));
            _lines[i].localScale = new Vector3(0f, 1f, 1f);

            Color tint = tints[Random.Range(0, tints.Length)];
            _images[i].color = new Color(tint.r, tint.g, tint.b, 0f);
        }
    }
}
