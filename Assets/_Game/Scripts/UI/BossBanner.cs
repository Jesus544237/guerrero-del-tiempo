using Ekkar.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// La cara visible de una pelea de jefe: el cartel de entrada y la barra
    /// de vida grande de arriba.
    ///
    /// Lo monta <see cref="BossEncounter"/> cuando empieza el combate y se va
    /// solo cuando termina. Antes un jefe se distinguia de un enemigo normal
    /// solo en que tardaba mas en caer; ahora al menos se anuncia, se le ve la
    /// vida bajar y se nota cuando cambia de fase.
    /// </summary>
    public class BossBanner : MonoBehaviour
    {
        static readonly Color Oro = new Color(0.980f, 0.753f, 0.141f);
        static readonly Color Cian = new Color(0.133f, 0.827f, 0.929f);
        static readonly Color Hueso = new Color(0.886f, 0.906f, 0.922f);
        static readonly Color Sangre = new Color(0.78f, 0.11f, 0.15f);
        static readonly Color Oscuro = new Color(0.04f, 0.02f, 0.08f, 0.92f);

        TMP_FontAsset _fuente;
        CanvasGroup _grupoCartel, _grupoBarra;
        RectTransform _relleno, _fantasma, _bloqueCartel;
        Image _tinte;
        TextMeshProUGUI _nombreCartel, _subtitulo, _nombreBarra, _faseTexto;

        float _objetivo = 1f, _suave = 1f;
        float _cartelHasta = -1f;
        Color _tinteColor = Cian;
        bool _visible;

        public static BossBanner Crea(TMP_FontAsset fuente)
        {
            var go = new GameObject("UI_Jefe");
            var b = go.AddComponent<BossBanner>();
            b._fuente = fuente;
            b.Construye();
            return b;
        }

        // ------------------------------------------------------------- uso

        /// <summary>El cartel de entrada, unos segundos y se va.</summary>
        public void Presenta(string nombre, string subtitulo, float segundos = 3.2f)
        {
            if (_nombreCartel != null) _nombreCartel.text = nombre;
            if (_subtitulo != null) _subtitulo.text = subtitulo;
            _cartelHasta = Time.unscaledTime + segundos;
            if (_grupoCartel != null) _grupoCartel.alpha = 0f;
            if (_bloqueCartel != null) _bloqueCartel.localScale = new Vector3(1.1f, 0.75f, 1f);
            Destella(Oro, 0.35f);
        }

        public void MuestraBarra(string nombre)
        {
            if (_nombreBarra != null) _nombreBarra.text = nombre;
            if (_grupoBarra != null) _grupoBarra.alpha = 0f;
            _objetivo = 1f;
            _suave = 1f;
            _visible = true;
        }

        public void Vida(float fraccion) => _objetivo = Mathf.Clamp01(fraccion);

        public void Fase(int fase, int total)
        {
            if (_faseTexto != null)
                _faseTexto.text = total > 1 ? $"FASE {Mathf.Clamp(fase, 1, total)} / {total}" : "";
            Destella(Cian, 0.5f);
        }

        public void Destella(Color color, float fuerza)
        {
            _tinteColor = color;
            if (_tinte != null) _tinte.color = new Color(color.r, color.g, color.b, fuerza);
        }

        public void Cierra()
        {
            _visible = false;
            _cartelHasta = -1f;
        }

        // ----------------------------------------------------------- ciclo

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // ---- cartel de entrada
            if (_grupoCartel != null)
            {
                bool dentro = Time.unscaledTime < _cartelHasta;
                float queda = _cartelHasta - Time.unscaledTime;
                float destino = dentro ? (queda < 0.7f ? Mathf.Clamp01(queda / 0.7f) : 1f) : 0f;
                _grupoCartel.alpha = Mathf.MoveTowards(_grupoCartel.alpha, destino, dt * 3.2f);
                if (_bloqueCartel != null)
                    _bloqueCartel.localScale = Vector3.Lerp(_bloqueCartel.localScale, Vector3.one, dt * 7f);
            }

            // ---- barra
            if (_grupoBarra != null)
                _grupoBarra.alpha = Mathf.MoveTowards(_grupoBarra.alpha, _visible ? 1f : 0f, dt * 2.4f);

            _suave = Mathf.MoveTowards(_suave, _objetivo, dt * 0.55f);
            if (_relleno != null) _relleno.localScale = new Vector3(_objetivo, 1f, 1f);
            if (_fantasma != null) _fantasma.localScale = new Vector3(Mathf.Max(_suave, _objetivo), 1f, 1f);

            // ---- fogonazo de fase
            if (_tinte != null)
            {
                var c = _tinte.color;
                if (c.a > 0.001f)
                {
                    c.a = Mathf.MoveTowards(c.a, 0f, dt * 1.6f);
                    _tinte.color = new Color(_tinteColor.r, _tinteColor.g, _tinteColor.b, c.a);
                }
            }
        }

        // --------------------------------------------------------- montaje

        void Construye()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 330;

            var escala = gameObject.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;

            _tinte = Caja("Fogonazo", new Color(0f, 0f, 0f, 0f), transform);
            Pantalla(_tinte.rectTransform);

            ConstruyeBarra();
            ConstruyeCartel();
        }

        void ConstruyeBarra()
        {
            var raiz = new GameObject("Barra", typeof(RectTransform));
            raiz.transform.SetParent(transform, false);
            _grupoBarra = raiz.AddComponent<CanvasGroup>();
            _grupoBarra.alpha = 0f;
            _grupoBarra.blocksRaycasts = false;

            var rt = (RectTransform)raiz.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1180f, 96f);
            rt.anchoredPosition = new Vector2(0f, -28f);

            _nombreBarra = Texto("Nombre", "", 30f, Oro, raiz.transform,
                                 new Vector2(0f, -14f), new Vector2(1100f, 36f));
            _nombreBarra.rectTransform.anchorMin = _nombreBarra.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _nombreBarra.rectTransform.pivot = new Vector2(0.5f, 1f);

            var marco = Caja("Marco", Oro, raiz.transform);
            var mrt = marco.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1f);
            mrt.pivot = new Vector2(0.5f, 1f);
            mrt.sizeDelta = new Vector2(1140f, 30f);
            mrt.anchoredPosition = new Vector2(0f, -52f);

            var fondo = Caja("Fondo", Oscuro, marco.transform);
            Dentro(fondo.rectTransform, 3f);

            _fantasma = Caja("Fantasma", new Color(1f, 1f, 1f, 0.4f), fondo.transform).rectTransform;
            Dentro(_fantasma, 0f);
            _fantasma.pivot = new Vector2(0f, 0.5f);

            _relleno = Caja("Vida", Sangre, fondo.transform).rectTransform;
            Dentro(_relleno, 0f);
            _relleno.pivot = new Vector2(0f, 0.5f);

            _faseTexto = Texto("Fase", "", 20f, Cian, raiz.transform,
                               new Vector2(0f, -86f), new Vector2(600f, 24f));
            _faseTexto.rectTransform.anchorMin = _faseTexto.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _faseTexto.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        void ConstruyeCartel()
        {
            var raiz = new GameObject("Cartel", typeof(RectTransform));
            raiz.transform.SetParent(transform, false);
            _grupoCartel = raiz.AddComponent<CanvasGroup>();
            _grupoCartel.alpha = 0f;
            _grupoCartel.blocksRaycasts = false;
            Pantalla((RectTransform)raiz.transform);

            var bloque = new GameObject("Bloque", typeof(RectTransform));
            bloque.transform.SetParent(raiz.transform, false);
            _bloqueCartel = (RectTransform)bloque.transform;
            _bloqueCartel.anchorMin = _bloqueCartel.anchorMax = new Vector2(0.5f, 0.5f);
            _bloqueCartel.sizeDelta = new Vector2(1500f, 320f);
            _bloqueCartel.anchoredPosition = Vector2.zero;

            var velo = Caja("Franja", new Color(0.04f, 0.02f, 0.08f, 0.78f), _bloqueCartel);
            var vrt = velo.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.sizeDelta = new Vector2(0f, 230f);
            vrt.anchoredPosition = Vector2.zero;

            Regla(_bloqueCartel, 96f);
            Regla(_bloqueCartel, -96f);

            _nombreCartel = Texto("Nombre", "", 84f, Oro, _bloqueCartel,
                                  new Vector2(0f, 26f), new Vector2(1500f, 110f));
            _subtitulo = Texto("Sub", "", 28f, Hueso, _bloqueCartel,
                               new Vector2(0f, -46f), new Vector2(1500f, 40f));
        }

        void Regla(Transform padre, float y)
        {
            var linea = Caja("Regla", new Color(Oro.r, Oro.g, Oro.b, 0.85f), padre);
            var rt = linea.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760f, 3f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        // ---------------------------------------------------------- piezas

        static Image Caja(string nombre, Color c, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Texto(string nombre, string texto, float tam, Color color,
                              Transform padre, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_fuente != null) t.font = _fuente;
            t.text = texto;
            t.fontSize = tam;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return t;
        }

        static void Pantalla(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Dentro(RectTransform rt, float margen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }
    }
}
