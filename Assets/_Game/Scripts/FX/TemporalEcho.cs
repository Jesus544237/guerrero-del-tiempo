using Ekkar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.FX
{
    /// <summary>
    /// "Eco temporal": copias fantasma del personaje que muestran fotogramas
    /// del pasado, desplazadas y tenidas de cian. Refuerza la idea de que
    /// Ekkar arrastra su propia linea de tiempo.
    /// Los fantasmas se crean como hijos de este objeto, que debe ir por
    /// detras del personaje en la jerarquia.
    /// </summary>
    public class TemporalEcho : MonoBehaviour
    {
        [SerializeField] Image source;
        [SerializeField] int echoCount = 3;
        [SerializeField] float delayPerEcho = 0.13f;
        [SerializeField] Vector2 offsetPerEcho = new Vector2(-26f, 0f);
        [SerializeField, Range(0f, 1f)] float firstEchoAlpha = 0.24f;
        [SerializeField, Range(0f, 1f)] float falloff = 0.55f;
        [SerializeField] Color tint = new Color(0.13f, 0.83f, 0.93f, 1f);
        [SerializeField] float breathePeriod = 5f;
        [SerializeField] bool obeyParticleSetting = true;

        const int HistorySize = 128;

        Sprite[] _historySprite = new Sprite[HistorySize];
        float[] _historyTime = new float[HistorySize];
        int _historyHead = -1;

        Image[] _ghosts;
        RectTransform _sourceRect;

        void Awake()
        {
            if (source == null) return;
            _sourceRect = (RectTransform)source.transform;
            if (tint == default) tint = Palette.CyanBright;
        }

        void OnEnable()  { GameSettings.FxChanged += ApplySetting; }
        void OnDisable() { GameSettings.FxChanged -= ApplySetting; }

        void Start()
        {
            if (source == null) { enabled = false; return; }

            _ghosts = new Image[Mathf.Max(0, echoCount)];
            for (int i = 0; i < _ghosts.Length; i++)
            {
                var go = new GameObject($"Echo_{i}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = _sourceRect.anchorMin;
                rt.anchorMax = _sourceRect.anchorMax;
                rt.pivot = _sourceRect.pivot;
                rt.sizeDelta = _sourceRect.sizeDelta;
                rt.anchoredPosition = _sourceRect.anchoredPosition + offsetPerEcho * (i + 1);
                rt.localScale = _sourceRect.localScale;

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = source.preserveAspect;
                img.sprite = source.sprite;
                img.color = new Color(tint.r, tint.g, tint.b, 0f);

                // los mas lejanos se dibujan primero
                rt.SetSiblingIndex(0);
                _ghosts[i] = img;
            }

            ApplySetting();
        }

        void ApplySetting()
        {
            if (!obeyParticleSetting || _ghosts == null) return;
            foreach (var g in _ghosts)
                if (g != null) g.gameObject.SetActive(GameSettings.Particles);
        }

        void Update()
        {
            if (source == null || _ghosts == null) return;

            // registra el fotograma actual
            _historyHead = (_historyHead + 1) % HistorySize;
            _historySprite[_historyHead] = source.sprite;
            _historyTime[_historyHead] = Time.unscaledTime;

            float breathe = breathePeriod > 0.01f
                ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / breathePeriod)
                : 1f;

            for (int i = 0; i < _ghosts.Length; i++)
            {
                var ghost = _ghosts[i];
                if (ghost == null) continue;

                Sprite past = SampleHistory(Time.unscaledTime - delayPerEcho * (i + 1));
                if (past != null) ghost.sprite = past;

                var rt = (RectTransform)ghost.transform;
                rt.anchoredPosition = _sourceRect.anchoredPosition + offsetPerEcho * (i + 1);
                rt.sizeDelta = _sourceRect.sizeDelta;
                rt.localScale = _sourceRect.localScale;

                float alpha = firstEchoAlpha * Mathf.Pow(falloff, i) * breathe * source.color.a;
                ghost.color = new Color(tint.r, tint.g, tint.b, alpha);
            }
        }

        Sprite SampleHistory(float time)
        {
            if (_historyHead < 0) return null;
            for (int step = 0; step < HistorySize; step++)
            {
                int idx = (_historyHead - step + HistorySize) % HistorySize;
                if (_historySprite[idx] == null) break;
                if (_historyTime[idx] <= time) return _historySprite[idx];
            }
            return _historySprite[_historyHead];
        }
    }
}
